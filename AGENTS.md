# Beavers For Real — Mod-Specific Agent Instructions

## Identity
- **Mod ID:** `Calloatti.BeaversForReal`
- **Assembly:** `beaversforreal`
- **Min Game Version:** 1.0.12.10 — uses `timberborn-decompiled-1.0.*` and `timberborn-ripped-1.0.*`
- **Framework:** .NET Standard 2.1, C# 10, Harmony
- **Entry point:** `IModStarter` with `Harmony.PatchAll()` — Harmony ID `"calloatti.beaversforreal"`
- **Config:** SimpleConfig (`ModStarter.Config`) — config file `BeaversForReal.txt`

## What This Mod Does
Allows beavers to naturally enter and exit water directly from riverbanks. Scans terrain ledges at water-adjacent positions and injects NavMesh edges so beavers can path through water. Edges are dynamically blocked/unblocked based on water depth and contamination.

## Source Architecture (`Version-1.0/Source/`)

| File | Role |
|---|---|
| `ModStarter.cs` | Entry point — Harmony.PatchAll(), initializes SimpleConfig |
| `ModConfigurator.cs` | DI configurator `[Context("Game")]` — binds `BFRManager` and `BFRInputService` as singletons |
| `BFRManager.cs` | Core singleton (`ILoadableSingleton`, `IPostLoadableSingleton`, `ITickableSingleton`, `IDisposable`). Creates visualizer GameObject, manages `ShorelineGroupId`, debug toggle. |
| `BFRManager.AddEdges.cs` | Two-phase shoreline scan. **Phase 1**: scans all nav mesh nodes for standable surfaces near water, caching results in `_isStandableCache`. **Phase 2**: for each ledge, checks cardinal neighbors for valid drops and creates `BFREdge` pairs. |
| `BFRManager.SurfaceValidator.cs` | `IsStandableSurface()` — checks if a coordinate has terrain or a finished stackable block (`Levee`/`Platform`) directly below. |
| `BFRManager.WaterUpdates.cs` | `Tick()` — batches `ShorelinesPerTick` (500) shoreline checks per tick for water height and contamination. Blocks/unblocks edges via `NavMeshService.BlockEdge`/`UnblockEdge`. |
| `BFRManager.DynamicUpdates.cs` | Harmony prefix on `NavigationSynchronizer.ProcessRegularChanges` to intercept terrain/building edits. Performs localized 4-phase update (scan, validate, delete stale edges, add new edges) within changed bounds + 1-block padding. |
| `BFREdge.cs` | Data class: `Upper`/`Lower` node ID + coordinate, `IsBlockedByWater`, `NavMeshEdge EdgeDown` / `EdgeUp` pair. |
| `BFRInputService.cs` | `IInputProcessor` — listens for `Calloatti.BeaversForReal.KeyBind.Toggle` to toggle debug visualization. |
| `BFRPatches.cs` | All Harmony patches — `SwimmingAnimator.SmoothOffset` (visual depth fix), `NavMeshSourceNode.UnblockEdge` (crash safety), `CitizenUnstucker.TryUnstuckAndKeepDistrict` (spiral-up rescue). |
| `BFRNavMeshDebugger.cs` | Custom `BFRNavMeshRenderer` MonoBehaviour + Harmony prefix on `NavMeshDrawer.DrawForOneFrameAroundCoordinates`. Replaces vanilla `Debug.Draw` with GL-based rendering (lines + colored spheres). |
| `BFRMeshRenderer.cs` | Debug visualization MonoBehaviour — draws shoreline edges (green=active, red=blocked) as lines + spheres when `DebugEnabled` is toggled. |

