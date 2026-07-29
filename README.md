# Ghost

Ghost is a Windows desktop assistant that teaches you how to use software by pointing at the
screen, one step at a time, and waiting for you at each step. Ghost points, you click, Ghost
re-reads the screen and points again.

Ghost never clicks anything for you. It never sends synthetic input. It is a pointer and a
teacher, not an agent.

## Why this exists / prior art

Every comparable tool screenshots the screen and asks a vision model where to click. Vision
grounding on real desktop software is unreliable: leading specialist models score under 20% on
ScreenSpot-Pro, the benchmark for high-resolution professional applications.

Ghost's primary source of truth is instead the **Windows UI Automation (UIA) accessibility
tree**, which the OS already maintains, and which returns every element with an exact bounding
rectangle, a control type, and a human-readable name. Vision is a fallback tier for applications
that expose no usable tree, not the primary path.

This concept isn't novel — Ghost builds on and credits
[Clicky](https://github.com/farzaa/clicky) (the original, macOS/Swift) and its Windows port,
[clicky_windows](https://github.com/emreyilmaz46/clicky_windows), along with
[clicky-win](https://github.com/JaySmith502/clicky-win) and Microsoft's
[UFO](https://github.com/microsoft/UFO), whose hybrid UIA + vision pipeline Ghost is a
single-purpose version of. Ghost's accessibility-first resolution and genuine multi-step guidance
(click detection, settle detection, recovery) are what distinguish it from prior art.

**Headline metric:** percentage of steps resolved in the deterministic tier with zero LLM calls,
and p95 latency on that path. Eval numbers land here as later phases complete.

## Status

Phase 1 — UIA screen reader (`UiaThread`, `UiaScreenReader`, `SnapshotCache`) and the
deterministic resolver (`Scoring`, `DeterministicResolver`) are in. `Ghost.Eval capture` reads a
live foreground window; `Ghost.Eval run` replays fixtures through the real Tier 1 resolver. See
`Ghost.sln` build phases in the project spec for what's next (Phase 2: performance tuning and
pre-warming).

## Repository layout

```
src/Ghost.Core/    class library, no UI, no WPF reference — testable headlessly
src/Ghost.App/      WPF overlay + hotkey + planner/engine wiring (Phase 4+)
src/Ghost.Eval/     console app: `capture` and `run` verbs for the offline eval suite
tests/Ghost.Core.Tests/
fixtures/           committed eval suite, replayed offline
docs/coordinate-system.md
```

## Building

Requires the .NET 10 SDK and Windows (Ghost.Core targets `net10.0-windows` because later phases
need Windows-only UIA and interop APIs; WPF/UI itself doesn't start until Phase 4).

```
dotnet build
dotnet test
dotnet run --project src/Ghost.Eval -- run
```

Install [FlaUInspect](https://github.com/FlaUI/FlaUInspect) separately if you want to inspect a
live application's UIA element tree while developing the resolver — it isn't a project
dependency.

## Configuration

Copy `config.example.json` to `%APPDATA%\Ghost\config.json` and fill in `apiKey`, or set the
`ANTHROPIC_API_KEY` environment variable. `config.json` is gitignored; never commit an API key.

## Known limitations

- **UIPI**: a non-elevated Ghost process cannot read the accessibility tree of an elevated
  window. Ghost will be blind inside Task Manager, elevated PowerShell, and UAC dialogs.
- **Chromium lazy accessibility**: Chrome and Electron apps build their tree only when a client
  asks; Ghost pre-warms on foreground-window change to hide this latency. Some Electron apps need
  `--force-renderer-accessibility`.
- **Canvas-rendered apps** (Figma, games, some Java apps) expose no meaningful tree; Ghost says
  "I can't read this app" until the Phase 6 vision fallback lands.
- `RuntimeId` is not stable across snapshots and is never persisted or compared across captures.

## Privacy

Ghost reads on-screen UI text via the accessibility tree to figure out where to point, but never
records the screen, uploads telemetry, or syncs to a cloud account. Element `Value` contents
(which can include text the user typed, including passwords in some applications) are never
logged at `Information` level or above — only their length.
