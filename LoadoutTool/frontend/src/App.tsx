import { useEffect, useMemo, useRef, useState } from "react"
import { Plus } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import {
  InputGroup,
  InputGroupAddon,
  InputGroupInput,
} from "@/components/ui/input-group"
import { TraitPicker } from "./TraitPicker"
import { LoadTraits, LoadConfig, SaveLoadout, MinimiseApp, GetHotkey } from "../bindings/loadouttool/loadoutservice"

const MAX_SLOTS = 12 // fixed rows shown in the editor

type Lang = "zh" | "en"

const copy = {
  zh: {
    headerPrimary: "主因子",
    headerSecondary: "副因子",
    selectAll: "全选/反选",
    pickTrait: "选择因子",
    none: "无",
    search: "搜索",
    empty: "无匹配因子",
    dictFail: (e: unknown) => `词条字典加载失败：${e}`,
    configFail: (e: unknown) => `配装加载失败：${e}`,
    unknown: (names: string) => `存在字典外的词条（未保存）：${names}`,
    saveFail: (e: unknown) => `自动保存失败：${e}`,
  },
  en: {
    headerPrimary: "Primary Sigil",
    headerSecondary: "Secondary Sigil",
    selectAll: "Select all / none",
    pickTrait: "Select sigil",
    none: "None",
    search: "Search",
    empty: "No matching sigils",
    dictFail: (e: unknown) => `Failed to load trait dictionary: ${e}`,
    configFail: (e: unknown) => `Failed to load loadout: ${e}`,
    unknown: (names: string) => `Unknown traits (not saved): ${names}`,
    saveFail: (e: unknown) => `Auto-save failed: ${e}`,
  },
} as const

interface Slot {
  trait1: string
  level1: number
  trait2: string
  level2: number
  enabled: boolean
}

interface Trait {
  zh: string
  en: string
  maxLevel: number
}

/* Fixed side columns + factor columns that eat all remaining width. */
const GRID_COLS =
  "grid grid-cols-[2.5rem_2rem_minmax(0,1fr)_minmax(0,1fr)] items-center gap-x-2"

const HEADER_ROW = `${GRID_COLS} border-b pb-2 text-sm font-medium text-foreground`
const DATA_ROW = `${GRID_COLS} border-b py-2 text-sm transition-colors last:border-b-0 hover:bg-muted/50`

/** Clamped numeric level input with a grey "/ max" suffix. */
function LevelInput({
  value,
  max,
  min = 1,
  onLevel,
}: {
  value: number
  max: number
  min?: number
  onLevel: (n: number) => void
}) {
  return (
    <InputGroup className="w-20 shrink-0">
      <InputGroupInput
        type="number"
        min={min}
        max={max}
        value={value}
        onChange={(e) => {
          const n = Math.max(min, Math.floor(Math.min(Number(e.target.value), max)))
          onLevel(n)
          if (e.target.value !== String(n)) e.target.value = String(n)
        }}
        className="py-0 pb-px text-center leading-[36px] [appearance:textfield] [&::-webkit-outer-spin-button]:appearance-none [&::-webkit-inner-spin-button]:appearance-none"
      />
      <InputGroupAddon align="inline-end" className="text-[#a0a0a0] tabular-nums">
        / {max}
      </InputGroupAddon>
    </InputGroup>
  )
}

function normalizeSlot(raw: unknown): Slot {
  const s = (raw ?? {}) as Partial<Slot>
  const trait1 = typeof s.trait1 === "string" ? s.trait1 : ""
  const trait2 = typeof s.trait2 === "string" ? s.trait2 : ""
  return {
    trait1,
    level1: trait1 ? (Number.isFinite(s.level1) ? (s.level1 as number) : 15) : 0,
    trait2,
    level2: trait2 ? (Number.isFinite(s.level2) ? (s.level2 as number) : 15) : 0,
    enabled: s.enabled !== false,
  }
}

const emptySlot = (): Slot => ({ trait1: "", level1: 0, trait2: "", level2: 0, enabled: true })

