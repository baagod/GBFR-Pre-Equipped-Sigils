import { useMemo } from "react"
import { Checkbox } from "@/components/ui/checkbox"
import type { Exclusive, ExclusiveState, Sigil } from "./model"
import type { Lang } from "./copy"

export function ExclusivePanel({
  table,
  state,
  sigilByHash,
  lang,
  onChange,
}: {
  table: Exclusive[]
  state: ExclusiveState | undefined
  sigilByHash: Map<string, Sigil>
  lang: Lang
  onChange: (player: string, row: Exclusive, traitHash: string, value: boolean) => void
}) {
  // One row per shared player code (Gran/Djeeta both PL0000 with the same
  // exclusives): the toggle is linked for both in-game characters.
  const rows = useMemo(() => {
    const seen = new Set<string>()
    return table.filter((e) => {
      if (seen.has(e.player)) return false
      seen.add(e.player)
      return true
    })
  }, [table])
  // Display name per player code (Gran/Djeeta share PL0000 and merge into one
  // row); built once instead of filtering the table for every row.
  const nameByPlayer = useMemo(() => {
    const byPlayer = new Map<string, string[]>()
    for (const entry of table) {
      const names = byPlayer.get(entry.player) ?? []
      names.push((lang === "zh" ? entry.zh || entry.name : entry.name || entry.zh) ?? "")
      byPlayer.set(entry.player, names)
    }
    return byPlayer
  }, [table, lang])
  if (table.length === 0) return null
  const gemName = (gem: string) => {
    const s = sigilByHash.get(gem)
    if (!s) return gem
    return lang === "zh" ? s.zh || s.name || gem : s.name || s.zh || gem
  }
  return (
    <div>
      {rows.map((e) => {
          const st = state?.[e.player]
          const row = [
            { key: e.t1, on: st?.[e.t1] ?? true, gem: e.t1Gem },
            { key: e.t2, on: st?.[e.t2] ?? true, gem: e.t2Gem },
            { key: e.war, on: st?.[e.war] ?? true, gem: e.warGem },
          ]
          const characterName = nameByPlayer.get(e.player)?.join(" / ") || e.hash
          return (
          <div
            key={e.hash}
            className="flex h-[42px] items-center border-b text-sm last:border-b-0"
          >
            <div className="grid w-full grid-cols-[7rem_1fr_1fr_1fr] items-center gap-x-2">
              <span className="truncate font-medium">{characterName}</span>
              {row.map((r) => (
                <label key={r.key} className="flex min-w-0 items-center gap-1.5">
                  <Checkbox
                    checked={r.on}
                    onCheckedChange={(v) => onChange(e.player, e, r.key, v === true)}
                  />
                  <span className="truncate">{gemName(r.gem)}</span>
                </label>
              ))}
            </div>
          </div>
        )
      })}
    </div>
  )
}
