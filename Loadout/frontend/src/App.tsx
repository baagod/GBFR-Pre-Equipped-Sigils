import { useEffect, useMemo, useRef, useState } from "react"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import {
  InputGroup,
  InputGroupAddon,
  InputGroupInput,
} from "@/components/ui/input-group"
import { TraitPicker } from "./TraitPicker"
import { LoadTraits, LoadSigils, LoadConfig, SaveLoadout, MinimiseApp, GetHotkey } from "../bindings/loadouttool/loadoutservice"

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
    sigilFail: (e: unknown) => `因子表加载失败：${e}`,
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
    sigilFail: (e: unknown) => `Failed to load sigil table: ${e}`,
    configFail: (e: unknown) => `Failed to load loadout: ${e}`,
    unknown: (names: string) => `Unknown sigils/traits (not saved): ${names}`,
    saveFail: (e: unknown) => `Auto-save failed: ${e}`,
  },
} as const

interface Slot {
  mainHash: string
  mainLevel: number
  secHash: string
  secLevel: number
  enabled: boolean
}

interface Sigil {
  gem: string
  zh: string
  en: string
  skill: string // primary trait hash
}

interface Trait {
  hash: string
  zh: string
  en: string
  cap: number
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
  const mainHash = typeof s.mainHash === "string" ? s.mainHash : ""
  const secHash = typeof s.secHash === "string" ? s.secHash : ""
  return {
    mainHash,
    mainLevel: mainHash ? (Number.isFinite(s.mainLevel) ? (s.mainLevel as number) : 15) : 0,
    secHash,
    secLevel: secHash ? (Number.isFinite(s.secLevel) ? (s.secLevel as number) : 15) : 0,
    enabled: s.enabled !== false,
  }
}

const emptySlot = (): Slot => ({ mainHash: "", mainLevel: 0, secHash: "", secLevel: 0, enabled: true })

/** Always pad the editor to MAX_SLOTS rows so the user just fills them in. */
function pad12(slots: Slot[]): Slot[] {
  const out = [...slots]
  while (out.length < MAX_SLOTS) out.push(emptySlot())
  return out.slice(0, MAX_SLOTS)
}

/** Normalize a saved config (new array format) into Slot[]. */
function slotsFromConfig(raw: unknown): Slot[] {
  const arr = Array.isArray(raw) ? raw : []
  return arr.map((slot) => {
    const s = (slot ?? {}) as {
      enabled?: boolean
      items?: { gem?: string; hash?: string; level?: number }[]
    }
    const items = Array.isArray(s.items) ? s.items : []
    const main = items[0] ?? {}
    const sec = items[1]
    return {
      mainHash: typeof main.gem === "string" ? main.gem : (typeof main.hash === "string" ? main.hash : ""),
      mainLevel: typeof main.level === "number" ? main.level : 15,
      secHash: sec && typeof sec.hash === "string" ? sec.hash : "",
      secLevel: sec && typeof sec.level === "number" ? sec.level : 15,
      enabled: s.enabled !== false,
    }
  })
}

