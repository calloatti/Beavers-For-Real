Beavers For Real

https://steamcommunity.com/sharedfiles/filedetails/?id=3701165866

TECHNICAL ARCHITECTURE: BeaversForReal - Shoreline Navigation Mod

1. CORE FUNCTIONALITY & HOW IT WORKS
The mod dynamically alters Timberborn's navigation mesh by creating custom "shoreline" edges, allowing beavers to traverse 
vertically between dry land (Upper) and underwater terrain (Lower) without requiring stairs, provided the water depth and 
height differentials meet specific criteria. 

- Initialization: BFRManager registers a unique NavMesh Group ID ("Calloatti.BeaversForReal").
- Map Scanning: On load, it scans the entire map via _terrainService.GetAllHeightsInCell. It looks for adjacent Z-level 
drops (ledges).
- Edge Generation: When a valid ledge is found (not blocked by finished stackable blocks), it records a BFREdge.
- Validation & Injection: During the update loop, it checks if the upper/lower points are valid NavMesh nodes, if the 
vertical gap is within the configured `MaxWaterNavigationHeight`, and if water depth > 0.1f. If valid, it injects two 
directional NavMeshEdge objects (EdgeUp, EdgeDown) directly into the _navMeshService.
- Swimming Animation: BFRSwimmingPatch (Harmony Postfix) overrides the `SmoothOffset` calculation in vanilla's 
`SwimmingAnimator` to prevent beavers from sinking to the lakebed, pinning their visual model near the water surface.
- Vanilla Interference Prevention: BFRPatches intercepts `NavMeshSource.BlockEdge` and `UnblockEdge`, explicitly ignoring 
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
The visualizer (`BFRPathRenderer`) utilizes low-level OpenGL immediate mode drawing (`GL.Begin(GL.LINES)`) and 
`Graphics.DrawMeshNow`. These instructions are gated behind an `if (!Manager.DebugEnabled)` guard clause. When debugging is
 toggled off, the render loop exits instantly, costing exactly zero draw calls.
 
REFACTORED VERSION
=============================================================================
BFR SHORELINE MANAGER: DEEP-DIVE TECHNICAL ARCHITECTURE
THE BOOTSTRAPPER (AddEdges.cs)

When Timberborn loads a map, the dependency injection framework (Bindito) spawns the BFRManager singleton. Because the game 
has no native concept of shoreline pathing, AddEdges.cs performs a brute-force sweep.

It uses _nodeIdService.NumberOfNodes to iterate through every single node ID on the map and converts them to grid coordinates
 via _nodeIdService.IdToGrid().

For every node, it runs your custom physical rules (IsStandableSurface, IsLedgePhysicallyValid).

It directly calls _navMeshService.AddEdge() to inject our custom shoreline edges (grouped under your specific 
ShorelineGroupId) into the game's pathing brain. This establishes the baseline grid.

THE EVENT HOOK (DynamicUpdates.cs)

During gameplay, players dig terrain and place buildings. We could patch the building placement events or terrain digging 
events directly, but that causes a massive race condition: if we check the map the millisecond a player places a levee, the 
game's native pathfinding hasn't updated yet. We would be reading stale data.

Instead, Timberborn places all terrain/road changes into holding queues inside the NavMeshUpdater class. Eventually, the game
 calls NavigationSynchronizer.ProcessRegularChanges() to flush those queues and recalculate the NavMesh all at once.

By using Harmony to patch ProcessRegularChanges, we surgically attach the mod to the exact moment Timberborn recalculates its
 pathing grid.

THE PREFIX: DATA EXTRACTION

When ProcessRegularChanges.Prefix fires, Timberborn is about to process its queues. If we wait, the queues will be emptied 
and we won't know what changed.

Here, we intercept the __instance._navMeshUpdater and read its internal queues: _enqueuedRegularTerrainChanges and 
_enqueuedRegularRoadChanges.

These queues are extracted by the ExtractNodes method.

We iterate through them, grab the _startNodeId and _endNodeId, and feed them into _nodeIdService.IdToGrid() to convert the 
raw game IDs into spatial Vector3Int coordinates.

