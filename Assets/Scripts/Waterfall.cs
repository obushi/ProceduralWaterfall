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
    public Shader RenderShader;
    public Material ParticlesMaterial;
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

        var drops = new Drop[numDrops];
        for (int i = 0; i < numDrops; i++)
        {
            drops[i].Id = i;
            drops[i].StreamId = -1;
            drops[i].ParticleSize = DropSize;
        }
        DropsBuffer.SetData(drops);

        ParticlesMaterial = new Material(RenderShader);
        ParticlesMaterial.hideFlags = HideFlags.HideAndDontSave;

        Lines = new GameObject[numStreams];
        for (int i = 0; i < numStreams; i++)
        {
            Lines[i] = new GameObject();
            var lineRenderer = Lines[i].AddComponent<LineRenderer>();
            lineRenderer.material.shader = Shader.Find("Unlit/Color");
            lineRenderer.SetVertexCount(11);
            lineRenderer.SetWidth(0.1f, 0.1f);
            lineRenderer.SetPositions(GetParabolaPoints(streams[i].BirthPosition, streams[i].DeathPosition, 10));
        }
        streams = null;
    }

    void OnRenderObject()
    {
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

        var inverseViewMatrix = BillboardCam.worldToCameraMatrix.inverse;

        Material m = ParticlesMaterial;
        m.SetPass(0);
        m.SetMatrix("_InvViewMatrix", inverseViewMatrix);
        m.SetTexture("_DropTexture", DropTexture);
        m.SetFloat("_DropSize", DropSize);
        m.SetBuffer("_StreamLinesBuffer", StreamLinesBuffer);

        Graphics.DrawProcedural(MeshTopology.Points, numStreams);

    }

    void OnDestroy()
    {
        if (StreamLinesBuffer != null)
            StreamLinesBuffer.Release();

        if (DropsBuffer != null)
            DropsBuffer.Release();

        if (ParticlesMaterial != null)
            DestroyImmediate(ParticlesMaterial);
    }
}
