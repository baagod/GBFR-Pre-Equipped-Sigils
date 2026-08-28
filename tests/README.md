# Smoke-test harnesses

These command-line harnesses validate the native ABI and the pure managed
integration paths without starting the game. They load DLLs only from an
already-built repository output directory and use isolated directories under
the system temporary folder.

Build the product and run every harness from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1
$env:GBFR_EXE_204 = 'D:\granblue_fantasy_relink.exe'
$env:GBFR_EXE_205 = 'D:\GAME\steam\steamapps\common\Granblue Fantasy Relink\granblue_fantasy_relink.exe'
powershell -ExecutionPolicy Bypass -File .\tests\run-smoke-tests.ps1
```

Pass `-Configuration Debug` to the runner after building Debug. The harness
projects themselves build as Release utilities; generated `bin` and `obj`
directories are ignored by Git.

The harnesses cover:

- missing NumConfig creation, byte-for-byte preservation of valid files, exact backup plus default replacement for invalid settings/selections, pending 1–24 count-input normalization, transactional shrink/expand/no-op count changes, fail-closed in-memory handling before executable/layout verification, native startup-phase callbacks, and the absence of full-EXE hashing from the synchronous native startup path;
- packed ABI v13 sizes and transactional preset-reference updates;
- keyboard/mouse versus HID/controller Raw Input classification, device-specific Win32 capture policy, foreground Raw Input cleanup policy, Present-thread input-reset coalescing, plus two-phase input release while held keys or mouse buttons drain;
- event-driven frontend wake-up, key-repeat suppression, held-key latching, cancellation of an unconsumed background-open request, closed-frame sleeping, focus/capture mouse resynchronization, deterministic physical-button mask mapping, and first-interaction gating.
- Reloaded-II hotkey defaults, persistence, live updates, and invalid-value normalization.
- production semantic-layout resolution and exact-byte revalidation against section-mapped 2.0.4 and 2.0.5 executables when both `GBFR_EXE_204` and `GBFR_EXE_205` are set, including a mutated-hook-byte fail-closed check;
- deferred full-EXE SHA-256 correctness/non-blocking behavior, recognition of both verified 2.0.4 and 2.0.5 hashes, plus source classification for official Deploy ASI and Launcher injection without similarly named-module false positives.
- recoverable Overlay Broker leases, host-generation fencing, surviving-peer rebinding, and stale-writer rejection.

These harnesses do not execute the real ImGui Win32 backend or decode a real
`HRAWINPUT`. In particular, they do not prove the five synthetic mouse-button
release messages, cursor coordinate resynchronization, `DefWindowProc` cleanup,
Present/WndProc thread relationship, or replacement of one live cimgui context
with another after host recovery. Those paths still require in-game validation.