We stash these coordinates in _pendingUpdateCoordinates and let the vanilla game continue its processing.

THE POSTFIX: LOCALIZED AUDIT

When ProcessRegularChanges.Postfix fires, Timberborn has completely finished updating its world state. _terrainNavMeshGraph 
is fully updated, and _blockService knows exactly where every new building is. NOW we run the audit.

Instead of scanning the whole map, we take the unique coordinates from _pendingUpdateCoordinates.

We draw a highly optimized, tight ±1 block bounding box around them (searchMinX = minX - 1, etc.) because custom shoreline 
edges only span exactly 1 horizontal block.

PHASE 1: NODE SCAN

We iterate through the bounding box using _terrainService.Contains() to stay in bounds.

For each coordinate, we get the ID using _nodeIdService.GridToId().

We check IsStandableSurface().

If the game says the node IS on the NavMesh, we query _terrainNavMeshGraph.GetNeighbors() to evaluate adjacent connections. 
Crucially, we filter out our own custom edges by checking _shorelineDict to prevent infinite update loops (flip-flopping).

If the node is NOT on the NavMesh, we query _blockService.GetObjectsAt() looking for block objects. We explicitly ignore 
plants and natural resources to ensure we only block ledges when true solid buildings are present.

Surviving nodes are added to a potentialLedges list.

PHASE 2: CONNECTION SCAN

For each ledge, we check its neighbors using _terrainNavMeshGraph.GetNeighbors().

We skip connections that are already registered as our custom edges in _shorelineDict.

We generate a candidate BFREdge and run it through your IsLedgePhysicallyValid() method. Because we are in the Postfix, this 
method is evaluating against the completely updated, 100% accurate terrain and water states.

Edges that pass are hashed and stored in ThisEdgesAreOk.

PHASE 3: DELETION

We iterate backwards through our active _shorelines list.

If an existing edge falls inside our ±1 bounding box but is NO LONGER inside ThisEdgesAreOk (e.g., because a new building 
blocked IsLedgePhysicallyValid), we call _navMeshService.RemoveEdge() to dynamically sever the connection.

PHASE 4: ADDITION

We iterate through ThisEdgesAreOk.

If we find a hash that isn't in our _shorelineDict (e.g., the player just dug a brand new valid shoreline), we call 
_navMeshService.AddEdge() to instantly wire it into the pathing grid.

SUMMARY
The dynamic update system is a masterclass in hijacking the game's native lifecycle. By grabbing node IDs from NavMeshUpdater
 in the Prefix, waiting for the game to update its grids, and running an idempotent localized audit in the Postfix with a ±1 
block padding, we ensure the custom edges perfectly react to the dynamic world without infinite loops and without ever having
 to rescan the entire map.


THE PROBLEM: BEAVERS GETTING STUCK INSIDE TERRAIN
In vanilla Timberborn, the `CitizenUnstucker` system is designed to rescue beavers whose paths become blocked. However, it 
explicitly only rescues beavers stuck inside a `FinishedBuilding`. 
Because your custom shorelines allow beavers to drop down into water tiles to plant Mangroves (which are `NaturalResources`, 
not buildings), the vanilla rescue system ignores them. 
When the beaver drops down into the hole to plant the solid Mangrove, the plant's physical collider immediately traps the 
beaver against the cliff wall. Because the vanilla game refuses to rescue them from a plant or empty air, the beaver becomes 
permanently clipped halfway inside the terrain block.

THE SOLUTION:
We created a Harmony patch for `CitizenUnstucker.TryUnstuckAndKeepDistrict`.
Instead of hiding the problem from the game, we allow the game to realize the beaver is cut off so it triggers the emergency 
rescue sequence.
If the vanilla rescue fails (because the beaver is not in a building), our patch takes over and performs a custom vertical 
scan. It checks the 3x3 horizontal area around the beaver, starting at the current elevation (Z-level) and climbing upward 
block-by-block to the map's maximum height (32).
Once it finds the first valid block safely inside the District (the top of the shoreline cliff), it perfectly mimics the 
vanilla rescue by physically teleporting the beaver to that safe coordinate and confirming the rescue with the game.