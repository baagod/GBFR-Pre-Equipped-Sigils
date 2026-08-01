# Smoke-test harnesses

These command-line harnesses validate the native ABI and the pure managed
integration paths without starting the game. They load DLLs only from an
already-built repository output directory and use isolated directories under
the system temporary folder.

Build the product and run every harness from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1
powershell -ExecutionPolicy Bypass -File .\tests\run-smoke-tests.ps1
```

Pass `-Configuration Debug` to the runner after building Debug. The harness
projects themselves build as Release utilities; generated `bin` and `obj`
directories are ignored by Git.

The harnesses cover:

- missing NumConfig creation, byte-for-byte preservation of valid files, exact backup plus default replacement for invalid settings/selections, fail-closed in-memory handling before executable/layout verification, native startup-phase callbacks, and the absence of full-EXE hashing from the synchronous native startup path;
- packed ABI v11 sizes and transactional preset-reference updates;
- keyboard/mouse versus HID/controller Raw Input classification.
- event-driven frontend wake-up, key-repeat suppression, and closed-frame sleeping.
- Reloaded-II hotkey defaults, persistence, live updates, and invalid-value normalization.
- deferred full-EXE SHA-256 correctness/non-blocking behavior, plus source classification for official Deploy ASI and Launcher injection without similarly named-module false positives.
- recoverable Overlay Broker leases, host-generation fencing, surviving-peer rebinding, and stale-writer rejection.
