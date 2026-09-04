import { Button } from "@/components/ui/button"
import {
  Combobox,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
  ComboboxTrigger,
  ComboboxValue,
} from "@/components/ui/combobox"
import { ChevronDown } from "lucide-react"

interface TraitItem {
  value: string
  label: string
}

interface TraitPickerProps {
  value: string
  traits: string[]
  /** Text shown on the trigger when value is empty ("选择因子" / "不选择"). */
  placeholder?: string
  /** Prepend a "不选择" (empty value) option - used for the second trait. */
  noneOption?: boolean
  onSelect: (value: string) => void
}

export function TraitPicker({
  value,
  traits,
  placeholder = "选择因子",
  noneOption = false,
  onSelect,
}: TraitPickerProps) {
  const items: TraitItem[] = noneOption
    ? [{ value: "", label: "不选择" }, ...traits.map((trait) => ({ value: trait, label: trait }))]
    : traits.map((trait) => ({ value: trait, label: trait }))

  const selected: TraitItem =
    items.find((item) => item.value === value) ??
    (noneOption ? { value: "", label: "不选择" } : { value: "", label: placeholder })

  return (
    <Combobox
      items={items}
      value={selected}
      autoHighlight={true}
      onValueChange={(item) => {
        if (item) onSelect(item.value)
      }}
    >
      <ComboboxTrigger
        className="[&_[data-slot=combobox-trigger-icon]]:hidden"
        render={
          <Button variant="outline" className="min-w-0 flex-1 justify-between font-normal">
            <ComboboxValue />
            <ChevronDown className="size-4 text-muted-foreground" />
          </Button>
        }
      />
      <ComboboxContent>
        <ComboboxInput showTrigger={false} placeholder="搜索" />
        <ComboboxEmpty>无匹配因子</ComboboxEmpty>
        <ComboboxList className="max-h-[264px]">
          {(item) => (
            <ComboboxItem key={item.value} value={item}>
              {item.label}
            </ComboboxItem>
          )}
        </ComboboxList>
      </ComboboxContent>
    </Combobox>
  )
}
