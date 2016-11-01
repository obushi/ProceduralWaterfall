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
    public int Id;
    public int StreamId;
    public float ParticleSize;
    public Vector3 DropSize;
    public Vector3 Position;
    public Vector3 Velocity;
}

public class Waterfall : MonoBehaviour {

    // Global parameters
    public Vector3 AreaSize = new Vector3(1.0f, 15.0f, 1.0f);
    public Texture2D DropTexture;

    // Emitter parameters
    public Vector3 EmitterSize = new Vector3(0, 9, 0);
    public Vector3 EliminatorSize = new Vector3(0, 0, -3);

    [Range(0.0005f, 2.0f)]
    public float DropSize = 0.001f;

    [Range(0.01f, 10.0f)]
    public float g = 4.0f;

    const int numDrops = 2000;
    const int numStreams = 32;
    const int numThreadX = 8;
    const int numThreadY = 1;
    const int numThreadZ = 1;

    public ComputeShader UpdateShader;
    public ComputeBuffer StreamLinesBuffer;
    public ComputeBuffer DropsBuffer;
    public Shader StreamLinesRenderShader;
    public Material StreamLinesMaterial;
    public Shader DropsRenderShader;
    public Material DropsMaterial;
    public Camera BillboardCam;

    public GameObject[] Lines;

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
        StreamLinesBuffer = new ComputeBuffer(numStreams, Marshal.SizeOf(typeof(StreamLine)));
        DropsBuffer = new ComputeBuffer(numDrops, Marshal.SizeOf(typeof(Drop)));

        // Setup stream lines
        StreamLine[] streams = new StreamLine[numStreams];
        for (int i = 0; i < numStreams; i++)
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
        StreamLinesBuffer.SetData(streams);
        StreamLinesMaterial = new Material(StreamLinesRenderShader);
        StreamLinesMaterial.hideFlags = HideFlags.HideAndDontSave;

        //Draw stream lines
        Lines = new GameObject[numStreams];
        for (int i = 0; i < numStreams; i++)
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
        var drops = new Drop[numDrops];
        for (int i = 0; i < numDrops; i++)
        {
            drops[i].Id = i % (numStreams + 1);
            drops[i].StreamId = (int)Random.Range(0, numStreams - float.Epsilon);
            drops[i].ParticleSize = DropSize;
            drops[i].Position = new Vector3(Random.Range(EmitterSize.x + drops[i].Id * 0.2f, EmitterSize.x + drops[i].Id * 0.21f),
                                            Random.Range(EmitterSize.y - 0.1f, EmitterSize.y + 0.1f),
                                            Random.Range(EmitterSize.z - 0.1f, EmitterSize.z + 0.1f));
            drops[i].Velocity = Vector3.down;
        }
        DropsBuffer.SetData(drops);
        DropsMaterial = new Material(DropsRenderShader);
        DropsMaterial.hideFlags = HideFlags.HideAndDontSave;
    }

    void OnRenderObject()
    {
        // compute shader
        ComputeShader cs = UpdateShader;

        cs.SetFloat("_DeltaTime", Time.deltaTime);
        cs.SetFloat("_Gravity", g);
        cs.SetVector("_EmitterSize", EmitterSize);
        cs.SetVector("_EliminatorSize", EliminatorSize);

        // Stream Lines
        int numThreadGroupStreamLines = numStreams / numThreadX;
        int kernelIdStreamLines = cs.FindKernel("CSStreamLines");
        cs.SetBuffer(kernelIdStreamLines, "_StreamLinesBuffer", StreamLinesBuffer);
        cs.Dispatch(kernelIdStreamLines, numThreadGroupStreamLines, 1, 1);

        // Drops
        int numThreadGroupDrops = numDrops / numThreadX;
        int kernelIdDrops = cs.FindKernel("CSDrops");
        cs.SetBuffer(kernelIdDrops, "_DropsBuffer", DropsBuffer);
        cs.Dispatch(kernelIdDrops, numThreadGroupDrops, 1, 1);

        // vert / geom / frag shader
        var inverseViewMatrix = BillboardCam.worldToCameraMatrix.inverse;

        Material m1 = StreamLinesMaterial;
        m1.SetPass(0);
        m1.SetMatrix("_InvViewMatrix", inverseViewMatrix);
        m1.SetTexture("_DropTexture", DropTexture);
        m1.SetFloat("_DropSize", DropSize);
        m1.SetBuffer("_StreamLinesBuffer", StreamLinesBuffer);
        Graphics.DrawProcedural(MeshTopology.Points, numStreams);

        Material m2 = DropsMaterial;
        m2.SetPass(0);
        m2.SetMatrix("_InvViewMatrix", inverseViewMatrix);
        m2.SetTexture("_DropTexture", DropTexture);
        m2.SetFloat("_DropSize", DropSize);
        m2.SetBuffer("_DropsBuffer", DropsBuffer);

        //Graphics.DrawProcedural(MeshTopology.Points, numDrops);

    }

    void OnDestroy()
    {
        if (StreamLinesBuffer != null)
            StreamLinesBuffer.Release();

        if (DropsBuffer != null)
            DropsBuffer.Release();

        if (StreamLinesMaterial != null)
            DestroyImmediate(StreamLinesMaterial);
    }
}