export default function App() {
  const [traits, setTraits] = useState<Trait[]>([])
  const [sigils, setSigils] = useState<Sigil[]>([])
  const [slots, setSlots] = useState<Slot[]>([])
  const [status, setStatus] = useState("")
  const [hideKey, setHideKey] = useState(0x70) // F1 default (matches mod default)
  const [lang, setLang] = useState<Lang>("zh") // default zh; toggle is session-only
  const t = copy[lang]
  const toggleLang = () => {
    setLang((prev) => (prev === "zh" ? "en" : "zh"))
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
          (JSON.parse(traitJson).traits as { hash?: string; zh: string; en?: string; cap?: number }[])
            .filter((tr) => tr.hash)
            .map((tr) => ({
              hash: tr.hash as string,
              zh: tr.zh,
              en: tr.en ?? tr.zh,
              cap: tr.cap ?? 15,
            }))
        )
      } catch (e) {
        setStatus(t.dictFail(e))
      }
      try {
        const sigilJson = await LoadSigils()
        setSigils(
          (JSON.parse(sigilJson).sigils as { gem?: string; zh: string; en?: string; skill: string }[])
            .filter((s) => s.gem)
            .map((s) => ({ gem: s.gem as string, zh: s.zh, en: s.en ?? s.zh, skill: s.skill }))
        )
      } catch (e) {
        setStatus(t.sigilFail(e))
      }
      try {
        const configJson = await LoadConfig()
        const parsed = JSON.parse(configJson)
        skipSave.current = true
        if (typeof parsed?.lang === "string") setLang(parsed.lang as Lang)
        setSlots(pad12(slotsFromConfig(parsed?.slots)))
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

  const traitByName = useMemo(() => new Map(traits.map((tr) => [tr.hash, tr])), [traits])
  const sigilByName = useMemo(() => new Map(sigils.map((s) => [s.gem, s])), [sigils])
  const maxOfMain = (h: string) => {
    const s = sigilByName.get(h)
    const tr = s ? traitByName.get(s.skill) : undefined
    return tr?.cap ?? 15
  }
  const maxOfSec = (h: string) => traitByName.get(h)?.cap ?? 15

  const sigilHashes = sigils.map((s) => s.gem)
  const traitHashes = traits.map((tr) => tr.hash)
  const hashLabels = useMemo(
    () =>
      Object.fromEntries(
        [...traits, ...sigils].map((x) => [
          "hash" in x ? x.hash : x.gem,
          lang === "zh" ? x.zh : x.en,
        ])
      ),
    [traits, sigils, lang]
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
    const filled = slots.filter((s) => s.mainHash !== "")
    const unknown = filled.filter(
      (s) => !sigilByName.has(s.mainHash) || (s.secHash !== "" && !traitByName.has(s.secHash))
    )
    if (unknown.length > 0) {
      const names = unknown.map((s) => s.mainHash || s.secHash).join("、")
      setStatus(t.unknown(names))
      return
    }
    const cfg = filled.map((s) => {
      const main = {
        gem: s.mainHash,
        level: s.mainLevel,
        zh: sigilByName.get(s.mainHash)?.zh ?? "",
        en: sigilByName.get(s.mainHash)?.en ?? "",
      }
      const items: { gem?: string; hash?: string; level: number; zh: string; en: string }[] = [main]
      if (s.secHash !== "") {
        items.push({
          hash: s.secHash,
          level: s.secLevel,
          zh: traitByName.get(s.secHash)?.zh ?? "",
          en: traitByName.get(s.secHash)?.en ?? "",
        })
      }
      return { items, enabled: s.enabled }
    })
    try {
      await SaveLoadout(JSON.stringify({ lang, slots: cfg }, null, 2))
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
  }, [slots, lang])

  // The hide key is the SAME key as the mod's menu hotkey (default F1,
  // configurable; the mod publishes it in tool-hotkey.txt). Pressed here it
  // minimises the window; pressed in the game it brings the tool back. The
  // hide is deferred until AFTER the key-up so the press is fully consumed
  // here and never leaks to the game window.
  useEffect(() => {
    let timer: ReturnType<typeof setTimeout> | undefined
    const match = (e: KeyboardEvent) =>
      (e.keyCode || e.which) === hideKey || e.key === "Escape"
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
          <SlotRow
            key={index}
            index={index}
            slot={slot}
            sigilHashes={sigilHashes}
            traitHashes={traitHashes}
            labels={hashLabels}
            t={t}
            maxOfMain={maxOfMain}
            maxOfSec={maxOfSec}
            updateSlot={updateSlot}
          />
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

function SlotRow({
  index,
  slot,
  sigilHashes,
  traitHashes,
  labels,
  t,
  maxOfMain,
  maxOfSec,
  updateSlot,
}: {
  index: number
  slot: Slot
  sigilHashes: string[]
  traitHashes: string[]
  labels: Record<string, string>
  t: (typeof copy)["zh"] | (typeof copy)["en"]
  maxOfMain: (h: string) => number
  maxOfSec: (h: string) => number
  updateSlot: (i: number, patch: Partial<Slot>) => void
}) {
  return (
    <div className={DATA_ROW}>
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
          value={slot.mainHash}
          traits={sigilHashes}
          labels={labels}
          placeholder={t.none}
          onSelect={(v) => updateSlot(index, { mainHash: v, mainLevel: Math.min(15, maxOfMain(v)) })}
        />
        <LevelInput
          value={slot.mainHash ? slot.mainLevel : 0}
          max={maxOfMain(slot.mainHash)}
          min={slot.mainHash ? 1 : 0}
          onLevel={(n) => updateSlot(index, { mainLevel: n })}
        />
      </div>
      <div className="flex min-w-0 items-center gap-1.5 pl-2">
        <TraitPicker
          value={slot.secHash}
          traits={traitHashes}
          labels={labels}
          placeholder={t.none}
          noneOption
          noneLabel={t.none}
          searchPlaceholder={t.search}
          emptyLabel={t.empty}
          disabled={!slot.mainHash}
          onSelect={(v) => updateSlot(index, { secHash: v, secLevel: v ? Math.min(15, maxOfSec(v)) : 0 })}
        />
        <LevelInput
          value={slot.secHash ? slot.secLevel : 0}
          max={maxOfSec(slot.secHash)}
          min={slot.secHash ? 1 : 0}
          onLevel={(n) => updateSlot(index, { secLevel: n })}
        />
      </div>
    </div>
  )
}
