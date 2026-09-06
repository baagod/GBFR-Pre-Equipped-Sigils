import { useEffect, useMemo, useRef, useState } from "react"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog"
import {
  InputGroup,
  InputGroupAddon,
  InputGroupInput,
} from "@/components/ui/input-group"
import { TraitPicker } from "./TraitPicker"
import { LoadTraits, LoadSigils, LoadConfig, SaveLoadout, MinimiseApp, GetHotkey, ResetLoadout } from "../bindings/loadouttool/loadoutservice"

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
    saveFail: (e: unknown) => `自动保存失败：${e}`,
    reset: "重置为预设",
    resetDesc: "将删除当前配置，恢复为预设模板。",
    resetConfirm: "重置",
    cancel: "取消",
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
    saveFail: (e: unknown) => `Auto-save failed: ${e}`,
    reset: "Reset to preset",
    resetDesc: "Removes the current configuration and restores the preset template.",
    resetConfirm: "Reset",
    cancel: "Cancel",
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
  key: string
  gem: string
  name: string // display name (base, level suffix stripped); grouping key
  zh: string
  skill: string // primary trait hash
  sec: string // fixed second trait hash ("" for pool-backed / plain)
  pool: string[] // random-pool candidates ([] for fixed / plain)
  category: string
  player: string
  special: boolean
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

const emptySlot = (): Slot => ({ mainHash: "", mainLevel: 0, secHash: "", secLevel: 0, enabled: true })

/** Normalize a saved config (new array format) into Slot[] (mainHash = name).
 * Stored gems are mapped to display names via the sigil table; unknown gems
 * keep their raw value. */
function configToSlots(
  parsed: { slots?: unknown; lang?: string },
  sigils: Sigil[]
): Slot[] {
  const fromCfg = slotsFromConfig(parsed?.slots)
  const nameOfGem = new Map(sigils.map((s) => [s.gem, s.name]))
  for (const s of fromCfg) {
    if (nameOfGem.has(s.mainHash)) s.mainHash = nameOfGem.get(s.mainHash) as string
  }
  return fromCfg
}

/** Always pad the editor to MAX_SLOTS rows so the user just fills them in. */
function pad12(slots: Slot[]): Slot[] {
  const out = [...slots]
  while (out.length < MAX_SLOTS) out.push(emptySlot())
  return out.slice(0, MAX_SLOTS)
}

