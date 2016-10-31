using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;

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
    public Vector3 Gravity = new Vector3(0, 5.0f, 0f);
    public Vector3 AreaSize = new Vector3(1.0f, 15.0f, 1.0f);
    public Texture2D DropTexture;

    // Emitter parameters
    public Vector3 EmitterSize = new Vector3(0, 9, 0);
    public Vector3 EliminatorSize = new Vector3(0, 0, -3);

    [Range(0.0005f, 2.0f)]
    public float DropSize = 0.001f;

    [Range(0.01f, 10.0f)]
    public float g = 4.0f;

    const int numDrops = 20;
    const int numStreams = 32;
    const int numThreadX = 8;
    const int numThreadY = 1;
    const int numThreadZ = 1;

    public ComputeShader UpdateShader;
    public ComputeBuffer ParticlesBuffer;
    public Shader RenderShader;
    public Material ParticlesMaterial;
    public Camera BillboardCam;

    public StreamLine[] streams;

    void Start ()
    {
        ParticlesBuffer = new ComputeBuffer(numStreams, Marshal.SizeOf(typeof(StreamLine)));
        streams = new StreamLine[numStreams];

        for (int i = 0; i < numStreams; i++)
        {
            streams[i].Id = i;
            streams[i].BirthPosition = new Vector3(Random.Range(EmitterSize.x - 10, EmitterSize.x + 10),
                                                   Random.Range(EmitterSize.y - 0.1f, EmitterSize.y + 0.1f),
                                                   Random.Range(EmitterSize.z - 0.1f, EmitterSize.z + 0.1f));
            streams[i].DeathPosition = new Vector3(streams[i].BirthPosition.x,
                                                   Random.Range(EliminatorSize.y - 0.1f, EliminatorSize.y + 0.1f),
                                                   Random.Range(EliminatorSize.z - 0.1f, EliminatorSize.z + 0.1f));
            streams[i].Position = streams[i].BirthPosition;

            var dz = streams[i].DeathPosition.z - streams[i].BirthPosition.z;
            var dy = streams[i].DeathPosition.y - streams[i].BirthPosition.y;

            streams[i].InitVelocity = new Vector3(0, 0, -Mathf.Sqrt((g * dz * dz) / (2 * Mathf.Abs(dy))));
            streams[i].Velocity = streams[i].InitVelocity;

            Debug.Log(string.Format("Birth : {0}, Death : {1}", streams[i].BirthPosition, streams[i].DeathPosition));
        }

        //var drops = new Drop[numDrops];
        //for (int i = 0; i < numDrops; i++)
        //{
        //    drops[i].Id = i;
        //    drops[i].StreamId = Random.Range(0, numStreams + 1);
        //    drops[i].ParticleSize = DropSize;
        //}

        ParticlesBuffer.SetData(streams);
        //streams = null;

        ParticlesMaterial = new Material(RenderShader);
        ParticlesMaterial.hideFlags = HideFlags.HideAndDontSave;
	}

    void OnRenderObject()
    {
        for (int i = 0; i < numStreams; i++)
        {
            Debug.DrawLine(streams[i].BirthPosition, streams[i].DeathPosition, Color.green);
        }
        ComputeShader cs = UpdateShader;

        // スレッドグループ数を計算
        int numThreadGroup = numStreams / numThreadX;
        int kernelId = cs.FindKernel("CSMain");

        cs.SetFloat("_DeltaTime", Time.deltaTime);
        cs.SetVector("_Gravity", Gravity);
        cs.SetVector("_EmitterSize", EmitterSize);
        cs.SetVector("_EliminatorSize", EliminatorSize);
        cs.SetBuffer(kernelId, "_ParticlesBuffer", ParticlesBuffer);
        cs.Dispatch(kernelId, numThreadGroup, 1, 1);

        var inverseViewMatrix = BillboardCam.worldToCameraMatrix.inverse;

        Material m = ParticlesMaterial;
        m.SetPass(0);
        m.SetMatrix("_InvViewMatrix", inverseViewMatrix);
        m.SetTexture("_DropTexture", DropTexture);
        m.SetFloat("_DropSize", DropSize);
        m.SetBuffer("_ParticlesBuffer", ParticlesBuffer);

        Graphics.DrawProcedural(MeshTopology.Points, numStreams);

    }

    void OnDestroy()
    {
        if (ParticlesBuffer != null)
            ParticlesBuffer.Release();

        if (ParticlesMaterial != null)
            DestroyImmediate(ParticlesMaterial);
    }
}
