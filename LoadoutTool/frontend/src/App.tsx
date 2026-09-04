import { useEffect, useRef, useState } from "react"
import { MinusIcon, Plus } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import {
  InputGroup,
  InputGroupAddon,
  InputGroupInput,
} from "@/components/ui/input-group"
import { TraitPicker } from "./TraitPicker"
import { LoadTraits, LoadConfig, SaveLoadout, MinimiseApp } from "../bindings/loadouttool/loadoutservice"

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
  "grid grid-cols-[2.5rem_2.5rem_minmax(0,1fr)_minmax(0,1fr)_3.5rem] items-center gap-x-2"

const HEADER_ROW = `${GRID_COLS} border-b pb-2 text-sm font-medium text-foreground`
const DATA_ROW = `${GRID_COLS} border-b py-2 text-sm transition-colors last:border-b-0 hover:bg-muted/50`

export default function App() {
  const [traits, setTraits] = useState<Trait[]>([])
  const [slots, setSlots] = useState<Slot[]>([])
  const [status, setStatus] = useState("")
  // First render + first load must not write loadout.json: the preset stays
  // active until the user actually edits something.
  const skipSave = useRef(true)

  useEffect(() => {
    ;(async () => {
      try {
        const traitJson = await LoadTraits()
        setTraits(
          (JSON.parse(traitJson).traits as { nameZh: string; maxLevel?: number }[]).map(
            (t) => ({ nameZh: t.nameZh, maxLevel: t.maxLevel ?? 15 })
          )
        )
        const configJson = await LoadConfig()
        skipSave.current = true
        setSlots(JSON.parse(configJson).slots ?? [])
      } catch (e) {
        setStatus(`加载失败：${e}`)
      }
    })()
  }, [])

  // Fixed Escape minimises the tool to the taskbar (window stays alive). Esc
  // pressed inside an input group or the combobox popup is left to the
  // components themselves (clear/close popup).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== "Escape") return
      const target = e.target as HTMLElement | null
      if (target?.closest('[data-slot="input-group"], [data-slot="combobox-content"]')) return
      void MinimiseApp()
    }
    document.addEventListener("keydown", onKey)
    return () => document.removeEventListener("keydown", onKey)
  }, [])

  const maxOf = (name: string) => traits.find((t) => t.nameZh === name)?.maxLevel ?? 15
  const traitNames = traits.map((t) => t.nameZh)

  const updateSlot = (index: number, patch: Partial<Slot>) => {
    setSlots((prev) => prev.map((slot, i) => (i === index ? { ...slot, ...patch } : slot)))
  }

  const addSlot = () => {
    if (slots.length >= 24) return
    setSlots((prev) => [...prev, { trait1: "", level1: 15, trait2: "", level2: 15, enabled: true }])
  }

  const removeSlot = (index: number) => {
    setSlots((prev) => prev.filter((_, i) => i !== index))
  }

  const save = async () => {
    try {
      await SaveLoadout(JSON.stringify({ slots }, null, 2))
      setStatus(`已自动保存 ${slots.length} 槽`)
    } catch (e) {
      setStatus(`自动保存失败：${e}`)
    }
  }

  // Auto-save on every edit; the first load skips writing once.
  useEffect(() => {
    if (skipSave.current) {
      skipSave.current = false
      return
    }
    void save()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [slots])

  return (
    <div className="fixed inset-0 flex flex-col">
      <div className="min-h-0 flex-1 overflow-y-auto p-4 pb-2">
        <div className={HEADER_ROW}>
          <div />
          <div>#</div>
          <div>主因子</div>
          <div>副因子</div>
          <div>移除</div>
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
            <div className="flex min-w-0 items-center gap-1.5">
              <TraitPicker
                value={slot.trait1}
                traits={traitNames}
                placeholder="选择因子"
                onSelect={(v) => updateSlot(index, { trait1: v, level1: Math.min(15, maxOf(v)) })}
              />
              <InputGroup className="w-20 shrink-0">
                <InputGroupInput
                  type="number"
                  min={1}
                  max={maxOf(slot.trait1)}
                  value={slot.level1}
                  onChange={(e) => {
                    const n = Math.min(Number(e.target.value), maxOf(slot.trait1))
                    updateSlot(index, { level1: n })
                    if (e.target.value !== String(n)) e.target.value = String(n)
                  }}
                  className="py-0 pb-px text-center leading-[36px] [appearance:textfield] [&::-webkit-outer-spin-button]:appearance-none [&::-webkit-inner-spin-button]:appearance-none"
                />
                <InputGroupAddon align="inline-end" className="text-[#a0a0a0] tabular-nums">
                  / {maxOf(slot.trait1)}
                </InputGroupAddon>
              </InputGroup>
            </div>
            <div className="flex min-w-0 items-center gap-1.5">
              <TraitPicker
                value={slot.trait2}
                traits={traitNames}
                placeholder="无"
                noneOption
                onSelect={(v) => updateSlot(index, { trait2: v, level2: v ? Math.min(15, maxOf(v)) : 0 })}
              />
              <InputGroup className="w-20 shrink-0">
                <InputGroupInput
                  type="number"
                  min={1}
                  max={maxOf(slot.trait2)}
                  value={slot.level2}
                  onChange={(e) => {
                    const n = Math.min(Number(e.target.value), maxOf(slot.trait2))
                    updateSlot(index, { level2: n })
                    if (e.target.value !== String(n)) e.target.value = String(n)
                  }}
                  className="py-0 pb-px text-center leading-[36px] [appearance:textfield] [&::-webkit-outer-spin-button]:appearance-none [&::-webkit-inner-spin-button]:appearance-none"
                />
                <InputGroupAddon align="inline-end" className="text-[#a0a0a0] tabular-nums">
                  / {maxOf(slot.trait2)}
                </InputGroupAddon>
              </InputGroup>
            </div>
            <div>
              <Button variant="outline" size="icon" onClick={() => removeSlot(index)}>
                <MinusIcon />
              </Button>
            </div>
          </div>
        ))}
      </div>

      <div className="flex shrink-0 items-center gap-3 border-t bg-background p-4">
        <span className="truncate text-sm text-muted-foreground">{status}</span>
        <Button variant="outline" className="ml-auto" onClick={addSlot}>
          <Plus className="h-4 w-4" /> 添加槽位
        </Button>
      </div>
    </div>
  )
}