/** Always pad the editor to MAX_SLOTS rows so the user just fills them in. */
function pad12(slots: Slot[]): Slot[] {
  const out = [...slots]
  while (out.length < MAX_SLOTS) out.push(emptySlot())
  return out.slice(0, MAX_SLOTS)
}

export default function App() {
  const [traits, setTraits] = useState<Trait[]>([])
  const [slots, setSlots] = useState<Slot[]>([])
  const [status, setStatus] = useState("")
  const [hideKey, setHideKey] = useState(0x70) // F1 default (matches mod default)
  const [lang, setLang] = useState<Lang>(() =>
    typeof localStorage !== "undefined" && localStorage.getItem("lang") === "en" ? "en" : "zh"
  )
  const t = copy[lang]
  const toggleLang = () => {
    setLang((prev) => {
      const next: Lang = prev === "zh" ? "en" : "zh"
      try {
        localStorage.setItem("lang", next)
      } catch {
        // non-persistent environments
      }
      return next
    })
  }
  // First render + first load must not write loadout.json: the preset stays
  // active until the user actually edits something.
  const skipSave = useRef(true)
  const saveTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  useEffect(() => {
    ;(async () => {
      // Independent loads: one broken file must not block the rest.
      try {
        const traitJson = await LoadTraits()
        setTraits(
          (JSON.parse(traitJson).traits as { zh: string; en?: string; maxLevel?: number }[]).map(
            (tr) => ({ zh: tr.zh, en: tr.en ?? tr.zh, maxLevel: tr.maxLevel ?? 15 })
          )
        )
      } catch (e) {
        setStatus(t.dictFail(e))
      }
      try {
        const configJson = await LoadConfig()
        const parsed = JSON.parse(configJson)
        const rawSlots = Array.isArray(parsed?.slots) ? parsed.slots : []
        skipSave.current = true
        setSlots(pad12(rawSlots.map(normalizeSlot)))
      } catch (e) {
        setStatus(t.configFail(e))
      }
      try {
        setHideKey(await GetHotkey())
      } catch {
        // keep F1 default
      }
    })()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const traitMax = useMemo(
    () => new Map(traits.map((tr) => [tr.zh, tr.maxLevel] as const)),
    [traits]
  )
  const maxOf = (name: string) => traitMax.get(name) ?? 15
  const traitNames = traits.map((tr) => tr.zh)
  const traitLabels = useMemo(
    () =>
      Object.fromEntries(traits.map((tr) => [tr.zh, lang === "zh" ? tr.zh : tr.en])),
    [traits, lang]
  )

  const updateSlot = (index: number, patch: Partial<Slot>) => {
    setSlots((prev) => prev.map((slot, i) => (i === index ? { ...slot, ...patch } : slot)))
  }

  // Header check box: select all / clear all (official Table pattern).
  const allEnabled = slots.length > 0 && slots.every((s) => s.enabled)
  const toggleAll = () => {
    setSlots((prev) => prev.map((slot) => ({ ...slot, enabled: !allEnabled })))
  }

  const save = async () => {
    const filled = slots.filter((s) => s.trait1 !== "")
    const unknown = filled.filter(
      (s) =>
        (s.trait1 !== "" && !traitMax.has(s.trait1)) ||
        (s.trait2 !== "" && !traitMax.has(s.trait2))
    )
    if (unknown.length > 0) {
      const names = unknown.map((s) => s.trait1 || s.trait2).join("、")
      setStatus(t.unknown(names))
      return
    }
    try {
      await SaveLoadout(JSON.stringify({ slots: filled }, null, 2))
    } catch (e) {
      setStatus(t.saveFail(e))
    }
  }

  // Auto-save on edits (300 ms debounce; first load skips writing once).
  useEffect(() => {
    if (skipSave.current) {
      skipSave.current = false
      return
    }
    clearTimeout(saveTimer.current)
    saveTimer.current = setTimeout(() => void save(), 300)
    return () => clearTimeout(saveTimer.current)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [slots])

  // The hide key is the SAME key as the mod's menu hotkey (default F1,
  // configurable; the mod publishes it in tool-hotkey.txt). Pressed here it
  // minimises the window; pressed in the game it brings the tool back. The
  // hide is deferred until AFTER the key-up so the press is fully consumed
  // here and never leaks to the game window.
  useEffect(() => {
    let timer: ReturnType<typeof setTimeout> | undefined
    const match = (e: KeyboardEvent) => (e.keyCode || e.which) === hideKey
    const onKeyDown = (e: KeyboardEvent) => {
      if (!match(e)) return
      clearTimeout(timer)
      e.preventDefault()
    }
    const onKeyUp = (e: KeyboardEvent) => {
      if (!match(e)) return
      clearTimeout(timer)
      timer = setTimeout(() => void MinimiseApp(), 150)
    }
    document.addEventListener("keydown", onKeyDown)
    document.addEventListener("keyup", onKeyUp)
    return () => {
      document.removeEventListener("keydown", onKeyDown)
      document.removeEventListener("keyup", onKeyUp)
      clearTimeout(timer)
    }
  }, [hideKey])

  return (
    <div className="fixed inset-0 flex flex-col">
      <div className="min-h-0 flex-1 overflow-y-auto px-4 pt-6 pb-0 [scrollbar-gutter:stable]">
        {status && (
          <div className="mb-2 rounded-md bg-muted/50 px-3 py-1.5 text-sm text-muted-foreground">
            {status}
          </div>
        )}
        <div className={HEADER_ROW}>
          <div>
            <Checkbox checked={allEnabled} onCheckedChange={toggleAll} aria-label={t.selectAll} />
          </div>
          <div>#</div>
          <div className="pr-2 pl-[11px]">{t.headerPrimary}</div>
          <div className="pl-[21px]">{t.headerSecondary}</div>
        </div>

        {slots.map((slot, index) => (
          <div key={index} className={DATA_ROW}>
            <div>
              <Checkbox
                checked={slot.enabled}
                onCheckedChange={(v) => updateSlot(index, { enabled: v === true })}
              />
            </div>
            <div>
              <span className="text-muted-foreground tabular-nums">{index + 1}</span>
            </div>
            <div className="flex min-w-0 items-center gap-1.5 pr-2">
              <TraitPicker
                value={slot.trait1}
                traits={traitNames}
                labels={traitLabels}
                placeholder={t.none}
                onSelect={(v) => updateSlot(index, { trait1: v, level1: Math.min(15, maxOf(v)) })}
              />
              <LevelInput
                value={slot.trait1 ? slot.level1 : 0}
                max={maxOf(slot.trait1)}
                min={slot.trait1 ? 1 : 0}
                onLevel={(n) => updateSlot(index, { level1: n })}
              />
            </div>
            <div className="flex min-w-0 items-center gap-1.5 pl-2">
              <TraitPicker
                value={slot.trait2}
                traits={traitNames}
                labels={traitLabels}
                placeholder={t.none}
                noneOption
                noneLabel={t.none}
                searchPlaceholder={t.search}
                emptyLabel={t.empty}
                disabled={!slot.trait1}
                onSelect={(v) => updateSlot(index, { trait2: v, level2: v ? Math.min(15, maxOf(v)) : 0 })}
              />
              <LevelInput
                value={slot.trait2 ? slot.level2 : 0}
                max={maxOf(slot.trait2)}
                min={slot.trait2 ? 1 : 0}
                onLevel={(n) => updateSlot(index, { level2: n })}
              />
            </div>
          </div>
        ))}
      </div>

      <div className="flex shrink-0 items-center border-t bg-background py-3 pr-4">
        <Button
          variant="ghost"
          size="sm"
          className="ml-auto"
          onClick={toggleLang}
          aria-label="Switch language"
        >
          {lang === "zh" ? "EN" : "中"}
        </Button>
      </div>
    </div>
  )
}
