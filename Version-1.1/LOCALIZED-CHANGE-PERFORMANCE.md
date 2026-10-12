# Localized navigation-update batching (Timberborn 1.1)

## Background

BFR collects terrain/road navmesh endpoints in `_pendingUpdateCoordinates` and previously ran a single `ProcessLocalizedChange` over their combined XY bounding rectangle (+1-cell padding). Far-apart changes could scan an enormous mostly unchanged area.

This ports the **BFR-only** optimization from [TB Optimizer](https://github.com/tharbad08/TimberbornMods/blob/TB-Optimizer/BenchmarkAndOptimizerV11Port/Optimizations/Mods/BFR/BfrLocalizedChangeOptimizerPatcher.cs) directly into native BFR code instead of adding a reflection/Harmony wrapper.

## Source map

- `Source/BFRManager.WaterUpdates.cs`: dispatch copied pending coordinates into `ProcessLocalizedChangesBatched`, with original pending-set clearing and 500-shoreline-per-tick checks unchanged.
- `Source/BFRManager.LocalizedBatching.cs`: group by 8x8 XY buckets (floor division handles negative coordinates), estimate padded scan area and call the existing `ProcessLocalizedChange` for each bucket only when the estimate is **strictly less than 75%** of the original.
- `Source/BFRManager.DynamicUpdates.cs`: untouched native four-phase scanning, validation, deletion and addition implementation.

Fallback to the original single-rectangle path for fewer than two coordinates, area smaller than 144 XY tiles, a single bucket, or insufficient estimated savings. No changes to water depth/contamination, navmesh edge ownership, first-run skip, global tick scheduling, UI, config or the 1.0 version.

## Risks and limitations

- Bucketed scans still iterate/reconcile all known shorelines per bucket. Many sparse buckets can incur overhead; the area threshold is only a heuristic.
- Processing split regions need not exactly match *incidental* rescanning inside the original vast bounding rectangle. Validate changes near bucket borders, multi-level water, levees, platforms and dynamite.
- This assumes each reported NavMeshChange has meaningful local endpoints. Revisit if the game's event semantics change.
- No rollback/replay on partial exceptions: edge removals/additions are not guaranteed idempotent.
- If both mods are installed, **disable TB Optimizer's BFR checkbox** to avoid redundant interception.

## Validation

Offline algorithm checks (10,000 randomized coordinate batches) covered grouping, negative coordinates, one-cell padding and threshold decisions; they do not prove in-game correctness. Source-only PR: compilation and runtime checks remain outstanding.

Before merging, compile with the Timberborn 1.1 managed DLLs using BFR's existing build process (do not touch deploy folders manually). A/B compare one edit, dense and distant edits, bucket borders, directional dynamite, levees/platforms, clean/deep/badwater and stacked columns. Exercise load/save and weather events. Inspect logs and compare **both FPS and actual game speed** at paused/x1/x3 under matched conditions. Isolate with BFR-only and TB Optimizer BFR disabled.

Historical TB Optimizer diagnostics reported some localized rescan samples around 180-237 ms before vs 18-23 ms after, with 456 grouping applications in a later session; not controlled BFR-only benchmarks. No measured speedup is claimed for this native PR.
