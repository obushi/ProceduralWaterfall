using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System.Linq;

// http://sourceforge.net/p/urgnetwork/wiki/top_jp/
// https://www.hokuyo-aut.co.jp/02sensor/07scanner/download/pdf/URG_SCIP20.pdf

//public static class LINQExtension
//{
//    public static Vector3 Average(this IEnumerable<Vector3> source)
//    {
//        return new Vector3(source.Select(v => v.x).Average(), source.Select(v => v.y).Average());
//    }
//}

//[System.Serializable]
//class DetectedCluster
//{
//    public List<int> indices;
//    [SerializeField]
//    public List<Vector3> vectorList;
//    public Vector3 center;
//    public int objectId;

//    public DetectedCluster(List<int> _indices, List<Vector3> _vectorList, int _id)
//    {
//        indices = new List<int>();
//        vectorList = new List<Vector3>();
//        indices.AddRange(_indices);
//        vectorList.AddRange(_vectorList);
//        center = CalcCenter();
//        objectId = _id;
//    }

//    public Vector3 CalcCenter()
//    {
//        Vector3 result = new Vector3(vectorList.Select(v => v.x).Sum() / vectorList.Count,
//                                     vectorList.Select(v => v.y).Sum() / vectorList.Count);
//        return result;
//    }
//}

//public class Vec3Event : UnityEvent<Vector3>
//{ }

//public class Urg : MonoBehaviour
//{
//    UrgDeviceEthernet urg;

//    [SerializeField]
//    string ipAddress = "192.168.0.35";
//    [SerializeField]
//    int portNumber = 10940;

//    public float scale = 0.001f; // mm -> m
//    public float distanceThreshold = 300.0f;//mm

//    public Color distanceColor = Color.white;
//    public Color[] groupColors;

//    long[] rawDistances;
//    long[] distances;
//    long[] background;

//    public bool debugDraw = false;
//    public Vector2 offsetPosition = new Vector2(0, 12.4f);
//    public float gapThreshold = 40;
//    public float streakThreshold = 10;
//    public float detectObjThreshold = 10;
//    int blankCount = 0;

//    bool enableGui = true;
//    bool captureBackground = false;
//    bool gotBackground = false;

//    [SerializeField]
//    List<DetectedCluster> detectedClusters;
//    List<DetectedCluster> prevClusters;

//    [SerializeField]
//    public Dictionary<string, Vector3> referenceCoords;

//    Vec3Event objectDetectedEvent;
//    Mesh mesh;

//    public GameObject detectedSphere;

//    // Use this for initialization
//    void Start()
//    {
//        rawDistances = new long[1081];
//        distances = new long[1081];
//        background = new long[1081];

//        urg = gameObject.AddComponent<UrgDeviceEthernet>();
//        urg.StartTCP(ipAddress, portNumber);

//        detectedClusters = new List<DetectedCluster>();
//        prevClusters = new List<DetectedCluster>();
//        referenceCoords = new Dictionary<string, Vector3>();

//        objectDetectedEvent = new Vec3Event();
//        objectDetectedEvent.AddListener(OnDetected);

//        mesh = new Mesh();
//    }

//    // Update is called once per frame
//    void Update()
//    {
//        if (Input.GetKeyDown(KeyCode.G))
//            enableGui = !enableGui;

//        // distances
//        if (urg.distances.Count == 1081)
//        {
//            if (rawDistances != urg.distances.ToArray())
//            {
//                rawDistances = urg.distances.ToArray();
//            }

//            // 背景をとる
//            captureBackground = Input.GetKeyDown(KeyCode.Space);
//            if (captureBackground)
//            {
//                background = rawDistances;
//                gotBackground = true;
//            }

//            else
//            {
//                // 背景を持っている
//                if (gotBackground)
//                {
//                    // 背景差分をとる
//                    distances = SubtractBackground(background, rawDistances);

//                    IgnoreOutScreen();

//                    // 前景データを表示
//                    List<Vector3> vertList = new List<Vector3>();
//                    List<Vector2> uvList = new List<Vector2>();
//                    List<int> indexList = new List<int>();
//                    vertList.Add(scale * offsetPosition);
//                    uvList.Add(Camera.main.WorldToViewportPoint(scale * offsetPosition));

