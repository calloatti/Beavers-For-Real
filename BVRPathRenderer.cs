using System.Collections.Generic;
using Timberborn.Navigation;
using UnityEngine;

namespace Calloatti.BeaversForReal
{
  public class BVRPathRenderer : MonoBehaviour
  {
    public BVRShorelineManager Manager;
    private Material _mat;
    private Mesh _cubeGreen;
    private Mesh _cubeRed;
    public List<BVRShorelineEdge> Shorelines;

    void Awake()
    {
      _mat = new Material(Shader.Find("Hidden/Internal-Colored"));
      _mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
      _cubeGreen = CreateCube(Color.green);
      _cubeRed = CreateCube(Color.red);
    }

    void OnDestroy()
    {
      if (_mat != null) Destroy(_mat);
      if (_cubeGreen != null) Destroy(_cubeGreen);
      if (_cubeRed != null) Destroy(_cubeRed);
    }

    private Mesh CreateCube(Color color)
    {
      Mesh m = new Mesh();
      m.vertices = new Vector3[] { new Vector3(-0.05f, -0.05f, -0.05f), new Vector3(0.05f, -0.05f, -0.05f), new Vector3(0.05f, 0.05f, -0.05f), new Vector3(-0.05f, 0.05f, -0.05f), new Vector3(-0.05f, -0.05f, 0.05f), new Vector3(0.05f, -0.05f, 0.05f), new Vector3(0.05f, 0.05f, 0.05f), new Vector3(-0.05f, 0.05f, 0.05f) };
      Color[] cols = new Color[8]; for (int i = 0; i < 8; i++) cols[i] = color;
      m.colors = cols;
      m.SetIndices(new int[] { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 }, MeshTopology.Lines, 0);
      return m;
    }

    void OnRenderObject()
    {
      if (Manager == null || !Manager.DebugEnabled || Shorelines == null || _mat == null) return;

      _mat.SetPass(0);
      foreach (var s in Shorelines)
      {
        bool visualActive = s.IsActive;
        Vector3 u = NavigationCoordinateSystem.GridToWorld(s.Upper);
        Vector3 l = NavigationCoordinateSystem.GridToWorld(s.Lower);
        Graphics.DrawMeshNow(visualActive ? _cubeGreen : _cubeRed, Matrix4x4.Translate(u));
        Graphics.DrawMeshNow(visualActive ? _cubeGreen : _cubeRed, Matrix4x4.Translate(l));
        GL.Begin(GL.LINES);
        GL.Color(visualActive ? Color.green : Color.red);
        GL.Vertex(u); GL.Vertex(l);
        GL.End();
      }
    }
  }
}