Beavers For Real

https://steamcommunity.com/sharedfiles/filedetails/?id=3701165866

TECHNICAL ARCHITECTURE: BeaversForReal - Shoreline Navigation Mod

1. CORE FUNCTIONALITY & HOW IT WORKS
The mod dynamically alters Timberborn's navigation mesh by creating custom "shoreline" edges, allowing beavers to traverse 
vertically between dry land (Upper) and underwater terrain (Lower) without requiring stairs, provided the water depth and 
height differentials meet specific criteria. 

- Initialization: BVRShorelineManager registers a unique NavMesh Group ID ("Calloatti.BeaversForReal").
- Map Scanning: On load, it scans the entire map via _terrainService.GetAllHeightsInCell. It looks for adjacent Z-level 
drops (ledges).
- Edge Generation: When a valid ledge is found (not blocked by finished stackable blocks), it records a BVRShorelineEdge.
- Validation & Injection: During the update loop, it checks if the upper/lower points are valid NavMesh nodes, if the 
vertical gap is within the configured `MaxWaterNavigationHeight`, and if water depth > 0.1f. If valid, it injects two 
directional NavMeshEdge objects (EdgeUp, EdgeDown) directly into the _navMeshService.
- Swimming Animation: BVRSwimmingPatch (Harmony Postfix) overrides the `SmoothOffset` calculation in vanilla's 
`SwimmingAnimator` to prevent beavers from sinking to the lakebed, pinning their visual model near the water surface.
- Vanilla Interference Prevention: BVRPatches intercepts `NavMeshSource.BlockEdge` and `UnblockEdge`, explicitly ignoring 
the mod's custom Group ID so the vanilla engine does not accidentally cull the custom shoreline edges.

2. WHY PERFORMANCE IS NOT NEGATIVELY AFFECTED
The mod employs several aggressive optimization strategies to maintain high UPS/FPS (Updates Per Second / Frames Per 
Second) despite continuously managing thousands of dynamic NavMesh edges:

A. Time-Sliced Processing (Amortization)
Instead of validating the entire map's shoreline edges in a single frame, the `UpdateSingleton` method uses a round-robin 
approach. It only processes a maximum of `EdgesPerFrame` (150) per tick. This distributes the CPU load across multiple 
frames, preventing micro-stutters.

B. Debounced Event Handling
Terrain modifications (e.g., explosions, building placement, terraforming) trigger `TerrainHeightChanged`. Instead of 
immediately recalculating, the mod flags `_needsRescan = true` and applies a `DebounceTime` (1.0 second). If multiple 
blocks change simultaneously, the recalculation is deferred and batched, preventing CPU spikes during massive terrain 
updates.

C. Targeted Spatial Invalidation
When terrain changes, the mod does not rescan the entire map. It records the specific 3x3 grid around the change into an 
`_invalidatedCells` HashSet. When the debounce timer expires, it only destroys and recalculates edges originating from or 
pointing to those specific invalidated coordinates.

D. Zero-Allocation Hash Lookups
In `FindAndAddLedges`, the mod prevents duplicate edge tracking by compressing the 3D coordinates of both the upper and 
lower nodes into a single 64-bit integer (`long hash = (((long)upper.x & 0x3FF) << 50) | ...`). Checking this against a 
HashSet<long> is a highly optimized O(1) operation that avoids object instantiation and garbage collection (GC) overhead.

E. Debug Rendering Isolation
The visualizer (`BVRPathRenderer`) utilizes low-level OpenGL immediate mode drawing (`GL.Begin(GL.LINES)`) and 
`Graphics.DrawMeshNow`. These instructions are gated behind an `if (!Manager.DebugEnabled)` guard clause. When debugging is
 toggled off, the render loop exits instantly, costing exactly zero draw calls.