//                    for (int i = distances.Length - 1; i >= 0; i--)
//                    {
//                        Debug.DrawLine(scale * offsetPosition, scale * (Index2Position(i, distances) + (Vector3)offsetPosition), Color.blue);
//                        vertList.Add(scale * (Index2Position(i, distances) + (Vector3)offsetPosition));
//                        uvList.Add(Camera.main.WorldToViewportPoint(scale * (Index2Position(i, distances) + (Vector3)offsetPosition)));
//                    }

//                    for (int i = 0; i < distances.Length - 1; i++)
//                    {
//                        indexList.AddRange(new int[] { 0, i + 1, i + 2 });
//                    }

//                    Mesh mesh = new Mesh();
//                    GetComponent<MeshFilter>().sharedMesh = mesh;
//                    mesh.name = "calclatedData";
//                    mesh.vertices = vertList.ToArray();
//                    mesh.uv = uvList.ToArray();
//                    mesh.triangles = indexList.ToArray();

//                    // 距離データのクラスタリング
//                    ClusterDistances();

//                    // 各クラスタのデータを平均に統一
//                    //for (int i = 0; i < detectedClusters.Count; i++)
//                    //{
//                    //    long sum = 0;
//                    //    for (int j = 0; j < detectedClusters[i].indices.Count; j++)
//                    //    {
//                    //        sum += distances[detectedClusters[i].indices[j]];
//                    //    }

//                    //    long ave = sum / detectedClusters[i].indices.Count;
//                    //    for (int j = 0; j < detectedClusters[i].indices.Count; j++)
//                    //    {
//                    //        distances[detectedClusters[i].indices[j]] = ave;
//                    //    }
//                    //}

//                    //クラスタリングの結果を表示
//                    for (int i = 0; i < detectedClusters.Count; i++)
//                    {
//                        //Debug.Log(string.Format("cluster id {0}", detectedClusters[i].objectId));
//                        Vector3 scaledObjPosition = scale * (detectedClusters[i].center + (Vector3)offsetPosition);
//                        Debug.DrawLine(scale * offsetPosition, scaledObjPosition, Color.green);
//                        //Debug.DrawLine(scale * offsetPosition, scale * detectedClusters[i].center.magnitude * (new Vector3(Mathf.Sin(Index2Rad((int)detectedClusters[i].indices.Average())),
//                        //Mathf.Cos(Index2Rad((int)detectedClusters[i].indices.Average()))) + (Vector3)offsetPosition), Color.yellow);
//                        //Debug.Log(string.Format("cluster[{0}] Id {1}", i, detectedClusters[i].objectId));
//                        objectDetectedEvent.Invoke(scaledObjPosition);
//                        //var obj = Instantiate(detectedSphere, scaledObjPosition, Quaternion.identity);
//                        //Destroy(obj, 0.07f);
//                    }
//                }

//                // 背景を持っていないので生データ表示
//                else
//                {
//                    List<Vector3> vertList = new List<Vector3>();
//                    List<Vector2> uvList = new List<Vector2>();
//                    List<int> indexList = new List<int>();
//                    vertList.Add(scale * offsetPosition);
//                    uvList.Add(Camera.main.WorldToViewportPoint(scale * offsetPosition));

//                    for (int i = rawDistances.Length - 1; i >= 0; i--)
//                    {
//                        vertList.Add(scale * (Index2Position(i, rawDistances) + (Vector3)offsetPosition));
//                        uvList.Add(Camera.main.WorldToViewportPoint(scale * (Index2Position(i, rawDistances) + (Vector3)offsetPosition)));
//                    }

//                    for (int i = 0; i < rawDistances.Length - 1; i++)
//                    {
//                        indexList.AddRange(new int[] { 0, i + 1, i + 2 });
//                    }

//                    GetComponent<MeshFilter>().sharedMesh = mesh;
//                    mesh.name = "rawData";
//                    mesh.vertices = vertList.ToArray();
//                    mesh.uv = uvList.ToArray();
//                    mesh.triangles = indexList.ToArray();


//                    if (mesh.triangles.Length != 3240)
//                        Debug.Log(mesh.triangles.Length);

//                }
//            }
//        }
//    }

