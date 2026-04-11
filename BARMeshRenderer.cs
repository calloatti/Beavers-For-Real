using System.Collections.Generic;
using Timberborn.Navigation;
using UnityEngine;

namespace Calloatti.BeaversForReal
{
  public class BARMeshRenderer : MonoBehaviour
  {
    public BARManager Manager;
    private Material _matLine;
    private Material _matGreen;
    private Material _matRed;
    private Mesh _sphereMesh;
    public List<BAREdge> Shorelines;

    void Awake()
    {
      Shader debugShader = Shader.Find("Hidden/Internal-Colored");

      // Material for the connection lines
      _matLine = new Material(debugShader);
      _matLine.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

      // Materials for the spheres
      _matGreen = new Material(debugShader);
      _matGreen.SetColor("_Color", Color.green);
      _matGreen.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

      _matRed = new Material(debugShader);
      _matRed.SetColor("_Color", Color.red);
      _matRed.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

      // Extract the sphere mesh
      GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      _sphereMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
      Destroy(tempSphere);
    }

    void OnDestroy()
    {
      if (_matLine != null) Destroy(_matLine);
      if (_matGreen != null) Destroy(_matGreen);
      if (_matRed != null) Destroy(_matRed);
    }

    void OnRenderObject()
    {
      if (Manager == null || !Manager.DebugEnabled || Shorelines == null || _matLine == null) return;

      Vector3 sphereScale = new Vector3(0.1f, 0.1f, 0.1f);

      // 1. Draw Connection Lines
      _matLine.SetPass(0);
      GL.Begin(GL.LINES);
      foreach (var s in Shorelines)
      {
        bool visualActive = !s.IsBlockedByWater;
        GL.Color(visualActive ? Color.green : Color.red);
        GL.Vertex(NavigationCoordinateSystem.GridToWorld(s.Upper));
        GL.Vertex(NavigationCoordinateSystem.GridToWorld(s.Lower));
      }
      GL.End();

      // 2. Draw Spheres
      foreach (var s in Shorelines)
      {
        bool visualActive = !s.IsBlockedByWater;
        Material activeMat = visualActive ? _matGreen : _matRed;
        activeMat.SetPass(0);

        Vector3 uPos = NavigationCoordinateSystem.GridToWorld(s.Upper);
        Vector3 lPos = NavigationCoordinateSystem.GridToWorld(s.Lower);

        Graphics.DrawMeshNow(_sphereMesh, Matrix4x4.TRS(uPos, Quaternion.identity, sphereScale));
        Graphics.DrawMeshNow(_sphereMesh, Matrix4x4.TRS(lPos, Quaternion.identity, sphereScale));
      }
    }
  }
}