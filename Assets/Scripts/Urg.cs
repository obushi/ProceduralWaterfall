using UnityEngine;
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

    public void AddVertex(Vector3 pos)
    {
        VertexList.Add(pos);
    }

    public void AddUv(Vector2 uv)
    {
        UVList.Add(uv);
    }

    public void AddIndices(int[] indices)
    {
        IndexList.AddRange(indices);
    }
}


public class Urg : MonoBehaviour
{
    #region Device Config
    [SerializeField]
    URGDevice urg;

    [SerializeField]
    string ipAddress = "192.168.0.35";

    [SerializeField]
    int portNumber = 10940;

    [SerializeField]
    string portName = "COM3";

    [SerializeField]
    int baudRate = 115200;

    [SerializeField]
    bool useEthernetTypeURG = true;

    int beginId;
    int endId;
    #endregion

    #region Debug

    float scale = 0.15f;
    public float Scale
    {
        get { return scale; }
        set
        {
            if (value > 0)
                scale = value;
        }
    }

    Vector3 posOffset = Vector3.zero;
    public Vector3 PosOffset
    {
        get { return posOffset; }
        set { posOffset = value;  }
    }

    bool drawMesh = true;
    public bool DrawMesh {
        get { return drawMesh; }
        set { drawMesh = value; }
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
        if (useEthernetTypeURG)
        {
            urg = new EthernetURG(ipAddress, portNumber);
        }
        else
        {
            urg = new SerialURG(portName, baudRate);
        }

        urg.Open();

        beginId = urg.StartStep;
        endId = urg.EndStep;

        distances = new long[endId - beginId + 1];

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mesh = new Mesh();


        urgMesh = new UrgMesh();

        DetectedObstacles = new Vector4[endId - beginId + 1];
    }

    // Update is called once per frame
    void Update()
    {
        if (urg.Distances.Count() == distances.Length)
            distances = urg.Distances.ToArray();
        
        UpdateObstacleData();

        meshRenderer.enabled = drawMesh;
        if (drawMesh)
        {
            CreateMesh();
            ApplyMesh();
        }
    }

    void UpdateObstacleData()
    {
        for (int i = 0; i < distances.Length; i++)
        {
            Vector3 position = scale * Index2Position(i) + PosOffset;
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

    float Index2Rad(int index)
    {
        float step = 2 * Mathf.PI / urg.StepsCount360;
        float offset = step * (urg.EndStep + urg.StartStep) / 2;
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
            urgMesh.AddVertex(scale * Index2Position(i) + PosOffset);
            urgMesh.AddUv(Camera.main.WorldToViewportPoint(scale * Index2Position(i) + PosOffset));
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