//    void OnDetected(Vector3 objPosition)
//    {
//        //Debug.Log(objPosition);
//        //Debug.Log(Camera.main.WorldToViewportPoint(objPosition));
//    }

//    private long[] SubtractBackground(long[] background, long[] distances)
//    {
//        Assert.IsTrue(background.Length == distances.Length);
//        long[] subtractedBg = new long[background.Length];
//        var mappedDistances = MapDistances(distances);
//        var mappedBackground = MapDistances(background);
//        for (int i = 0; i < background.Length; i++)
//        {
//            subtractedBg[i] = mappedDistances[i] - mappedBackground[i] > gapThreshold ? mappedDistances[i] : 0;
//        }
//        return subtractedBg;
//    }

//    private bool IsOffScreen(Vector3 worldPosition)
//    {
//        Vector3 viewPos = Camera.main.WorldToViewportPoint(worldPosition);
//        if (viewPos.x < 0 || viewPos.x > 1 || viewPos.y < 0 || viewPos.y > 1)
//            return true;
//        else
//            return false;
//    }

//    void DrawRect(Rect rect, Color color)
//    {
//        Vector3 p0 = new Vector3(rect.x, rect.y, 0);
//        Vector3 p1 = new Vector3(rect.x + rect.width, rect.y, 0);
//        Vector3 p2 = new Vector3(rect.x + rect.width, rect.y + rect.height, 0);
//        Vector3 p3 = new Vector3(rect.x, rect.y + rect.height, 0);
//        Debug.DrawLine(p0, p1, color);
//        Debug.DrawLine(p1, p2, color);
//        Debug.DrawLine(p2, p3, color);
//        Debug.DrawLine(p3, p0, color);
//    }

//    long[] MapDistances(long[] values)
//    {
//        long[] result = new long[values.Length];
//        for (int i = 0; i < values.Length; i++)
//        {
//            result[i] = MapDistance(values[i]);
//        }
//        return result;
//    }

//    long MapDistance(long value)
//    {
//        if (value > 10000 || value < 21)
//            return 0;
//        else
//            return value;
//    }

//    static float Index2Rad(int index)
//    {
//        float step = 2 * Mathf.PI / 1440;
//        float offset = step * 540;
//        return step * index + offset;
//    }

//    Vector3 Index2Position(int index, long[] distances)
//    {
//        Assert.IsTrue(index >= 0 && index <= 1081);
//        return new Vector3(distances[index] * Mathf.Cos(Index2Rad(index)),
//                           distances[index] * Mathf.Sin(Index2Rad(index)));
//    }

//    private void IgnoreOutScreen()
//    {
//        for (int i = 0; i < distances.Length; i++)
//        {
//            if (IsOffScreen(scale * (Index2Position(i, rawDistances) + (Vector3)offsetPosition)))
//            {
//                distances[i] = 0;
//            }
//        }
//    }

//    void ClusterDistances()
//    {
//        bool startCluster = false;
//        bool endCluster = false;
//        List<int> indices = new List<int>();
//        List<Vector3> vectorList = new List<Vector3>();
//        int detectedCount = 0;
//        detectedClusters.Clear();
//        distances[0] = 0;
//        blankCount++;

//        for (int i = 1; i < distances.Length; i++)
//        {
//            Random.InitState(i * (int)Time.realtimeSinceStartup);

//            Vector3 delta = Index2Position(i, distances) - Index2Position(i - 1, distances);
//            if (endCluster)
//            {
//                if (indices.Count > 0 && detectedCount > streakThreshold)
//                {
//                    int clusterId;

//                    //List<DetectedCluster> samePositionObj = prevClusters.Where(prevc => prevc.indices.Average() - indices.Average() < 100).ToList();
//                    List<DetectedCluster> samePositionObj = new List<DetectedCluster>();

//                    prevClusters.Where(prevc => Vector3.Distance(prevc.vectorList.Average(), vectorList.Average()) < detectObjThreshold).ToList();
//                    //List<DetectedCluster> samePositionObj = prevClusters.Where(prevc => prevc.indices.Intersect(indices).Count() > 0).ToList();
//                    if (samePositionObj.Count() > 0)
//                        clusterId = samePositionObj.First().objectId;
//                    else
//                        clusterId = Random.Range(0, 1000);

