# GBFR Extra Sigil Slots — Reloaded-II migration

This project is the ReShade-free frontend for the ER 2.0.2-2.0.5 configurable extra-sigil mod.

Runtime dependency boundary:

- Reloaded-II
- Reloaded II Shared Lib: Reloaded.Hooks (`reloaded.sharedlib.hooks`)
- `Reloaded.Imgui.Hook` with its Direct3D 11 implementation

It does not call or depend on ReShade or Luma. The verified ReShade add-on remains frozen only as a behavioral reference.

Runtime split:

- `GBFR.ExtraSigilSlots.Reloaded.dll`: Reloaded-II lifecycle and hotkey configuration, Direct3D 11 ImGui UI, and keyboard/mouse capture with controller pass-through.
- `GBFR.ExtraSigilSlots.Native.dll`: one-shot semantic layout resolution with exact local-byte preflight, SafetyHook detours, inventory snapshot, per-character selections and same-thread native status rebuild.

The native core exposes a fixed packed C ABI and never renders a UI. The managed frontend never reads or writes game memory itself.