## The Six Pillars (Architecture)
1. **Two-Phase Shoreline Scan** — Phase 1: scan every node, check `IsStandableSurface`, filter by nav mesh presence, buildings, restricted nodes, and full-neighbor count (8 = already connected). Phase 2: for each potential ledge, scan cardinal neighbors downward for a standable landing, validate the air gap, and inject `NavMeshEdge` pairs.
2. **Water-Height Gating** — Each tick, `ProcessWaterLevel` checks `WaterHeightOrFloor` and `ColumnContamination` at the lower node of each edge. If the drop exceeds `MaxWaterNavigationHeight` (0.5) or contamination exceeds `MaxWaterContamination` (0.05), the edge is blocked; otherwise unblocked. Batching at 500/tick prevents perf spikes.
3. **Localized Dynamic Updates** — Harmony prefix on `NavigationSynchronizer.ProcessRegularChanges` intercepts queued terrain/road changes. Extracts affected node coordinates and triggers a localized 4-phase re-scan within the bounding box (±1 padding). Phase 3 deletes stale edges, Phase 4 adds new ones — avoids full-map re-scan on every edit.
4. **CitizenUnstucker Rescue** — Postfix on `CitizenUnstucker.TryUnstuckAndKeepDistrict`. When the vanilla unstucker fails, searches upward from the beaver's position (spiraling X/Y at each Z level) for the first globally-reachable position. Teleports the beaver there and calls `Walker.StopNextTick`. Restored null check for `preferredDistrict` to prevent `Traverse` crash on districtless beavers.
5. **UnblockEdge Safety Patch** — Prefix on `NavMeshSourceNode.UnblockEdge` checks if the blockage key actually exists in `____blockages` before allowing the unblock. Returns false if not found, preventing crashes from double-unblock or stale blockages.
6. **Memory Reuse Optimization** — `_isStandableCache` (bool array sized to `NodeIdService.NumberOfNodes`), `_potentialLedges` (List<int>), and `_thisEdgesAreOk` (Dictionary) are reused between initial scan and dynamic updates. `Array.Clear` vs re-allocation based on size changes.

## Key Data Flow
```
Game start → ModStarter.StartMod() → Harmony.PatchAll(), SimpleConfig init
  → ModConfigurator.Configure() → binds BFRManager, BFRInputService
  → BFRManager.Load() → ShorelineGroupId = GetOrAddGroupId(), creates visualizer GO
  → BFRManager.PostLoad() → ProcessAndAddEdges(), EnableDynamicUpdates()
    → Phase 1: scan all nodes → _isStandableCache, _potentialLedges
    → Phase 2: for each ledge → drop scan → air gap check → NavMeshEdge.CreateGrouped
    → _navMeshService.AddEdge() for each down/up pair

Water tick → BFRManager.Tick() → ProcessWaterLevel() batch 500/tick
  → waterSurface = WaterHeightOrFloor, contamination = ColumnContamination
  → BlockEdge/UnblockEdge based on config thresholds

Terrain edit → NavigationSynchronizer.ProcessRegularChanges [HarmonyPrefix]
  → ExtractNodes() → _pendingUpdateCoordinates
  → Next Tick → ProcessLocalizedChange(changedCoords)
    → Phase 1: scan bounding box (±1) for standable surfaces
    → Phase 2: validate candidate edges within bounds
    → Phase 3: remove stale edges from _shorelines/_shorelineDict
    → Phase 4: add new edges to _shorelines + NavMesh

Beaver stuck → CitizenUnstucker.TryUnstuckAndKeepDistrict [HarmonyPostfix]
  → spiral X/Y at each Z from current pos → IsGloballyReachableFromPosition
  → teleport to first reachable, StopNextTick
```

## Config Settings (simpleconfig.txt)
| Key | Type | Default | Description |
|---|---|---|---|
| `MaxWaterNavigationHeight` | float (Slider) | 0.5 | Max Z-diff from ledge to water surface for path to stay unblocked |
| `LogUnstuckBeavers` | bool (Toggle) | false | Log unstuck teleport events to console |
| `MaxWaterContamination` | float (Slider) | 0.05 | Max contamination fraction for path to stay unblocked |

