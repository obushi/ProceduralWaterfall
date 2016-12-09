using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System.Linq;
using URG;

// http://sourceforge.net/p/urgnetwork/wiki/top_jp/
// https://www.hokuyo-aut.co.jp/02sensor/07scanner/download/pdf/URG_SCIP20.pdf

class UrgMesh
{
    public List<Vector3> VertexList { get; private set; }
    public List<Vector2> UVList { get; private set; }
    public List<int> IndexList { get; private set; }

    public UrgMesh()
    {
        VertexList = new List<Vector3>();
        UVList = new List<Vector2>();
        IndexList = new List<int>();
    }

    public void Clear()
    {
        VertexList.Clear();
        UVList.Clear();
        IndexList.Clear();
    }

    public void AddVertex(Vector3 _pos)
    {
        VertexList.Add(_pos);
    }

    public void AddUv(Vector2 _uv)
    {
        UVList.Add(_uv);
    }

    public void AddIndices(int[] _indices)
    {
        IndexList.AddRange(_indices);
    }
}


public class Urg : MonoBehaviour
{
    #region Device Config
    [SerializeField]
    EthernetURG urg;

    [SerializeField]
    const string ipAddress = "192.168.0.35";

    [SerializeField]
    const int portNumber = 10940;

    [SerializeField]
    const int beginId = 460;

    [SerializeField]
    const int endId = 620;
    #endregion

    #region Debug

    float _scale = 0.15f;
    public float Scale
    {
        get { return _scale; }
        set
        {
            if (value > 0)
                _scale = value;
        }
    }

    Vector3 _posOffset = Vector3.zero;
    public Vector3 PosOffset
    {
        get { return _posOffset; }
        set { _posOffset = value;  }
    }

    bool _drawMesh = true;
    public bool DrawMesh {
        get { return _drawMesh; }
        set { _drawMesh = value; }
    }
    #endregion

    #region Mesh
    UrgMesh urgMesh;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;
    #endregion

    [SerializeField]
    long[] distances;

    public Vector4[] DetectedObstacles { get; private set; }

    public bool IsConnected { get { return urg.IsConnected; } }

    // Use this for initialization
    void Start()
    {
        distances = new long[endId - beginId + 1];

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mesh = new Mesh();

        urg = new EthernetURG(ipAddress, portNumber);
        urg.Open();
        urgMesh = new UrgMesh();

        DetectedObstacles = new Vector4[endId - beginId + 1];
    }

    // Update is called once per frame
    void Update()
    {
        if (urg.Distances.Count() == distances.Length)
            distances = urg.Distances.ToArray();
        
        UpdateObstacleData();

        meshRenderer.enabled = _drawMesh;
        if (_drawMesh)
        {
            CreateMesh();
            ApplyMesh();
        }
    }

    void UpdateObstacleData()
    {
        for (int i = 0; i < distances.Length; i++)
        {
            Vector3 position = _scale * Index2Position(i) + PosOffset;
            if (IsOffScreen(position) || !IsValidDistance(distances[i]))
            {
                distances[i] = 0;
            }
            DetectedObstacles[i] = new Vector4(position.x, position.y, position.z, distances[i]);
        }
    }

    static bool IsValidDistance(long distance)
    {
        return distance >= 21 && distance <= 30000;
    }

    bool IsOffScreen(Vector3 worldPosition)
    {
        Vector3 viewPos = Camera.main.WorldToViewportPoint(worldPosition);
        return (viewPos.x < 0 || viewPos.x > 1 || viewPos.y < 0 || viewPos.y > 1);
    }

    static float Index2Rad(int index)
    {
        float step = 2 * Mathf.PI / 1440;
        float offset = step * 540;
        return step * index + offset;
    }

    Vector3 Index2Position(int index)
    {
        return new Vector3(distances[index] * Mathf.Cos(Index2Rad(index + beginId)),
                           distances[index] * Mathf.Sin(Index2Rad(index + beginId)));
    }

    void CreateMesh()
    {
        urgMesh.Clear();
        urgMesh.AddVertex(PosOffset);
        urgMesh.AddUv(Camera.main.WorldToViewportPoint(PosOffset));

        for (int i = distances.Length - 1; i >= 0; i--)
        {
            urgMesh.AddVertex(_scale * Index2Position(i) + PosOffset);
            urgMesh.AddUv(Camera.main.WorldToViewportPoint(_scale * Index2Position(i) + PosOffset));
        }

        for (int i = 0; i < distances.Length - 1; i++)
        {
            urgMesh.AddIndices(new int[] { 0, i + 1, i + 2 });
        }
    }

    void ApplyMesh()
    {
        mesh.Clear();
        mesh.name = "URG Data";
        mesh.vertices = urgMesh.VertexList.ToArray();
        mesh.uv = urgMesh.UVList.ToArray();
        mesh.triangles = urgMesh.IndexList.ToArray();
        meshFilter.sharedMesh = mesh;
    }

    public void Connect()
    {
        urg.Write(SCIP_library.SCIP_Writer.MD(beginId, endId, 1, 0, 0));
    }

    public void Disconnect()
    {
        urg.Write(SCIP_library.SCIP_Writer.QT());
    }
}