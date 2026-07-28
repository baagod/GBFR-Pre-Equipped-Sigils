# GBFR Overlay Broker skeleton

This directory contains the process-local runtime shared by compatible GBFR overlays.

## Files

- `OverlayBrokerElection.cs` — elects the first-loaded compatible mod as the bootstrap carrier.
- `OverlayBrokerHost.cs` — owns the only ImGui frame, Present hook, WndProc, cursor and native-input transition path.
- `SharedImguiGraphicsBinding.cs` — publishes the carrier's exact cimgui module and context.
- `OverlayWindowInputClassifier.cs` — applies the aggregate keyboard, mouse and text capture policy.

The public peer API and the neutral peer registry live in the sibling
`GBFR.OverlayHub.Contracts` project.

## Invariants

1. Reloaded-II publishes only `IGbfrOverlayHub`. The host-control capability is never published.
2. Both Chat and Extra Sigil are ordinary peers, including the bootstrap carrier's business frontend.
3. Exactly one Broker host owns Present, WndProc and native keyboard/mouse interception.
4. A peer exception disables only that peer. A graphics-writer failure disables the Broker fail-closed.
5. Controller/HID input is never included in the Broker capture mask.
6. The shared Contract and runtime source files must remain byte-identical in both repositories.