## Known Pitfalls & Lessons Learned
- **`ShorelineGroupId`** is obtained via `_navMeshGroupService.GetOrAddGroupId("Calloatti.BeaversForReal")` in `Load()`, not hardcoded. It's stored as a static property on BFRManager.
- **Edge Hash** (`GetHash`) packs 6×10-bit coordinate components into a `long`. Each component is masked to 0x3FF (1024 range). If maps exceed 1024 in any axis, hash collisions will occur.
- **`_isStandableCache`** is a `bool[]` sized to `_nodeIdService.NumberOfNodes`. Must be re-checked and potentially re-allocated in `ProcessLocalizedChange` since node count can change between the initial scan and dynamic updates.
- **Grid ↔ World conversion** — `NavigationCoordinateSystem.GridToWorld` / `WorldToGridInt`. Grid coords are `Vector3Int`, world is `Vector3`. Be aware that grid-Z maps to world-Y (Unity Y-up).
- **`_stackableBlockService.IsFinishedStackableBlockAt`** is used in `IsStandableSurface` to check for artificial surfaces (levees, platforms) — this is what enables beavers to climb out of water onto player-built structures.
- **CitizenUnstucker patch uses `Traverse`** to call `preferredDistrict.IsGloballyReachableFromPosition` (private method). The null check for `preferredDistrict` is critical — districtless beavers will crash without it.
- **Dynamic update skip** — The first `ProcessRegularChanges` call after PostLoad is skipped (`_processRegularChangesFirstRun`) to avoid double-processing the initial state.
- **Debug config** — stored in `simpleconfig.txt` as part of the SimpleConfig system. Debug visualization toggled via keybinding `Calloatti.BeaversForReal.KeyBind.Toggle`. Toggle state is NOT persisted.
- **Localization** — Uses SimpleConfig locale keys (`Calloatti.Config.BeaversForReal.*`). Has per-locale CSV files in `Localizations/` for both main strings and SimpleConfig labels.
- **Harmony patches** target specific game classes in `BFRPatches.cs` (single file, not split — following existing convention).
- **Water contamination support** added in v2.1.0. The `ColumnContamination(Vector3Int)` overload respects vertical water column stacking (e.g., aqueducts).
- **`ProcessWaterLevel` uses the `Vector3Int` overload** of `ColumnContamination` — not the `Vector2Int` one — to correctly handle multi-level water columns.

## Build & Deploy
- Build via `dotnet build` in `Version-1.0/` or Visual Studio `.slnx`.
- Pre/post build scripts (`prebuild.ps1`/`postbuild.ps1`) handle assembly copying.
- `CommonModSettings.props` defines Timberborn game DLL references, publicizer configuration, and output paths.
- Game assemblies path: `C:\Program Files (x86)\Steam\steamapps\common\timberborn_main\Timberborn_Data\Managed`
- Harmony DLL path: Steam workshop content folder (currently `3284904751`).

## Game Source Access & Research

### Version-to-Path Mapping
Each mod's `Version-{X.Y}` folder targets game version `{X}.{Y}.x.x`. The suffix after the version number (e.g., `-b769e88-sw`) does not matter — match on the major.minor prefix using a wildcard.

| Version Folder | Game Version | Decompiled (glob) | Ripped (glob) | Docs (glob) |
|---|---|---|---|---|
| `Version-1.0` | `1.0.x.x` | `timberborn-decompiled-1.0.*` | `timberborn-ripped-1.0.*` | `timberborn-docs-1.0.*` |
| `Version-1.1` | `1.1.x.x` | `timberborn-decompiled-1.1.*` | `timberborn-ripped-1.1.*` | _(none yet)_ |

### Base Path
All game reference directories live under `C:\Users\calloatti\source\repos\`.

### Available Directory Types
| Prefix | Contents |
|---|---|
| `timberborn-decompiled-{version}*` | Decompiled C# game source |
| `timberborn-ripped-{version}*` | Ripped Unity assets (sprites, shaders, prefabs) |
| `timberborn-docs-{version}*` | Per-assembly documentation markdown |

### Decompiled Directory Structure
Inside each decompiled folder:
  * `EditorDll`
  * `EditorUI`
  * `Localizations`
  * `Shaders`
  * `UI`
  * `Blueprints`

### Version Checking
Target game versions can be confirmed via `_version.txt` at the root of each decompiled folder. Compare this to the `MinimumGameVersion` value in the mod's `manifest.json`.
