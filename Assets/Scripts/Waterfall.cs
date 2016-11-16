using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace ProceduralWaterfall
{
    public struct StreamLine
    {
        public Vector3 BirthPosition;
        public Vector3 DeathPosition;
        public Vector3 Position;
        public Vector3 InitVelocity;
        public Vector3 Velocity;
    }

    [System.Serializable]
    public struct Drop
    {
        public uint StreamId;
        public float DropSize;
        public float Age;
        public Vector3 Position;
        public Vector3 PrevPosition;
        public Vector3 Velocity;
        public Vector4 Params;
    }

    public struct DetectedObject
    {
        public bool IsActive;
        public Vector3 Position;
    }

    public class Waterfall : MonoBehaviour
    {

        #region Global parameters

        public GameObject UrgDevice;
        private Urg urg;

        public DropCounter DropCounter;
        private DropCounter counterText;
        
        public Texture2D DropTexture;
        public bool showStreamLines = true;
        public Camera BillboardCam;


        #endregion

        #region Emitter parameters

        public Vector3 EmitterSize = new Vector3(0, 20, 0);
        public Vector3 EliminatorSize = new Vector3(0, 0, -3);

        //const int maxDropsCount = 4194304;
        const int maxDropsCount = 2097152;
        //const int maxDropsCount = 2048;
        const int streamLinesCount = 128;
        const int maxEmitQuantity = 1024 * streamLinesCount;
        const int numThreadX = 128;
        const int numThreadY = 1;
        const int numThreadZ = 1;

        [Range(0.01f, 10.0f)]
        public float g = 4.0f;

        [Range(0.1f, 1.0f)]
        public float Jet = 1.0f;

        [SerializeField, Header("Drop / Splash Params")]
        Vector4 dropParams = new Vector4(1.0f, 1.0f, 1.0f, 0.015f);

        [Range(0.0005f, 0.2f)]
        public float dropSize = 0.001f;

        [SerializeField]
        Vector4 splashParams = new Vector4(0.5f, 0.5f, 0.1f, 0.1f);

        [Range(0.0001f, 0.1f)]
        public float splashSize = 0.001f;

        #endregion

        #region Stream lines

        [Header("Stream Lines")]
        public ComputeBuffer StreamLinesBuffer;
        public Shader StreamLinesRenderShader;
        public Material StreamLinesMaterial;

        public GameObject[] Lines;

        #endregion

        #region Drop

        [Header("Drops")]

        public Drop[] dropsArray;
        public uint[] dropsIdArray;
        public ComputeShader DropsCS;

        public ComputeBuffer DropsPoolBuffer;
        public ComputeBuffer DropsBuffer;

        public Shader DropsRenderShader;
        public Material DropsMaterial;

        public ComputeBuffer BuffArgs;



        #endregion

        #region Detected Object

        [Header("Detected Objects")]
        public ComputeBuffer DetectedObjectsBuff;
        private DetectedObject[] detectedObjects;
        const int detectionLimit = 10;
        #endregion

        #region Noise

        [Header("Noise")]
        public RenderTexture PerlinTexture;
        public Shader PerlinShader;
        public Material PerlinMaterial;

        #endregion

        void InitializeStreamLines()
        {
            StreamLine[] streams = new StreamLine[streamLinesCount];
            for (int i = 0; i < streamLinesCount; i++)
            {
                streams[i].BirthPosition = new Vector3(EmitterSize.x + i * 0.05f,
                                                       Random.Range(EmitterSize.y - 0.1f, EmitterSize.y + 0.1f),
                                                       Random.Range(EmitterSize.z - 0.1f, EmitterSize.z + 0.1f));
                streams[i].DeathPosition = new Vector3(streams[i].BirthPosition.x,
                                                       Random.Range(EliminatorSize.y - 0.1f, EliminatorSize.y + 0.1f),
                                                       Random.Range(EliminatorSize.z - 0.1f, EliminatorSize.z + 0.1f));
                streams[i].Position = streams[i].BirthPosition;

                var dz = streams[i].DeathPosition.z - streams[i].BirthPosition.z;
                var dy = streams[i].DeathPosition.y - streams[i].BirthPosition.y;

                streams[i].InitVelocity = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-1.0f, 1.0f), -Mathf.Sqrt((g * dz * dz) / (2 * Mathf.Abs(dy))));
                streams[i].Velocity = streams[i].InitVelocity;
            }
            StreamLinesBuffer.SetData(streams);
            StreamLinesMaterial = new Material(StreamLinesRenderShader);
            StreamLinesMaterial.hideFlags = HideFlags.HideAndDontSave;

            if (showStreamLines)
            {
                DrawStreamLines(streams);
            }

            streams = null;
        }

        void InitializeComputeBuffers()
        {
            StreamLinesBuffer = new ComputeBuffer(streamLinesCount, Marshal.SizeOf(typeof(StreamLine)));
            DetectedObjectsBuff = new ComputeBuffer(detectionLimit, Marshal.SizeOf(typeof(DetectedObject)));
            BuffArgs = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);

            DropsPoolBuffer = new ComputeBuffer(maxDropsCount, sizeof(uint), ComputeBufferType.Append);
            DropsPoolBuffer.SetCounterValue(0);
            DropsBuffer = new ComputeBuffer(maxDropsCount, Marshal.SizeOf(typeof(Drop)));
        }

        void InitializeDrops()
        {
            var drops = new Drop[maxDropsCount];
            for (int i = 0; i < maxDropsCount; i++)
            {
                drops[i].StreamId = 0;
                drops[i].Age = 0;
                drops[i].DropSize = 0.1f;
                drops[i].Position = Vector3.zero;
                drops[i].PrevPosition = Vector3.zero;
                drops[i].Velocity = Vector3.down;
                drops[i].Params = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);  // InitVhCoef, InitVvCoef, UpdatePosCoef, UpdateVelCoef
            }

            DropsBuffer.SetData(drops);
            DropsMaterial = new Material(DropsRenderShader);
            DropsMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        void InitializeDetectedObjects()
        {
            detectedObjects = new DetectedObject[detectionLimit];
            for (int i = 0; i < detectionLimit; i++)
            {
                detectedObjects[i].IsActive = false;
                detectedObjects[i].Position = Vector3.zero;
            }

            DetectedObjectsBuff.SetData(detectedObjects);
        }

        void UpdateDetectedObjects()
        {
            var objectsFromUrg = urg.DetectedObjects;
            for (int i = 0; i < objectsFromUrg.Length; i++)
            {
                detectedObjects[i].IsActive = objectsFromUrg[i] != Vector3.zero;
                detectedObjects[i].Position = objectsFromUrg[i];
            }
            for (int i = detectionLimit - 1; i > objectsFromUrg.Length; i--)
            {
                detectedObjects[i].IsActive = false;
            }
            DetectedObjectsBuff.SetData(detectedObjects);
        }

        void DrawStreamLines(StreamLine[] streams)
        {
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
        }

        void InitializeNoise()
        {
            PerlinTexture = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32);
            PerlinTexture.hideFlags = HideFlags.DontSave;
            PerlinTexture.filterMode = FilterMode.Point;
            PerlinTexture.wrapMode = TextureWrapMode.Repeat;
            PerlinMaterial = new Material(PerlinShader);
            Graphics.Blit(null, PerlinTexture, PerlinMaterial, 0);
        }

        int GetActiveBuffSize(ComputeBuffer cb)
        {
            int[] args = new int[] { 0, 1, 0, 0 };
            BuffArgs.SetData(args);
            ComputeBuffer.CopyCount(cb, BuffArgs, 0);
            BuffArgs.GetData(args);
            return args[0];
        }

        ComputeBuffer GetActiveBuff(ComputeBuffer cb)
        {
            ComputeBuffer.CopyCount(cb, BuffArgs, 0);
            return BuffArgs;
        }

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

        void Start()
        {
            urg = UrgDevice.GetComponent<Urg>();
            counterText = DropCounter.GetComponent<DropCounter>();

            InitializeComputeBuffers();
            InitializeStreamLines();
            InitializeDrops();
            InitializeDetectedObjects();
            InitializeNoise();


            // Kernel #0 Initialize

            DropsCS.SetBuffer(0, "_DropsPoolBuffer_In", DropsPoolBuffer);
            DropsCS.Dispatch(0, maxDropsCount / numThreadX, 1, 1);
        }

        void Update()
        {

            RenderTexture rt = RenderTexture.GetTemporary(PerlinTexture.width, PerlinTexture.height, 0);
            Graphics.Blit(PerlinTexture, rt, PerlinMaterial, 0);
            Graphics.Blit(rt, PerlinTexture);
            rt.Release();

            UpdateDetectedObjects();
            counterText.GuiText = "Drop Count : " + (maxDropsCount - GetActiveBuffSize(DropsPoolBuffer)).ToString();
        }


        void OnRenderObject()
        {
            // Constants

            DropsCS.SetInt("_StreamsCount", streamLinesCount);
            DropsCS.SetFloat("_DeltaTime", Time.deltaTime);
            DropsCS.SetFloat("_Gravity", g);
            DropsCS.SetFloat("_Jet", Jet);
            DropsCS.SetFloat("_RandSeed", Random.Range(0, 1.0f));
            DropsCS.SetFloat("_DropLife", 5.0f);
            DropsCS.SetVector("_DropParams", dropParams);
            DropsCS.SetFloat("_DropSize", dropSize);
            DropsCS.SetVector("_SplashParams", splashParams);
            DropsCS.SetFloat("_SplashSize", splashSize);

            // Kernel #1 Emit

            DropsCS.SetBuffer(1, "_DropsBuffer", DropsBuffer);
            DropsCS.SetBuffer(1, "_DropsPoolBuffer_Out", DropsPoolBuffer);
            DropsCS.SetBuffer(1, "_StreamLinesBuffer", StreamLinesBuffer);
            DropsCS.SetTexture(1, "_PerlinTexture", PerlinTexture);
            var emitAmount = GetActiveBuffSize(DropsPoolBuffer) > maxEmitQuantity ? 1 : 0;
            DropsCS.Dispatch(1, emitAmount, 1, 1);


            // Kernel #2 Update

            DropsCS.SetBuffer(2, "_DropsBuffer", DropsBuffer);
            DropsCS.SetBuffer(2, "_DropsPoolBuffer_In", DropsPoolBuffer);
            DropsCS.SetBuffer(2, "_StreamLinesBuffer", StreamLinesBuffer);
            DropsCS.SetBuffer(2, "_DetectedObjBuff", DetectedObjectsBuff);
            DropsCS.Dispatch(2, maxDropsCount / numThreadX, 1, 1);


            // Render

            DropsMaterial.SetPass(0);
            var inverseViewMatrix = BillboardCam.worldToCameraMatrix.inverse;
            DropsMaterial.SetMatrix("_InvViewMatrix", inverseViewMatrix);
            DropsMaterial.SetTexture("_DropTexture", DropTexture);
            DropsMaterial.SetBuffer("_DropsBuff", DropsBuffer);
            Graphics.DrawProcedural(MeshTopology.Points, maxDropsCount);
        }

        void OnDisable()
        {
            if (StreamLinesBuffer != null) StreamLinesBuffer.Release();
            if (DetectedObjectsBuff != null) DetectedObjectsBuff.Release();
            if (BuffArgs != null) BuffArgs.Release();
            if (DropsBuffer != null) DropsBuffer.Release();
            if (DropsPoolBuffer != null) DropsPoolBuffer.Release();

            if (StreamLinesMaterial != null) DestroyImmediate(StreamLinesMaterial);
            if (DropsMaterial != null) DestroyImmediate(DropsMaterial);
        }
    }
}