//                    detectedClusters.Add(new DetectedCluster(indices, vectorList, clusterId));
//                    indices.Clear();
//                    vectorList.Clear();
//                    startCluster = false;
//                }
//                endCluster = false;
//                detectedCount = 0;
//            }
//            else
//            {
//                if (delta.magnitude > gapThreshold)
//                {
//                    if (startCluster)
//                    {
//                        endCluster = true;
//                    }
//                    else
//                    {
//                        indices.Add(i);
//                        vectorList.Add(Index2Position(i, distances));
//                        detectedCount++;
//                        startCluster = true;
//                    }
//                }
//                else if (delta.magnitude != 0)
//                {
//                    if (startCluster)
//                    {
//                        indices.Add(i);
//                        vectorList.Add(Index2Position(i, distances));
//                        detectedCount++;
//                    }
//                }
//                else
//                {
//                    if (startCluster)
//                        endCluster = true;
//                }
//            }
//        }
//        if (blankCount == 4)
//        {
//            prevClusters.Clear();
//            blankCount = 0;
//        }
//        prevClusters.AddRange(detectedClusters);
//    }

//    void OnGUI()
//    {
//        if (enableGui)
//        {
//            if (GUILayout.Button("MD: (計測＆送信要求)"))
//            {
//                urg.Write(SCIP_library.SCIP_Writer.MD(0, 1080, 1, 0, 0));
//            }
//            if (GUILayout.Button("QUIT"))
//            {
//                urg.Write(SCIP_library.SCIP_Writer.QT());
//            }

//            scale = GUILayout.HorizontalSlider(scale, 0, 0.05f);
//            GUILayout.Label("Scale" + scale);

//            offsetPosition.x = GUILayout.HorizontalSlider(offsetPosition.x, -10000, 10000);
//            GUILayout.Label("Position Offset X" + offsetPosition.x);

//            offsetPosition.y = GUILayout.HorizontalSlider(offsetPosition.y, -10000, 10000);
//            GUILayout.Label("Position Offset Y" + offsetPosition.y);

//        }
//    }
//}

public class Urg : MonoBehaviour
{

    [SerializeField]
    UrgDeviceEthernet urg;

    [SerializeField]
    string ipAddress = "192.168.0.35";
    [SerializeField]
    int portNumber = 10940;

    public float scale = 0.001f; // mm -> m
    public float distanceThreshold = 300.0f;//mm

    public Color distanceColor = Color.white;
    public Color[] groupColors;

    long[] rawDistances;

    public bool debugDraw = false;
    public Vector2 offsetPosition = new Vector2(0, 12.4f);
    public float gapThreshold = 40;
    public float streakThreshold = 10;
    public float detectObjThreshold = 10;

    bool enableGui = true;

    public Mesh mesh;

    public GameObject detectedSphere;

