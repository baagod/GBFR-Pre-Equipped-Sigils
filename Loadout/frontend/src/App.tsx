import { useCallback, useEffect, useMemo, useRef, useState } from "react"
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
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { LoadSigils, LoadConfig, SaveLoadout, MinimiseApp, GetHotkey, LoadExclusives } from "../bindings/loadouttool/loadoutservice"
import { copy, type Lang } from "./copy"
import { configToSlots, pad12, type Exclusive, type ExclusiveState, type Sigil, type Slot, type Trait } from "./model"
import { SlotRow, HEADER_ROW } from "./SlotEditor"
import { ExclusivePanel } from "./ExclusivePanel"

/** Quest-locked families are matched by zh (crab 钳蟹系 / 相扑斗力); data-driven. */
const isSpecialZh = (zh: string) => zh.includes("钳蟹") || zh.includes("相扑斗力")

export default function App() {
  const [traits, setTraits] = useState<Trait[]>([])
  const [sigils, setSigils] = useState<Sigil[]>([])
  const [slots, setSlots] = useState<Slot[]>([])
  const [status, setStatus] = useState("")
  const [tab, setTab] = useState<"general" | "exclusive">("general")
  const [exclusiveTable, setExclusiveTable] = useState<Exclusive[]>([])
  const [exclusiveState, setExclusiveState] = useState<ExclusiveState | undefined>(undefined)
  const [resetOpen, setResetOpen] = useState(false)
  const [hideKey, setHideKey] = useState(0x70) // F1 default (matches mod default)
  const [lang, setLang] = useState<Lang>("zh") // default zh; persisted in loadout.json
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
      // Merged single table: item rows (hash != skill1) + non-item skill rows.
      let sigilsLoaded: Sigil[] = []
      try {
        const sigilJson = await LoadSigils()
        const rows = (JSON.parse(sigilJson).sigils as Partial<Sigil>[]) ?? []
        // Trait list = one row per trait hash (first row wins: the item named
        // after the trait); EN label uses the item EN name when present.
        const traitById = new Map<string, Trait>()
        for (const s of rows) {
          if (!s.skill1 || traitById.has(s.skill1)) continue
          if (s.player) continue // exclusive-slot sigils: traits never offered
          traitById.set(s.skill1, {
            hash: s.skill1,
            zh: s.zh ?? "",
            en: s.name || s.zh || "",
            cap: s.cap ?? 15,
            nonItem: s.hash === s.skill1,
          })
        }
        setTraits([...traitById.values()])
        // Item rows only (hash != skill1): non-item skill rows stay in the
        // trait list above but never appear as pickable sigils.
        sigilsLoaded = rows
          .filter((s) => s.hash !== s.skill1)
          .map((s) => ({
            hash: s.hash ?? "",
            name: s.name ?? s.zh ?? s.hash ?? "",
            zh: s.zh ?? "",
            skill1: s.skill1 ?? "",
            category: s.category ?? "",
            player: s.player ?? "",
            special: s.special === true,
            lot: s.lot?.length ? s.lot : undefined,
            sec: s.sec || undefined,
          }))
        setSigils(sigilsLoaded)
      } catch (e) {
        setStatus(t.sigilFail(e))
      }
      try {
        const configJson = await LoadConfig()
        applyConfig(JSON.parse(configJson), sigilsLoaded)
      } catch (e) {
        setStatus(t.configFail(e))
      }
      try {
        const exclusiveJson = await LoadExclusives()
        const table = (JSON.parse(exclusiveJson).exclusives ?? []) as Exclusive[]
        setExclusiveTable(table)
        // Migrate legacy exclusive entries (character-hash keys + t1/t2/war)
        // to the player-keyed shape; otherwise they render as all-enabled and
        // the first toggle would silently re-enable the disabled factors.
        setExclusiveState((prev) => {
          if (!prev) return prev
          const byHash = new Map(table.map((e) => [e.hash, e]))
          const out: ExclusiveState = {}
          let migrated = false
          for (const [key, entry] of Object.entries(prev)) {
            const row = byHash.get(key)
            if (!row) {
              out[key] = entry
              continue
            }
            const merged = { ...(out[row.player] ?? {}) }
            const legacy = entry as Record<string, unknown>
            merged[row.t1] = legacy.t1 !== false
            merged[row.t2] = legacy.t2 !== false
            merged[row.war] = legacy.war !== false
            out[row.player] = merged
            migrated = true
          }
          return migrated ? out : prev
        })
      } catch (e) {
        setStatus(t.exclFail(e))
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
  const sigilByHash = useMemo(() => new Map(sigils.map((s) => [s.hash, s])), [sigils])

  // Variants grouped by display name: one entry per name (unique picker item).
  const groupedByName = useMemo(() => {
    const byName = new Map<string, Sigil[]>()
    for (const s of sigils) {
      const g = byName.get(s.name)
      if (g) g.push(s)
      else byName.set(s.name, [s])
    }
    return byName
  }, [sigils])

  // General mains: only item rows (hash != skill1) with no character
  // exclusivity (player == "") qualify. Special rows (crab family etc.) stay
  // selectable as mains — their secondary combination is hinted as illegal
  // via specialMainNames.
  const sigilGroups = useMemo(
    () =>
      [...groupedByName.entries()]
        .filter(([, variants]) => variants.some((v) => v.player === ""))
        .map(([name, variants]) => ({
          name,
          zh: variants[0]?.zh ?? name,
        })),
    [groupedByName]
  )

  // Special mains (crab family / 相扑斗力 / special rows) cannot legally take
  // any secondary: the secondary list is shown fully as illegal (existing
  // grey/red styles) — choosing and saving are not blocked, nothing is cleared.
  const specialMainNames = useMemo(() => {
    const names = new Set<string>()
    for (const [name, variants] of groupedByName) {
      if (variants.some((v) => v.special || isSpecialZh(v.zh)))
        names.add(name)
    }
    return names
  }, [groupedByName])

  const traitHashes = useMemo(() => traits.map((tr) => tr.hash), [traits])

  // Exclusive-slot / quest-locked traits (crab family, 相扑斗力 etc.): these can
  // never appear as a secondary for a regular main. Everything else is legal —
  // game synthesis (2.0.5) freely combines same/cross-category and even
  // duplicate traits. Non-item skill rows (hash == skill1) are also excluded.
  const exclusiveTraits = useMemo(() => {
    const exclusive = new Set<string>()
    for (const s of sigils) {
      if (s.special) exclusive.add(s.skill1)
      if (isSpecialZh(s.zh)) exclusive.add(s.skill1)
    }
    for (const t of traits) if (t.nonItem) exclusive.add(t.hash)
    return exclusive
  }, [sigils, traits])

  // Pool families: the pool version (lot != []) plus its legal secondary set
  // (lot ∪ fixed-second). Derived once, read by both the legality hint below
  // and hashFor at save time, so the two can never drift apart.
  const poolByMain = useMemo(() => {
    const byName = new Map<string, { poolHash: string; lot: Set<string>; legal: Set<string> }>()
    for (const [name, variants] of groupedByName) {
      const pool = variants.find((v) => v.lot && v.lot.length > 0)
      if (!pool) continue
      const legal = new Set<string>()
      for (const v of variants) {
        for (const h of v.lot ?? []) legal.add(h)
        if (v.sec) legal.add(v.sec)
      }
      byName.set(name, { poolHash: pool.hash, lot: new Set(pool.lot), legal })
    }
    return byName
  }, [groupedByName])

  // Hint only; generation is never blocked. Legal secondaries for a main:
  //   - special mains (one == special): empty set -> everything dimmed
  //   - pool families (lot != []): legal = lot ∪ fixed-second (sec) of the
  //     family; anything outside is shown grey/red but still saved
  //   - everything else: all non-exclusive traits (free combination)
  const legalByMain = useMemo(() => {
    const legal = new Set(traitHashes.filter((h) => !exclusiveTraits.has(h)))
    const none: Set<string> = new Set()
    return (name: string) => {
      if (specialMainNames.has(name)) return none
      return poolByMain.get(name)?.legal ?? legal
    }
  }, [traitHashes, exclusiveTraits, specialMainNames, poolByMain])

  // Family item hash at save time: no secondary -> pool version (lot != []
  // variant, else first); secondary in the pool's lot -> pool version;
  // secondary equal to a variant's fixed second (sec) -> that fixed version;
  // anything else (illegal) still generates with the pool version (styles
  // only). lot match wins over sec match (currently no trait is in both).
  const hashFor = (name: string, secHash = ""): string => {
    const variants = groupedByName.get(name)
    if (!variants || variants.length === 0) return ""
    const pool = poolByMain.get(name)
    if (pool && (secHash === "" || pool.lot.has(secHash))) return pool.poolHash
    const fixed = secHash !== "" ? variants.find((v) => v.sec === secHash) : undefined
    return fixed?.hash ?? pool?.poolHash ?? variants[0].hash
  }

  // Stable callbacks + memoized arrays keep SlotRow memoization effective:
  // editing one row no longer re-renders all 12.
  const maxOfMain = useCallback((name: string) => {
    const variants = groupedByName.get(name)
    const tr = variants && variants.length > 0 ? traitByName.get(variants[0].skill1) : undefined
    return tr?.cap ?? 15
  }, [groupedByName, traitByName])
  const maxOfSec = useCallback((h: string) => traitByName.get(h)?.cap ?? 15, [traitByName])

  // Picker item values: main = unique display names; secondary = trait hashes
  // (labels provided by hashLabels).
  const sigilNames = useMemo(() => sigilGroups.map((g) => g.name), [sigilGroups])
  const hashLabels = useMemo(
    () =>
      Object.fromEntries([
        ...traits.map((tr) => [tr.hash, lang === "zh" ? tr.zh : tr.en] as const),
        ...sigilGroups.map((g) => [g.name, lang === "zh" ? g.zh : g.name] as const),
      ]),
    [traits, sigilGroups, lang]
  )
  const updateSlot = useCallback((index: number, patch: Partial<Slot>) => {
    setSlots((prev) => prev.map((slot, i) => (i === index ? { ...slot, ...patch } : slot)))
  }, [])

  // Reload the player config (falls back to the preset template). Used after
  // a reset so the UI mirrors the fresh state without restarting the app.
  const reloadConfig = async () => {
    try {
      const configJson = await LoadConfig()
      applyConfig(JSON.parse(configJson), sigils)
    } catch (e) {
      setStatus(t.configFail(e))
    }
  }

  /** Load a saved config into the editor state (first render skips saving). */
  const applyConfig = (parsed: unknown, sigilTable: Sigil[]) => {
    const cfg = (parsed ?? {}) as {
      lang?: unknown
      slots?: unknown
      exclusive?: unknown
    }
    skipSave.current = true
    if (typeof cfg.lang === "string") setLang(cfg.lang as Lang)
    setSlots(pad12(configToSlots(cfg, sigilTable)))
    setExclusiveState(
      cfg.exclusive && typeof cfg.exclusive === "object"
        ? (cfg.exclusive as ExclusiveState)
        : undefined
    )
  }

  // Header check box: select all / clear all (official Table pattern).
  // slots is always padded to MAX_SLOTS, so the length check is unnecessary.
  const allEnabled = slots.every((s) => s.enabled)
  const toggleAll = () => {
    setSlots((prev) => prev.map((slot) => ({ ...slot, enabled: !allEnabled })))
  }

  const save = async () => {
    if (sigils.length === 0 || traits.length === 0) {
      setStatus(t.tablesNotReady)
      return
    }
    const filled = slots.filter((s) => s.mainHash !== "")
    const cfg = filled.map((s) => {
      const hash = hashFor(s.mainHash, s.secHash)
      const main = {
        gem: hash, // loadout.json protocol: item id stays "gem" (mod reads it)
        level: s.mainLevel,
        zh: sigilByHash.get(hash)?.zh ?? "",
        en: sigilByHash.get(hash)?.name ?? "",
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
      const exclusive =
        exclusiveState && Object.keys(exclusiveState).length > 0
          ? exclusiveState
          : undefined
      await SaveLoadout(JSON.stringify({ lang, slots: cfg, exclusive }, null, 2))
    } catch (e) {
      setStatus(t.saveFail(e))
    }
  }

  // Exclusive toggle update (per character, per trait hash; a character's
  // first toggle writes all three factors so the file always shows the state).
  const updateExclusive = (
    player: string,
    row: Exclusive,
    traitHash: string,
    value: boolean
  ) => {
    setExclusiveState((prev) => {
      const current: ExclusiveState = prev ? { ...prev } : {}
      const entry = current[player]
        ? { ...current[player] }
        : { [row.t1]: true, [row.t2]: true, [row.war]: true }
      entry[traitHash] = value
      current[player] = entry
      return current
    })
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
  }, [slots, lang, exclusiveState])

  // The hide key is the SAME key as the mod's menu hotkey (default F1,
  // configurable; the mod publishes it in tool-hotkey.txt). Pressed here it
  // minimises the window; pressed in the game it brings the tool back. The
  // hide is deferred until AFTER the key-up so the press is fully consumed
  // here and never leaks to the game window. Escape also hides the window,
  // EXCEPT when an overlay (combobox list / dialog) has the focus: there it
  // belongs to the overlay and only closes it. The overlay check runs on
  // keydown (Base UI unmounts the popup during keydown, so keyup can no
  // longer see it) and is remembered for the keyup decision.
  useEffect(() => {
    let timer: ReturnType<typeof setTimeout> | undefined
    let overlayEscOnKeyDown = false
    const hideKeyPressed = (e: KeyboardEvent) => (e.keyCode || e.which) === hideKey
    const isInOverlay = (e: KeyboardEvent) =>
      !!(e.target as HTMLElement | null)?.closest?.(
        '[data-slot="combobox-content"], [role="dialog"]'
      )
    const onKeyDown = (e: KeyboardEvent) => {
      if (!hideKeyPressed(e) && e.key !== "Escape") return
      if (e.key === "Escape") {
        if (isInOverlay(e)) {
          overlayEscOnKeyDown = true
          return
        }
      }
      clearTimeout(timer)
      e.preventDefault()
    }
    const onKeyUp = (e: KeyboardEvent) => {
      if (!hideKeyPressed(e) && e.key !== "Escape") return
      if (e.key === "Escape") {
        if (overlayEscOnKeyDown) {
          overlayEscOnKeyDown = false
          return
        }
      }
      clearTimeout(timer)
      timer = setTimeout(() => void MinimiseApp(), 150)
    }
    // Capture phase: Base UI unmounts the popup while React processes the
    // keydown, so a bubble-phase listener would see a detached target and
    // wrongly hide the window. In the capture phase the popup is still live.
    document.addEventListener("keydown", onKeyDown, true)
    document.addEventListener("keyup", onKeyUp, true)
    return () => {
      document.removeEventListener("keydown", onKeyDown, true)
      document.removeEventListener("keyup", onKeyUp, true)
      clearTimeout(timer)
    }
  }, [hideKey])

  return (
    <div className="fixed inset-0 flex flex-col">
      <Tabs
        value={tab}
        onValueChange={(v) => setTab(v as "general" | "exclusive")}
        className="shrink-0 h-[60px] justify-center border-b bg-background px-4"
      >
        <div className="flex items-center justify-between gap-3">
          <TabsList>
            <TabsTrigger value="general">{t.tabGeneral}</TabsTrigger>
            <TabsTrigger value="exclusive">{t.tabExclusive}</TabsTrigger>
          </TabsList>
          <div className="flex items-center">
            <AlertDialog open={resetOpen} onOpenChange={setResetOpen}>
              <AlertDialogTrigger
                render={
                  <Button variant="ghost" size="sm" aria-label={t.reset}>
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
                      // Reset = empty configuration; lang survives (it is a
                      // tool-side setting, not part of the mod config).
                      void SaveLoadout(JSON.stringify({ lang, slots: [] }, null, 2))
                        .then(() => reloadConfig())
                        .catch((e) => setStatus(t.saveFail(e)))
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
              className="ml-1"
              onClick={toggleLang}
              aria-label="Switch language"
            >
              {lang === "zh" ? "EN" : "中"}
            </Button>
          </div>
        </div>
      </Tabs>
      <div className="min-h-0 flex-1 overflow-y-auto px-4 [scrollbar-gutter:stable]">
        {status && (
          <div className="mb-2 rounded-md bg-muted/50 px-3 py-1.5 text-sm text-muted-foreground">
            {status}
          </div>
        )}
        {tab === "general" ? (
          <>
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
          </>
        ) : (
          <ExclusivePanel
            table={exclusiveTable}
            state={exclusiveState}
            sigilByHash={sigilByHash}
            lang={lang}
            onChange={updateExclusive}
          />
        )}
      </div>
    </div>
  )
}


