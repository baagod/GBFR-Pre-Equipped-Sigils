# GBFR Overlay Broker skeleton

This directory contains the process-local runtime shared by compatible GBFR overlays.

## Files

- `OverlayBrokerElection.cs` — elects the first-loaded compatible mod as the bootstrap carrier.
- `OverlayBrokerHost.cs` — owns the only ImGui frame, Present hook, WndProc, cursor and native-input transition path.
- `SharedImguiGraphicsBinding.cs` — publishes the carrier's exact cimgui module and context.
- `OverlayWindowInputClassifier.cs` — applies the aggregate keyboard, mouse and text capture policy.
- `ImGuiInputResetGate.cs` — transfers coalesced input reset requests to the Present thread.

The public peer API and the neutral peer registry live in the sibling
`GBFR.OverlayHub.Contracts` project.

## Invariants

1. Reloaded-II publishes only `IGbfrOverlayHub`. A generation-fenced host lease is handed out only through the optional recovery capability.
2. Both Chat and Extra Sigil are ordinary peers, including the bootstrap carrier's business frontend.
3. Exactly one Broker host owns Present, WndProc and native keyboard/mouse interception.
4. A peer exception disables only that peer. A graphics-writer failure releases its lease fail-closed; one surviving peer may acquire the next generation and rebind existing registrations.
5. Controller/HID input is never included in the Broker capture mask.
6. The shared Contract and runtime source files must remain byte-identical in both repositories.
7. Callers retain a strong reference to each registered client until its registration is disposed; the Broker stores clients weakly so abandoned peers cannot be kept alive forever.

Before publishing either repository, run `VerifyOverlayBrokerSync.ps1 -OtherRepository <path>` against the sibling overlay repository. The command fails if a required shared source file is missing or has a different SHA-256 hash.
