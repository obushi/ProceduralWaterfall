using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using System.Collections.Generic;

//public class Guideline
//{
//    public struct LineProperty
//    {
//        public int Id;
//        public Vector3 BirthPosition;
//        public Vector3 DeathPosition;
//        public Vector3 Position;
//        public Vector3 InitVelocity;
//        public Vector3 Velocity;
//    }

//    public LineProperty Property;
//    private GameObject Line;
//    private LineRenderer GuidelineRenderer;
//    private float gravity;

//    Guideline(int id, Vector3 birthPos, Vector3 deathPos, Vector3 position, Vector3 initVelocity, Vector3 velocity)
//    {
//        Line = new GameObject();

//        Property.Id = id;
//        Property.BirthPosition = birthPos;
//        Property.DeathPosition = deathPos;
//        Property.Position = position;
//        Property.InitVelocity = initVelocity;
//        Property.Velocity = velocity;
        
//        GuidelineRenderer = Line.AddComponent<LineRenderer>();
//        GuidelineRenderer.material.shader = Shader.Find("Unlit/Color");
//        GuidelineRenderer.SetPosition(0, Property.BirthPosition);
//        GuidelineRenderer.SetPosition(1, Property.DeathPosition);
//    }

//    Vector3[] GetParabolaPoints(int numDivision)
//    {
//        List<Vector3> result = new List<Vector3>();
//        float a = (Property.DeathPosition.y - Property.BirthPosition.y) / Mathf.Pow((Property.DeathPosition.z - Property.BirthPosition.z), 2);

//        for (int i = 0; i < numDivision; i++)
//        {
//            float t = i / (float)numDivision;
//            float x = t * Property.DeathPosition.z - Property.BirthPosition.z;
//            float y = a * (x - Property.BirthPosition.z) * (x - Property.BirthPosition.z) + Property.BirthPosition.y;
//            result.Add(new Vector3(0, y, x));
//        }
//        return result.ToArray();
//    }
//}

public struct StreamLine
{
    public int Id;
    public Vector3 BirthPosition;
    public Vector3 DeathPosition;
    public Vector3 Position;
    public Vector3 InitVelocity;
    public Vector3 Velocity;
}

public struct Drop
{
    public float DropSize;
    public Vector3 Position;
    public Vector3 Velocity;
}

public class Waterfall : MonoBehaviour {

    #region Global parameters

    public Vector3 AreaSize = new Vector3(1.0f, 15.0f, 1.0f);
    public Texture2D DropTexture;
    public bool showStreamLines = true;
    public Camera BillboardCam;

    #endregion

    #region Emitter parameters

    public Vector3 EmitterSize = new Vector3(0, 9, 0);
    public Vector3 EliminatorSize = new Vector3(0, 0, -3);

    [Range(0.0005f, 2.0f)]
    public float DropSize = 0.001f;

    [Range(0.01f, 10.0f)]
    public float g = 4.0f;

    const int maxDropsCount = 20000;
    const int streamLinesCount = 64;
    const int maxEmitQuantity = 127 * streamLinesCount;
    const int numThreadX = 8;
    const int numThreadY = 1;
    const int numThreadZ = 1;
    private int perlinT = 0;

    #endregion

    #region Stream lines

    public ComputeShader StreamsCS;
    public ComputeBuffer StreamLinesBuff;
    public Shader StreamLinesRenderShader;
    public Material StreamLinesMaterial;

    public GameObject[] Lines;

    #endregion

    #region Drop

    public ComputeShader DropsCS;
    public ComputeBuffer DropsBuff;
    public ComputeBuffer DeadBuff;
    public Shader DropsRenderShader;
    public Material DropsMaterial;

    public ComputeBuffer DeadArgsBuff;

    #endregion

    #region Noise

    public RenderTexture PerlinTexture;
    public Shader PerlinShader;
    public Material PerlinMaterial;

    #endregion
    