    // Use this for initialization
    void Start()
    {
        rawDistances = new long[1081];
        mesh = new Mesh();

        urg = gameObject.AddComponent<UrgDeviceEthernet>();
        urg.StartTCP(ipAddress, portNumber);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
            enableGui = !enableGui;

        // distances
        if (urg.distances.Count == 1081)
        {
            if (rawDistances != urg.distances.ToArray())
            {
                rawDistances = urg.distances.ToArray();
            }

            List<Vector3> vertList = new List<Vector3>();
            List<Vector2> uvList = new List<Vector2>();
            List<int> indexList = new List<int>();
            //vertList.Add(scale * offsetPosition);
            //uvList.Add(Camera.main.WorldToViewportPoint(scale * offsetPosition));

            for (int i = rawDistances.Length - 1; i > 0; i--)
            {
                vertList.Add(scale * (Index2Position(i, rawDistances) + (Vector3)offsetPosition));
                vertList.Add(scale * (Index2Position(i, rawDistances) + (Vector3)offsetPosition) + Vector3.forward);
                vertList.Add(scale * (Index2Position(i-1, rawDistances) + (Vector3)offsetPosition));
                vertList.Add(scale * (Index2Position(i-1, rawDistances) + (Vector3)offsetPosition));
                vertList.Add(scale * (Index2Position(i, rawDistances) + (Vector3)offsetPosition) + Vector3.forward);
                vertList.Add(scale * (Index2Position(i-1, rawDistances) + (Vector3)offsetPosition) + Vector3.forward);
                uvList.Add(Camera.main.WorldToViewportPoint(scale * (Index2Position(i, rawDistances) + (Vector3)offsetPosition)));
                uvList.Add(Camera.main.WorldToViewportPoint(scale * (Index2Position(i, rawDistances) + (Vector3)offsetPosition + Vector3.forward)));
                uvList.Add(Camera.main.WorldToViewportPoint(scale * (Index2Position(i-1, rawDistances) + (Vector3)offsetPosition)));
                uvList.Add(Camera.main.WorldToViewportPoint(scale * (Index2Position(i - 1, rawDistances) + (Vector3)offsetPosition)));
                uvList.Add(Camera.main.WorldToViewportPoint(scale * (Index2Position(i, rawDistances) + (Vector3)offsetPosition + Vector3.forward)));
                uvList.Add(Camera.main.WorldToViewportPoint(scale * (Index2Position(i-1, rawDistances) + (Vector3)offsetPosition + Vector3.forward)));
            }
            vertList.Add(scale * (Index2Position(0, rawDistances) + (Vector3)offsetPosition));
            uvList.Add(Camera.main.WorldToViewportPoint(scale * (Index2Position(0, rawDistances) + (Vector3)offsetPosition)));

            for (int i = 0; i < rawDistances.Length - 1; i++)
            {
                indexList.AddRange(new int[] { i*6, i*6+1, i*6+2 });
                indexList.AddRange(new int[] { i*6+3, i*6+4, i*6+5 });
            }

            //for (int i = rawDistances.Length - 1; i >= 0; i--)
            //{
            //    vertList.Add(scale * (Index2Position(i, rawDistances) + (Vector3)offsetPosition));
            //    uvList.Add(Camera.main.WorldToViewportPoint(scale * (Index2Position(i, rawDistances) + (Vector3)offsetPosition)));
            //}

            //for (int i = 0; i < rawDistances.Length - 1; i++)
            //{
            //    indexList.AddRange(new int[] { 0, i + 1, i + 2 });
            //}

            GetComponent<MeshFilter>().sharedMesh = mesh;
            mesh.name = "rawData";
            mesh.vertices = vertList.ToArray();
            mesh.uv = uvList.ToArray();
            mesh.triangles = indexList.ToArray();


            //if (mesh.triangles.Length != 3240)
            //    Debug.Log(mesh.triangles.Length);

        }
    }

    private bool IsOffScreen(Vector3 worldPosition)
    {
        Vector3 viewPos = Camera.main.WorldToViewportPoint(worldPosition);
        if (viewPos.x < 0 || viewPos.x > 1 || viewPos.y < 0 || viewPos.y > 1)
            return true;
        else
            return false;
    }

    static float Index2Rad(int index)
    {
        float step = 2 * Mathf.PI / 1440;
        float offset = step * 540;
        return step * index + offset;
    }

    Vector3 Index2Position(int index, long[] distances)
    {
        Assert.IsTrue(index >= 0 && index <= 1081);
        return new Vector3(distances[index] * Mathf.Cos(Index2Rad(index)),
                            distances[index] * Mathf.Sin(Index2Rad(index)));
    }

    void OnGUI()
    {
        if (enableGui)
        {
            if (GUILayout.Button("MD: (計測＆送信要求)"))
            {
                urg.Write(SCIP_library.SCIP_Writer.MD(0, 1080, 1, 0, 0));
            }
            if (GUILayout.Button("QUIT"))
            {
                urg.Write(SCIP_library.SCIP_Writer.QT());
            }

            scale = GUILayout.HorizontalSlider(scale, 0, 0.05f);
            GUILayout.Label("Scale" + scale);

            offsetPosition.x = GUILayout.HorizontalSlider(offsetPosition.x, -10000, 10000);
            GUILayout.Label("Position Offset X" + offsetPosition.x);

            offsetPosition.y = GUILayout.HorizontalSlider(offsetPosition.y, -10000, 10000);
            GUILayout.Label("Position Offset Y" + offsetPosition.y);

        }
    }
}