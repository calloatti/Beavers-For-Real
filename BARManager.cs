using System;
using System.Collections.Generic;
using Timberborn.Localization;
using Timberborn.Navigation;
using Timberborn.QuickNotificationSystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.WaterSystem;
using Timberborn.BlockSystem;
using UnityEngine;

namespace Calloatti.BeaversForReal
{
  public partial class BARManager : ILoadableSingleton, IPostLoadableSingleton, IUpdatableSingleton, IDisposable
  {
    public bool DebugEnabled = false;
    public static int ShorelineGroupId { get; private set; } = -1;

    private int _validationIndex = 0;

    private readonly ITerrainService _terrainService;
    private readonly INavMeshService _navMeshService;
    private readonly NavMeshGroupService _navMeshGroupService;
    private readonly BARInputService _debugInputService;
    private readonly IBlockService _blockService;
    private readonly TerrainNavMeshGraph _terrainNavMeshGraph;
    private readonly RestrictedNodeMap _restrictedNodeMap;
    private readonly NodeIdService _nodeIdService;
    private readonly StackableBlockService _stackableBlockService;

    // FIX 1: Explicitly declare the _waterMap variable
    private readonly IThreadSafeWaterMap _waterMap;

    private readonly List<BAREdge> _shorelines = new List<BAREdge>();
    private readonly Dictionary<long, BAREdge> _shorelineDict = new Dictionary<long, BAREdge>();
    private BARMeshRenderer _visualizer;

    private readonly Vector3Int[] _cardinalDeltas = {
      new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
    };

    // FIX 2: Completely removed EventBus from the parameters so it stops throwing errors
    public BARManager(ITerrainService ts, INavMeshService nms, NavMeshGroupService nmgs, IThreadSafeWaterMap wm, BARInputService dis, QuickNotificationService qns, ILoc loc, IBlockService bs, TerrainNavMeshGraph tng, RestrictedNodeMap rnm, NodeIdService nis, StackableBlockService sbs)
    {
      _terrainService = ts;
      _navMeshService = nms;
      _navMeshGroupService = nmgs;
      _debugInputService = dis;
      _blockService = bs;
      _terrainNavMeshGraph = tng;
      _restrictedNodeMap = rnm;
      _nodeIdService = nis;
      _stackableBlockService = sbs;

      // FIX 1: Assign the injected water map to the variable
      _waterMap = wm;

      _instance = this;
    }

    public void Load()
    {
      ShorelineGroupId = _navMeshGroupService.GetOrAddGroupId("Calloatti.BeaversForReal");
      GameObject visualizerObj = new GameObject("BeaversForReal_Visualizer");
      _visualizer = visualizerObj.AddComponent<BARMeshRenderer>();
      _visualizer.Manager = this;
      _visualizer.Shorelines = _shorelines;

      if (_debugInputService != null) _debugInputService.OnToggleDebug += ToggleDebug;
    }

    public void PostLoad()
    {
      ProcessAndAddEdges();
      EnableDynamicUpdates();
    }

    public void Dispose()
    {
      if (_debugInputService != null) _debugInputService.OnToggleDebug -= ToggleDebug;
      if (_visualizer != null) UnityEngine.Object.Destroy(_visualizer.gameObject);

      // Prevents memory leak when loading a new save!
      SetInstance(null);
    }

    private void ToggleDebug() => DebugEnabled = !DebugEnabled;
  }
}