    Vector3[] GetParabolaPoints(Vector3 birthPos, Vector3 deathPos, int numDivision)
    {
        List<Vector3> result = new List<Vector3>();
        float a = (deathPos.y - birthPos.y) / Mathf.Pow((deathPos.z - birthPos.z), 2);

        for (int i = 0; i <= numDivision; i++)
        {
            float t = i / (float)numDivision;
            float x = t * (deathPos.z - birthPos.z);
            float y = a * (x - birthPos.z) * (x - birthPos.z) + birthPos.y;
            result.Add(new Vector3(birthPos.x, y, x));
        }
        return result.ToArray();
    }

    void Start ()
    {
        StreamLinesBuff = new ComputeBuffer(streamLinesCount, Marshal.SizeOf(typeof(StreamLine)));

        DeadBuff = new ComputeBuffer(maxDropsCount, sizeof(int), ComputeBufferType.Append);
        DeadBuff.SetCounterValue(0);
        
        DropsBuff = new ComputeBuffer(maxDropsCount, Marshal.SizeOf(typeof(Drop)));
        DeadArgsBuff = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);



        // Setup stream lines
        StreamLine[] streams = new StreamLine[streamLinesCount];
        for (int i = 0; i < streamLinesCount; i++)
        {
            streams[i].Id = i;
            streams[i].BirthPosition = new Vector3(Random.Range(EmitterSize.x + i * 0.2f, EmitterSize.x + i * 0.21f),
                                                   Random.Range(EmitterSize.y - 0.1f, EmitterSize.y + 0.1f),
                                                   Random.Range(EmitterSize.z - 0.1f, EmitterSize.z + 0.1f));
            streams[i].DeathPosition = new Vector3(streams[i].BirthPosition.x,
                                                   Random.Range(EliminatorSize.y - 0.1f, EliminatorSize.y + 0.1f),
                                                   Random.Range(EliminatorSize.z - 0.1f, EliminatorSize.z + 0.1f));
            streams[i].Position = streams[i].BirthPosition;

            var dz = streams[i].DeathPosition.z - streams[i].BirthPosition.z;
            var dy = streams[i].DeathPosition.y - streams[i].BirthPosition.y;

            streams[i].InitVelocity = new Vector3(0, Random.Range(-1.0f, 1.0f), -Mathf.Sqrt((g * dz * dz) / (2 * Mathf.Abs(dy))));
            streams[i].Velocity = streams[i].InitVelocity;
        }
        StreamLinesBuff.SetData(streams);
        StreamLinesMaterial = new Material(StreamLinesRenderShader);
        StreamLinesMaterial.hideFlags = HideFlags.HideAndDontSave;

        // Draw stream lines
        Lines = new GameObject[streamLinesCount];
        for (int i = 0; i < streamLinesCount; i++)
        {
            Lines[i] = new GameObject("Stream Line [" + i + "]");
            var lineRenderer = Lines[i].AddComponent<LineRenderer>();
            lineRenderer.material.shader = Shader.Find("Unlit/Color");
            lineRenderer.SetVertexCount(11);
            lineRenderer.SetWidth(0.01f, 0.01f);
            lineRenderer.SetPositions(GetParabolaPoints(streams[i].BirthPosition, streams[i].DeathPosition, 10));
        }
        streams = null;

        // Setup drops
        var drops = new Drop[maxDropsCount];
        for (int i = 0; i < maxDropsCount; i++)
        {
            drops[i].DropSize = 0.1f;
            drops[i].Position = Vector3.zero;
                
                //new Vector3(Random.Range(EmitterSize.x + drops[i].Id * 0.2f, EmitterSize.x + drops[i].Id * 0.21f),
                //                            Random.Range(EmitterSize.y - 0.1f, EmitterSize.y + 0.1f),
                //                            Random.Range(EmitterSize.z - 0.1f, EmitterSize.z + 0.1f));
            drops[i].Velocity = Vector3.down * 0.01f;
        }
        
        DropsBuff.SetData(drops);
        DropsMaterial = new Material(DropsRenderShader);
        DropsMaterial.hideFlags = HideFlags.HideAndDontSave;

