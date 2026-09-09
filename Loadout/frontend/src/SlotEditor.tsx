import { memo, useEffect, useRef } from "react"
import {
  InputGroup,
  InputGroupAddon,
  InputGroupInput,
} from "@/components/ui/input-group"
import { Checkbox } from "@/components/ui/checkbox"
import { TraitPicker } from "./TraitPicker"
import type { Slot } from "./model"
import type { T } from "./copy"

/* Fixed side columns + factor columns that eat all remaining width. */
const GRID_COLS =
  "grid grid-cols-[2.5rem_2rem_minmax(0,1fr)_minmax(0,1fr)] items-center gap-x-2"

export const HEADER_ROW = `${GRID_COLS} mt-2 min-h-[44px] border-b text-sm font-medium text-foreground`
const DATA_ROW = `${GRID_COLS} border-b py-2 text-sm transition-colors last:border-b-0 hover:bg-muted/50`

/** Clamped numeric level input with a grey "/ max" suffix. */
function LevelInput({
  value,
  max,
  min = 1,
  onLevel,
  disabled,
}: {
  value: number
  max: number
  min?: number
  disabled?: boolean
  onLevel: (n: number) => void
}) {
  const groupRef = useRef<HTMLDivElement | null>(null)
  const inputRef = useRef<HTMLInputElement | null>(null)

  // Wheel adjusts the level over the WHOLE input group (suffix "/ max"
  // included), but only while the number input is focused — otherwise the
  // wheel is left alone and scrolls the page. Native listener with
  // passive: false so preventDefault can suppress the scroll (React's
  // synthetic onWheel is passive and cannot be prevented).
  useEffect(() => {
    const el = groupRef.current
    const input = inputRef.current
    if (!el || disabled) return
    const onWheel = (e: WheelEvent) => {
      if (input === null || document.activeElement !== input) return
      e.preventDefault()
      const step = e.deltaY < 0 ? 1 : -1
      onLevel(Math.max(min, Math.min(max, value + step)))
    }
    el.addEventListener("wheel", onWheel, { passive: false })
    return () => el.removeEventListener("wheel", onWheel)
  }, [disabled, min, max, value, onLevel])

  return (
    <InputGroup ref={groupRef} className="w-20 shrink-0">
      <InputGroupInput
        ref={inputRef}
        type="number"
        min={min}
        max={max}
        value={value}
        disabled={disabled}
        onChange={(e) => {
          const raw = Number(e.target.value)
          const n = Number.isFinite(raw)
            ? Math.max(min, Math.floor(Math.min(raw, max)))
            : min
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

export const SlotRow = memo(function SlotRow({
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
  t: T
  maxOfMain: (h: string) => number
  maxOfSec: (h: string) => number
  updateSlot: (i: number, patch: Partial<Slot>) => void
}) {
  const mainValid = slot.mainHash !== "" && sigilNames.includes(slot.mainHash)
  const legal = mainValid ? legalOfMain(slot.mainHash) : new Set<string>()
  const secIllegal = mainValid && slot.secHash !== "" && !legal.has(slot.secHash)
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
          disabled={!slot.mainHash}
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
          disabled={!mainValid}
          onSelect={(v) => updateSlot(index, { secHash: v, secLevel: v ? Math.min(15, maxOfSec(v)) : 0 })}
        />
        <LevelInput
          value={slot.secHash ? slot.secLevel : 0}
          max={maxOfSec(slot.secHash)}
          min={slot.secHash ? 1 : 0}
          disabled={!slot.secHash || !mainValid}
          onLevel={(n) => updateSlot(index, { secLevel: n })}
        />
      </div>
    </div>
  )
})