/** Normalize a saved config (new array format) into Slot[] (mainHash = name). */
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
  const [resetOpen, setResetOpen] = useState(false)
  const [hideKey, setHideKey] = useState(0x70) // F1 default (matches mod default)
  const [lang, setLang] = useState<Lang>("zh") // default zh; toggle is session-only
  const resetCancelRef = useRef<HTMLButtonElement | null>(null)
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
      let sigilsLoaded: Sigil[] = []
      try {
        const sigilJson = await LoadSigils()
        sigilsLoaded = (JSON.parse(sigilJson).sigils as Partial<Sigil>[])
          .filter((s) => s.gem)
          .map((s) => ({
            key: s.key ?? "",
            gem: s.gem as string,
            name: s.name ?? s.zh ?? s.gem as string,
            zh: s.zh ?? "",
            skill: s.skill ?? "",
            sec: s.sec ?? "",
            pool: Array.isArray(s.pool) ? (s.pool as string[]) : [],
            category: s.category ?? "",
            player: s.player ?? "",
            special: s.special === true,
          }))
        setSigils(sigilsLoaded)
      } catch (e) {
        setStatus(t.sigilFail(e))
      }
      try {
        const configJson = await LoadConfig()
        const parsed = JSON.parse(configJson)
        skipSave.current = true
        if (typeof parsed?.lang === "string") setLang(parsed.lang as Lang)
        setSlots(pad12(configToSlots(parsed, sigilsLoaded)))
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
  const sigilByGem = useMemo(() => new Map(sigils.map((s) => [s.gem, s])), [sigils])

  // Variants grouped by display name: one entry per name (unique picker item).
  const sigilGroups = useMemo(() => {
    const byName = new Map<string, Sigil[]>()
    for (const s of sigils) {
      const g = byName.get(s.name)
      if (g) g.push(s)
      else byName.set(s.name, [s])
    }
    return [...byName.entries()].map(([name, variants]) => ({
      name,
      zh: variants[0]?.zh ?? name,
      poolGem: variants.find((v) => v.pool.length > 0)?.gem ?? variants[0]?.gem ?? "",
    }))
  }, [sigils])

  // Exclusive traits: used ONLY by special (single/awakening) sigils; a free
  // main can combine every non-exclusive trait. Computed from the tables.
  const { exclusiveTraits, allTraitHashes, freeTraits } = useMemo(() => {
    const specialUse = new Set<string>()
    const regularUse = new Set<string>()
    const freeTraits = new Set<string>()
    for (const s of sigils) {
      if (s.special) {
        specialUse.add(s.skill)
        if (s.sec) specialUse.add(s.sec)
      } else {
        regularUse.add(s.skill)
        if (s.sec) regularUse.add(s.sec)
        for (const h of s.pool) regularUse.add(h)
        if (s.sec === "" && s.pool.length === 0) freeTraits.add(s.skill)
      }
    }
    const exclusive = new Set([...specialUse].filter((h) => !regularUse.has(h)))
    return {
      exclusiveTraits: exclusive,
      allTraitHashes: traits.map((tr) => tr.hash),
      freeTraits,
    }
  }, [sigils, traits])

  const { legalSecs, freeNames } = useMemo(() => {
    const byName = new Map<string, Sigil[]>()
    for (const s of sigils) {
      const g = byName.get(s.name)
      if (g) g.push(s)
      else byName.set(s.name, [s])
    }
    const m = new Map<string, Set<string>>()
    const free = new Set<string>()
    for (const [name, variants] of byName) {
      const legal = new Set<string>()
      let hasFree = false
      for (const v of variants) {
        for (const h of v.pool) legal.add(h)
        if (v.sec) legal.add(v.sec)
        else if (v.pool.length === 0 && !v.special) hasFree = true
      }
      m.set(name, legal)
      if (hasFree) free.add(name)
    }
    return { legalSecs: m, freeNames: free }
  }, [sigils])

  const legalByMain = (name: string) => {
    if (freeNames.has(name)) {
      // free main can combine every non-exclusive trait (200 - exclusive)
      return new Set(allTraitHashes.filter((h) => !exclusiveTraits.has(h)))
    }
    const base = legalSecs.get(name) ?? new Set<string>()
    const out = new Set(base)
    for (const h of freeTraits) if (!exclusiveTraits.has(h)) out.add(h)
    return out
  }

  // Locate the gem to write for (name, secHash): fixed variant if secHash
  // matches a fixed sec, otherwise the pool-backed variant gem.
  const gemFor = (name: string, secHash: string): string => {
    const variants = sigils.filter((s) => s.name === name)
    const fixed = secHash ? variants.find((v) => v.sec === secHash) : undefined
    if (fixed) return fixed.gem
    return variants.find((v) => v.pool.length > 0)?.gem ?? variants[0]?.gem ?? ""
  }

  const maxOfMain = (name: string) => {
    const variants = sigils.filter((s) => s.name === name)
    const tr = variants.length > 0 ? traitByName.get(variants[0].skill) : undefined
    return tr?.cap ?? 15
  }
  const maxOfSec = (h: string) => traitByName.get(h)?.cap ?? 15

  // Picker item values: main = unique display names; secondary = trait hashes
  // (labels provided by hashLabels).
  const sigilNames = sigilGroups.map((g) => g.name)
  const traitHashes = traits.map((tr) => tr.hash)
  const hashLabels = useMemo(
    () =>
      Object.fromEntries([
        ...traits.map((tr) => [tr.hash, lang === "zh" ? tr.zh : tr.en] as const),
        ...sigilGroups.map((g) => [g.name, lang === "zh" ? g.zh : g.name] as const),
      ]),
    [traits, sigilGroups, lang]
  )
  const updateSlot = (index: number, patch: Partial<Slot>) => {
    setSlots((prev) => prev.map((slot, i) => (i === index ? { ...slot, ...patch } : slot)))
  }

  // Reload the player config (falls back to the preset template). Used after
  // a reset so the UI mirrors the fresh state without restarting the app.
  const reloadConfig = async () => {
    try {
      const configJson = await LoadConfig()
      const parsed = JSON.parse(configJson)
      skipSave.current = true
      if (typeof parsed?.lang === "string") setLang(parsed.lang as Lang)
      setSlots(pad12(configToSlots(parsed, sigils)))
    } catch (e) {
      setStatus(t.configFail(e))
    }
  }

  // Header check box: select all / clear all (official Table pattern).
  const allEnabled = slots.length > 0 && slots.every((s) => s.enabled)
  const toggleAll = () => {
    setSlots((prev) => prev.map((slot) => ({ ...slot, enabled: !allEnabled })))
  }

  const save = async () => {
    if (sigils.length === 0 || traits.length === 0) return // tables not loaded yet
    const filled = slots.filter((s) => s.mainHash !== "")
    const cfg = filled.map((s) => {
      const gem = gemFor(s.mainHash, s.secHash)
      const main = {
        gem,
        level: s.mainLevel,
        zh: sigilByGem.get(gem)?.zh ?? "",
        en: sigilByGem.get(gem)?.name ?? "",
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
            sigilNames={sigilNames}
            traitHashes={traitHashes}
            labels={hashLabels}
            legalOfMain={legalByMain}
            t={t}
            maxOfMain={maxOfMain}
            maxOfSec={maxOfSec}
            updateSlot={updateSlot}
          />
        ))}
      </div>

      <div className="flex shrink-0 items-center border-t bg-background py-3 pr-4">
        <AlertDialog open={resetOpen} onOpenChange={setResetOpen}>
          <AlertDialogTrigger
            render={
              <Button variant="ghost" size="sm" className="ml-auto" aria-label={t.reset}>
                {t.reset}
              </Button>
            }
          />
          <AlertDialogContent size="sm" initialFocus={resetCancelRef}>
            <AlertDialogHeader>
              <AlertDialogTitle>{t.reset}?</AlertDialogTitle>
              <AlertDialogDescription>{t.resetDesc}</AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel ref={resetCancelRef}>{t.cancel}</AlertDialogCancel>
              <AlertDialogAction
                onClick={() => {
                  void ResetLoadout()
                    .then(() => reloadConfig())
                    .finally(() => setResetOpen(false))
                }}
              >
                {t.resetConfirm}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
        <Button
          variant="ghost"
          size="sm"
          className="ml-2"
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
  sigilNames,
  traitHashes,
  labels,
  legalOfMain,
  t,
  maxOfMain,
  maxOfSec,
  updateSlot,
}: {
  index: number
  slot: Slot
  sigilNames: string[]
  traitHashes: string[]
  labels: Record<string, string>
  legalOfMain: (name: string) => Set<string>
  t: (typeof copy)["zh"] | (typeof copy)["en"]
  maxOfMain: (h: string) => number
  maxOfSec: (h: string) => number
  updateSlot: (i: number, patch: Partial<Slot>) => void
}) {
  const legal = slot.mainHash ? legalOfMain(slot.mainHash) : new Set<string>()
  const secIllegal = slot.secHash !== "" && !legal.has(slot.secHash)
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
          traits={sigilNames}
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
          legal={legal}
          invalid={secIllegal}
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
