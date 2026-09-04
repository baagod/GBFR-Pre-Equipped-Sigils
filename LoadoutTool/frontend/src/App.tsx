import { useEffect, useMemo, useRef, useState } from "react"
import { MinusIcon, Plus } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import {
  InputGroup,
  InputGroupAddon,
  InputGroupInput,
} from "@/components/ui/input-group"
import { TraitPicker } from "./TraitPicker"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { LoadTraits, LoadConfig, SaveLoadout, MinimiseApp, GetHotkey } from "../bindings/loadouttool/loadoutservice"

const MAX_SLOTS = 22 // matches mod MaxSlots / native effective limit

interface Slot {
  trait1: string
  level1: number
  trait2: string
  level2: number
  enabled: boolean
}

interface Trait {
  nameZh: string
  maxLevel: number
}

/* Fixed side columns + factor columns that eat all remaining width. */
const GRID_COLS =
  "grid grid-cols-[2.5rem_2rem_minmax(0,1fr)_minmax(0,1fr)_4rem] items-center gap-x-2"

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
  return {
    trait1: typeof s.trait1 === "string" ? s.trait1 : "",
    level1: Number.isFinite(s.level1) ? (s.level1 as number) : 15,
    trait2: typeof s.trait2 === "string" ? s.trait2 : "",
    level2: Number.isFinite(s.level2) ? (s.level2 as number) : (s.trait2 ? 15 : 0),
    enabled: s.enabled !== false,
  }
}

export default function App() {
  const [traits, setTraits] = useState<Trait[]>([])
  const [slots, setSlots] = useState<Slot[]>([])
  const [status, setStatus] = useState("")
  const [hideKey, setHideKey] = useState(0x70) // F1 default (matches mod default)
  const [pendingRemove, setPendingRemove] = useState<number | null>(null)
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
          (JSON.parse(traitJson).traits as { nameZh: string; maxLevel?: number }[]).map(
            (t) => ({ nameZh: t.nameZh, maxLevel: t.maxLevel ?? 15 })
          )
        )
      } catch (e) {
        setStatus(`词条字典加载失败：${e}`)
      }
      try {
        const configJson = await LoadConfig()
        const parsed = JSON.parse(configJson)
        const rawSlots = Array.isArray(parsed?.slots) ? parsed.slots : []
        skipSave.current = true
        setSlots(rawSlots.map(normalizeSlot))
      } catch (e) {
        setStatus(`配装加载失败：${e}`)
      }
      try {
        setHideKey(await GetHotkey())
      } catch {
        // keep F1 default
      }
    })()
  }, [])

  const traitMax = useMemo(
    () => new Map(traits.map((t) => [t.nameZh, t.maxLevel] as const)),
    [traits]
  )
  const maxOf = (name: string) => traitMax.get(name) ?? 15
  const traitNames = traits.map((t) => t.nameZh)

  const updateSlot = (index: number, patch: Partial<Slot>) => {
    setSlots((prev) => prev.map((slot, i) => (i === index ? { ...slot, ...patch } : slot)))
  }

  const addSlot = () => {
    if (slots.length >= MAX_SLOTS) return
    setSlots((prev) => [...prev, { trait1: "", level1: 15, trait2: "", level2: 15, enabled: true }])
  }

  const removeSlot = (index: number) => {
    setSlots((prev) => prev.filter((_, i) => i !== index))
  }

  // Header check box: select all / clear all (official Table pattern).
  const allEnabled = slots.length > 0 && slots.every((s) => s.enabled)
  const toggleAll = () => {
    setSlots((prev) => prev.map((slot) => ({ ...slot, enabled: !allEnabled })))
  }

  const save = async () => {
    const unknown = slots.filter(
      (s) =>
        (s.trait1 !== "" && !traitMax.has(s.trait1)) ||
        (s.trait2 !== "" && !traitMax.has(s.trait2))
    )
    if (unknown.length > 0) {
      const names = unknown.map((s) => s.trait1 || s.trait2).join("、")
      setStatus(`存在字典外的词条（未保存）：${names}`)
      return
    }
    try {
      await SaveLoadout(JSON.stringify({ slots }, null, 2))
      setStatus(`已自动保存 ${slots.length} 槽`)
    } catch (e) {
      setStatus(`自动保存失败：${e}`)
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
      <div className="min-h-0 flex-1 overflow-y-auto p-4 pb-2">
        <div className={HEADER_ROW}>
          <div>
            <Checkbox checked={allEnabled} onCheckedChange={toggleAll} aria-label="全选/反选" />
          </div>
          <div>#</div>
          <div className="pr-2 pl-[11px]">主因子</div>
          <div className="pl-[21px]">副因子</div>
          <div className="text-center">操作</div>
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
                placeholder="选择因子"
                onSelect={(v) => updateSlot(index, { trait1: v, level1: Math.min(15, maxOf(v)) })}
              />
              <LevelInput
                value={slot.level1}
                max={maxOf(slot.trait1)}
                onLevel={(n) => updateSlot(index, { level1: n })}
              />
            </div>
            <div className="flex min-w-0 items-center gap-1.5 pl-2">
              <TraitPicker
                value={slot.trait2}
                traits={traitNames}
                placeholder="无"
                noneOption
                onSelect={(v) => updateSlot(index, { trait2: v, level2: v ? Math.min(15, maxOf(v)) : 0 })}
              />
              <LevelInput
                value={slot.level2}
                max={maxOf(slot.trait2)}
                min={slot.trait2 ? 1 : 0}
                onLevel={(n) => updateSlot(index, { level2: n })}
              />
            </div>
            <div className="flex justify-center">
              <Button variant="outline" size="icon" aria-label="移除" onClick={() => setPendingRemove(index)}>
                <MinusIcon />
              </Button>
            </div>
          </div>
        ))}
      </div>

      <AlertDialog
        open={pendingRemove !== null}
        onOpenChange={(open) => {
          if (!open) setPendingRemove(null)
        }}
      >
        <AlertDialogContent size="sm">
          <AlertDialogHeader>
            <AlertDialogTitle>移除槽位？</AlertDialogTitle>
            <AlertDialogDescription>
              将删除第 {(pendingRemove ?? 0) + 1} 行槽位。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel
              variant="outline"
              data-autofocus
              autoFocus
              className="[&:focus]:border-ring [&:focus]:ring-2 [&:focus]:ring-ring/50"
            >
              取消
            </AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              onClick={() => {
                if (pendingRemove !== null) removeSlot(pendingRemove)
                setPendingRemove(null)
              }}
            >
              移除
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <div className="flex shrink-0 items-center gap-3 border-t bg-background p-4">
        <span className="truncate text-sm text-muted-foreground">{status}</span>
        <Button
          variant="outline"
          className="ml-auto"
          onClick={addSlot}
          disabled={slots.length >= MAX_SLOTS}
        >
          <Plus /> 添加槽位
        </Button>
      </div>
    </div>
  )
}
