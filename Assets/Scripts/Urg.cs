using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System.Linq;

// http://sourceforge.net/p/urgnetwork/wiki/top_jp/
// https://www.hokuyo-aut.co.jp/02sensor/07scanner/download/pdf/URG_SCIP20.pdf

public class Urg : MonoBehaviour
{
    #region Device Config
    [SerializeField]
    UrgDeviceEthernet urg;

    [SerializeField]
    const string ipAddress = "192.168.0.35";

    [SerializeField]
    const int portNumber = 10940;

    [SerializeField]
    const int beginId = 460;

    [SerializeField]
    const int endId = 620;
    #endregion

    #region Thresholds
    [SerializeField]
    float gapThreshold = 40;

    [SerializeField]
    float streakThreshold = 10;
    #endregion

    #region Debug
    [SerializeField]
    float scale = 0.001f; // mm -> m

    public Vector3 posOffset = new Vector3(0, 12.4f);

    [SerializeField]
    bool drawGui = true;
    #endregion

    #region Mesh
    class UrgMesh
    {
        public List<Vector3> vertices;
        public List<Vector2> uv;
        public List<int> indices;

        public UrgMesh()
        {
            vertices = new List<Vector3>();
            uv = new List<Vector2>();
            indices = new List<int>();
        }

        public void Clear()
        {
            vertices.Clear();
            uv.Clear();
            indices.Clear();
        }
    }

    UrgMesh urgMesh;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    public Mesh mesh;
    #endregion

    [System.Serializable]
    public struct DetectedObject
    {
        public float objectSize;
        public Vector3 position;
    }

    [SerializeField]
    long[] distances;
    long[] prevDistances;

    public DetectedObject[] DetectedObjects;
    List<DetectedObject> tempDetectedObjects;

    // Use this for initialization
    void Start()
    {
        distances = new long[endId - beginId + 1];
        prevDistances = new long[endId - beginId + 1];

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mesh = new Mesh();

        urg = gameObject.AddComponent<UrgDeviceEthernet>();
        urg.StartTCP(ipAddress, portNumber);
        urgMesh = new UrgMesh();

        DetectedObjects = new DetectedObject[10];
        tempDetectedObjects = new List<DetectedObject>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
            drawGui = !drawGui;

        if (distances.Length != 0)
            prevDistances = distances;
        distances = urg.distances.ToArray();
        
        PreProcess();
        DetectObjects();

        if (drawGui)
        {
            meshRenderer.enabled = true;
            UpdateMeshFilter();
        }
        else
        {
            meshRenderer.enabled = false;
        }

        for (int i = 0; i < DetectedObjects.Count(); i++)
            Debug.DrawLine(posOffset, DetectedObjects[i].position, Color.green);
    }

    private void PreProcess()
    {
        for (int i = 0; i < distances.Count(); i++)
        {
            Vector3 position = Index2Position(i);
            if (IsOffScreen(scale * position + posOffset) || !IsValidValue(distances[i]))
            {
                distances[i] = 0;
            }
        }
    }

    private bool IsValidValue(long value)
    {
        return value >= 21 && value <= 30000;
    }

    private void DetectObjects()
    {
        tempDetectedObjects.Clear();
        bool hasBegunObj = false;
        bool willEndObj = false;
        int seriesCount = 0;

        for (int i = 2; i < distances.Count(); i++)
        {
            if (willEndObj)
            {
                if (seriesCount > streakThreshold)
                {
                    DetectedObject obj;
                    obj.objectSize = seriesCount * 2 * Mathf.PI / 1440 / 2;
                    obj.position = scale * Index2Position((i - seriesCount + i) / 2) + posOffset;
                    tempDetectedObjects.Add(obj);
                }
                hasBegunObj = false;
                willEndObj = false;
                seriesCount = 0;
            }
            else
            {
                var delta = (distances[i] + prevDistances[i]) - 0.5 * (distances[i - 1] + prevDistances[i - 1] + distances[i - 2] + prevDistances[i - 2]);
                if (delta > gapThreshold)
                {
                    if(hasBegunObj)
                    {
                        willEndObj = true;
                    }
                    else
                    {
                        seriesCount ++;
                        hasBegunObj = true;
                    }
                }
                else if(delta == 0)
                {
                    if (hasBegunObj)
                        willEndObj = true;
                }
                else
                {
                    if(hasBegunObj)
                    {
                        seriesCount ++;
                    }
                }
            }
        }

        DetectedObjects = tempDetectedObjects.OrderByDescending(o => o.objectSize)
                                     .Take(10)
                                     .ToArray();
    }

    private bool IsOffScreen(Vector3 worldPosition)
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

    void UpdateMeshFilter()
    {
        var distances = this.distances.ToArray();
        urgMesh.Clear();
        urgMesh.vertices.Add(posOffset);
        urgMesh.uv.Add(Camera.main.WorldToViewportPoint(posOffset));


        for (int i = distances.Length - 1; i >= 0; i--)
        {
            urgMesh.vertices.Add(scale * Index2Position(i) + posOffset);
            urgMesh.uv.Add(Camera.main.WorldToViewportPoint(scale * Index2Position(i) + posOffset));
        }

        for (int i = 0; i < distances.Length - 1; i++)
        {
            urgMesh.indices.AddRange(new int[] { 0, i + 1, i + 2 });
        }
        
        mesh.name = "URG Data";
        mesh.vertices = urgMesh.vertices.ToArray();
        mesh.uv = urgMesh.uv.ToArray();
        mesh.triangles = urgMesh.indices.ToArray();
        meshFilter.sharedMesh = mesh;
    }

    void OnGUI()
    {
        if (drawGui)
        {
            if (GUILayout.Button("MD: (計測＆送信要求)"))
            {
                urg.Write(SCIP_library.SCIP_Writer.MD(beginId, endId, 1, 0, 0));
            }
            if (GUILayout.Button("QUIT"))
            {
                urg.Write(SCIP_library.SCIP_Writer.QT());
            }

            scale = GUILayout.HorizontalSlider(scale, 0, 0.15f);
            GUILayout.Label("Scale" + scale);

            posOffset.x = GUILayout.HorizontalSlider(posOffset.x, -20, 20);
            GUILayout.Label("Position Offset X" + posOffset.x);

            posOffset.y = GUILayout.HorizontalSlider(posOffset.y, -30, 30);
            GUILayout.Label("Position Offset Y" + posOffset.y);

        }
    }
    
}