        PerlinTexture = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32);
        PerlinTexture.hideFlags = HideFlags.DontSave;
        PerlinTexture.filterMode = FilterMode.Point;
        PerlinTexture.wrapMode = TextureWrapMode.Repeat;
        PerlinMaterial = new Material(PerlinShader);
        Graphics.Blit(null, PerlinTexture, PerlinMaterial, 0);

        // Drops
        int numThreadGroupDrops = maxDropsCount / numThreadX;

        // 0 : Init
        DropsCS.SetBuffer(0, "_DeadBuffAppend", DeadBuff);
        DropsCS.Dispatch(0, numThreadGroupDrops, 1, 1);
    }

    int GetDeadBuffSize()
    {
        int[] args = new int[] { 0, 1, 0, 0 };
        DeadArgsBuff.SetData(args);
        ComputeBuffer.CopyCount(DeadBuff, DeadArgsBuff, 0);
        DeadArgsBuff.GetData(args);
        return args[0];
    }

    //int oldCount;
    void OnRenderObject()
    {
        // Stream Lines
        StreamsCS.SetFloat("_DeltaTime", Time.deltaTime);
        StreamsCS.SetFloat("_Gravity", g);

        int numThreadGroupStreamLines = streamLinesCount / numThreadX;
        StreamsCS.SetBuffer(0, "_StreamLinesBuffer", StreamLinesBuff);
        StreamsCS.Dispatch(0, numThreadGroupStreamLines, 1, 1);

        // Drops
        int numThreadGroupDrops = maxDropsCount / numThreadX;
        DropsCS.SetInt("_StreamsCount", streamLinesCount);

        perlinT++;
        perlinT = perlinT % 64;
        DropsCS.SetInt("_PerlinT", perlinT);

        // 1 : Emit
        DropsCS.SetBuffer(1, "_DeadBuffConsume", DeadBuff);
        DropsCS.SetBuffer(1, "_DropsBuff", DropsBuff);
        DropsCS.SetTexture(1, "_PerlinTexture", PerlinTexture);
        int c = GetDeadBuffSize() > maxEmitQuantity ? numThreadGroupStreamLines : 0;
        DropsCS.Dispatch(1, c, 1, 1);

        //Debug.Log("c:" + c);
        //Debug.Log("diff" + (GetDeadBuffSize() - oldCount));
        //Debug.Log("GetDeadBuffSize:" + GetDeadBuffSize());
        //oldCount = GetDeadBuffSize();

        // 2 : Update
        DropsCS.SetBuffer(2, "_DropsBuff", DropsBuff);
        DropsCS.Dispatch(2, numThreadGroupDrops, 1, 1);

        // vert / geom / frag shader
        var inverseViewMatrix = BillboardCam.worldToCameraMatrix.inverse;

        StreamLinesMaterial.SetPass(0);
        StreamLinesMaterial.SetMatrix("_InvViewMatrix", inverseViewMatrix);
        StreamLinesMaterial.SetTexture("_DropTexture", DropTexture);
        StreamLinesMaterial.SetFloat("_DropSize", DropSize);
        StreamLinesMaterial.SetBuffer("_StreamLinesBuffer", StreamLinesBuff);
        Graphics.DrawProcedural(MeshTopology.Points, streamLinesCount);

        DropsMaterial.SetPass(0);
        DropsMaterial.SetMatrix("_InvViewMatrix", inverseViewMatrix);
        DropsMaterial.SetTexture("_DropTexture", DropTexture);
        DropsMaterial.SetFloat("_DropSize", DropSize);
        DropsMaterial.SetBuffer("_DropsBuff", DropsBuff);
        Graphics.DrawProcedural(MeshTopology.Points, maxDropsCount);
    }

    void OnDisable()
    {
        if (StreamLinesBuff != null)        StreamLinesBuff.Release();
        if (DropsBuff != null)              DropsBuff.Release();
        if (DeadBuff != null)               DeadBuff.Release();
        
        if (DeadArgsBuff != null)           DeadArgsBuff.Release();

        if (StreamLinesMaterial != null)    DestroyImmediate(StreamLinesMaterial);
        if (DropsMaterial != null)          DestroyImmediate(DropsMaterial);
    }
}
