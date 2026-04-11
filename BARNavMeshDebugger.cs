using System.Collections.Generic;
using HarmonyLib;
using Timberborn.Navigation;
using UnityEngine;

namespace Calloatti.BeaversForReal
{
  // 1. The Custom Renderer (MonoBehaviour)
  public class BARNavMeshRenderer : MonoBehaviour
  {
    public static BARNavMeshRenderer Instance;

    private Material _mat;
    private Material _matGreen;
    private Material _matYellow;
    private Mesh _sphereMesh;

    private bool _drawThisFrame;
    private Vector3Int _centerCoords;
    private NavMeshDrawer _vanillaDrawer;

    // Cached reflections so we don't kill performance every frame
    private Traverse _drawerTraverse;
    private NodeIdService _nodeIdService;
    private TerrainNavMeshGraph _terrainGraph;
    private RestrictedNodeMap _restrictedMap;

    void Awake()
    {
      Instance = this;

      // Revert to the utility shader from your original code that perfectly obeys depth overrides
      Shader debugShader = Shader.Find("Hidden/Internal-Colored");

      // 1. Material for Lines (This shader relies on GL.Color for tinting)
      _mat = new Material(debugShader);
      _mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

      // Extract Unity's default primitive sphere mesh for drawing
      GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      _sphereMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
      Destroy(tempSphere);

      // 2. Material for Green Spheres
      _matGreen = new Material(debugShader);
      _matGreen.SetColor("_Color", Color.green); // Internal-Colored uses _Color
      _matGreen.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

      // 3. Material for Yellow Spheres
      _matYellow = new Material(debugShader);
      _matYellow.SetColor("_Color", Color.yellow);
      _matYellow.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
    }

    void OnDestroy()
    {
      if (_mat != null) Destroy(_mat);
      if (_matGreen != null) Destroy(_matGreen);
      if (_matYellow != null) Destroy(_matYellow);

      // FIX: Prevent memory leak by clearing the static reference to the destroyed object
      if (Instance == this) Instance = null;
    }

    // Called by our Harmony Patch every time the Dev Tool toggle is ON
    public void QueueDrawRequest(NavMeshDrawer drawerInstance, Vector3Int centerCoords)
    {
      // FIX: Re-cache reflections if the drawer instance changes (e.g., loading a new save)
      if (_drawerTraverse == null || _vanillaDrawer != drawerInstance)
      {
        _vanillaDrawer = drawerInstance;
        _drawerTraverse = Traverse.Create(_vanillaDrawer);
        _nodeIdService = _drawerTraverse.Field("_nodeIdService").GetValue<NodeIdService>();
        _terrainGraph = _drawerTraverse.Field("_terrainNavMeshGraph").GetValue<TerrainNavMeshGraph>();
        _restrictedMap = _drawerTraverse.Field("_restrictedNodeMap").GetValue<RestrictedNodeMap>();
      }

      _vanillaDrawer = drawerInstance;
      _centerCoords = centerCoords;
      _drawThisFrame = true;
    }

    void OnRenderObject()
    {
      if (!_drawThisFrame || _vanillaDrawer == null) return;
      _drawThisFrame = false; // Consume the request so it only draws for one frame

      // Grab the actively updated list of nodes from the vanilla drawer
      HashSet<int> nodesWithNeighbors = _drawerTraverse.Field("_nodesWithNeighbors").GetValue<HashSet<int>>();
      if (nodesWithNeighbors == null) return;

      Vector3 centerWorld = NavigationCoordinateSystem.GridToWorld(_centerCoords);

      // 1. Draw Edges (Red)
      _mat.SetPass(0);
      GL.Begin(GL.LINES);
      GL.Color(Color.red);

      foreach (int nodeId in nodesWithNeighbors)
      {
        Vector3 nodePos = _nodeIdService.IdToWorld(nodeId);

        // Vanilla only draws nodes within 30 units of the cursor
        if (Vector3.Distance(centerWorld, nodePos) < 30f)
        {
          foreach (NavMeshNode neighbor in _terrainGraph.GetNeighbors(nodeId))
          {
            Vector3 endPos = _nodeIdService.IdToWorld(neighbor.Id);
            GL.Vertex(nodePos);
            GL.Vertex(endPos);
          }
        }
      }
      GL.End();

      // 2. Draw Node Marker (Green = Normal, Yellow = Restricted)
      Vector3 sphereScale = new Vector3(0.1f, 0.1f, 0.1f); // Set radius to 0.2 blocks wide
      foreach (int nodeId in nodesWithNeighbors)
      {
        Vector3 nodePos = _nodeIdService.IdToWorld(nodeId);

        if (Vector3.Distance(centerWorld, nodePos) < 30f)
        {
          Material activeMat = _restrictedMap.IsNodeRestricted(nodeId) ? _matYellow : _matGreen;
          activeMat.SetPass(0);

          // Draw the solid sphere mesh at the node's position
          Graphics.DrawMeshNow(_sphereMesh, Matrix4x4.TRS(nodePos, Quaternion.identity, sphereScale));
        }
      }
    }
  }

  // 2. The Harmony Patch to hijack the Vanilla Drawer
  [HarmonyPatch(typeof(NavMeshDrawer), nameof(NavMeshDrawer.DrawForOneFrameAroundCoordinates))]
  public static class NavMeshDrawerPatch
  {
    public static bool Prefix(NavMeshDrawer __instance, Vector3Int coordinates)
    {
      // Ensure our renderer exists in the scene
      if (BARNavMeshRenderer.Instance == null)
      {
        GameObject go = new GameObject("BARNavMeshRenderer");
        go.AddComponent<BARNavMeshRenderer>();
      }

      // Intercept the vanilla call and send the data to our GL renderer
      BARNavMeshRenderer.Instance.QueueDrawRequest(__instance, coordinates);

      // Return false to skip the vanilla method entirely (preventing useless Debug.Draw calls)
      return false;
    }
  }
}