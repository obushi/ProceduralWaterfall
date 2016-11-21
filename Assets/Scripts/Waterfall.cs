using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace ProceduralWaterfall
{
    [System.Serializable]
    public struct Stream
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
        public Vector2 Age;
        public Vector3 Position;
        public Vector3 PrevPosition;
        public Vector4 Velocity;
        public Vector4 Params;
    }

    public class Waterfall : MonoBehaviour
    {

        #region Global parameters

        [SerializeField]
        bool showStreams = true;

        [SerializeField]
        bool showGui = true;

        [SerializeField]
        Camera billboardCam;

        [SerializeField]
        Texture2D dropTexture;

        [SerializeField]
        Texture2D guiBackground;
        
        [SerializeField]
        GameObject UrgDevice;
        Urg urg;

        #endregion

        #region Emitter parameters

        [Header("Emitter Params")]

        [SerializeField]
        static Vector3 waterfallSize = new Vector3(5, 30, 3);

        [SerializeField, Range(0.01f, 20.0f)]
        float g = 10;

        [SerializeField, Range(0.1f, 1.0f)]
        float jet = 1.0f;

        [Header("Drop / Splash Params")]

        [SerializeField]
        Vector4 duration = new Vector4(5.0f, 6.0f, 6.0f, 8.0f);

        [SerializeField]
        Vector4 collisionParams = new Vector4(230, 5.0f, 1, 0.17f);

        [SerializeField]
        Vector4 dropParams = new Vector4(1.0f, 1.0f, 1.0f, 0.015f);

        [SerializeField, Range(0.0005f, 0.2f)]
        float dropSize = 0.035f;

        [SerializeField]
        Vector4 splashParams = new Vector4(0.5f, 0.5f, 0.1f, 0.1f);

        [SerializeField, Range(0.0001f, 0.1f)]
        float splashSize = 0.01f;

        const int maxDropsCount = 2097152;
        const int streamsCount = 128;
        const int maxEmitQuantity = 512 * streamsCount;
        const int numThreadX = 128;
        const int numThreadY = 1;
        const int numThreadZ = 1;

        #endregion


        #region Streams

        [Header("Stream")]

        [SerializeField]
        Shader streamsRenderShader;

        Material streamMaterial;
        ComputeBuffer streamsBuffer;
        GameObject[] streamObjects;

        #endregion


        #region Drop

        [Header("Drops")]
        
        [SerializeField]
        ComputeShader dropsComputeShader;

        [SerializeField]
        Shader dropsRenderShader;

        ComputeBuffer dropsPoolBuffer;
        ComputeBuffer dropsBuffer;
        ComputeBuffer buffArgs;
        Material dropsMaterial;

        #endregion

        #region Detected Object

        [Header("Detected Objects")]
        public ComputeBuffer DetectedObstaclesBuffer;
        const int detectionLimit = 161;

        #endregion


        #region Noise

        [Header("Noise")]

        [SerializeField]
        Shader perlinShader;

        RenderTexture perlinTexture;
        Material perlinMaterial;

        #endregion


        void InitializeStreams()
        {
            Stream[] streams = new Stream[streamsCount];
            for (int i = 0; i < streamsCount; i++)
            {
                float gap = waterfallSize.x / streamsCount;
                streams[i].BirthPosition = new Vector3(-waterfallSize.x / 2 + i * gap,
                                                       Random.Range(waterfallSize.y / 2 - 0.1f, waterfallSize.y / 2 + 0.1f),
                                                       Random.Range(- 0.1f, 0.1f));
                streams[i].DeathPosition = new Vector3(streams[i].BirthPosition.x,
                                                       Random.Range(-waterfallSize.y / 2 - 0.1f, -waterfallSize.y / 2 + 0.1f),
                                                       Random.Range(-waterfallSize.z - 0.1f, -waterfallSize.z + 0.1f));
                streams[i].Position = streams[i].BirthPosition;

                var dz = streams[i].DeathPosition.z - streams[i].BirthPosition.z;
                var dy = streams[i].DeathPosition.y - streams[i].BirthPosition.y;

                streams[i].InitVelocity = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-1.0f, 1.0f), -Mathf.Sqrt((g * dz * dz) / (2 * Mathf.Abs(dy))));
                streams[i].Velocity = streams[i].InitVelocity;
            }
            streamsBuffer.SetData(streams);
            streamMaterial = new Material(streamsRenderShader);
            streamMaterial.hideFlags = HideFlags.HideAndDontSave;

            if (showStreams)
            {
                DrawStreams(streams);
            }

            streams = null;
        }

        void InitializeComputeBuffers()
        {
            streamsBuffer = new ComputeBuffer(streamsCount, Marshal.SizeOf(typeof(Stream)));
            DetectedObstaclesBuffer = new ComputeBuffer(detectionLimit, Marshal.SizeOf(typeof(Vector4)));
            buffArgs = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);

            dropsPoolBuffer = new ComputeBuffer(maxDropsCount, sizeof(uint), ComputeBufferType.Append);
            dropsPoolBuffer.SetCounterValue(0);
            dropsBuffer = new ComputeBuffer(maxDropsCount, Marshal.SizeOf(typeof(Drop)));
        }

        void InitializeDrops()
        {
            var drops = new Drop[maxDropsCount];
            for (int i = 0; i < maxDropsCount; i++)
            {
                drops[i].StreamId = 0;
                drops[i].Age = new Vector2(0, 0);
                drops[i].DropSize = 0.1f;
                drops[i].Position = Vector3.zero;
                drops[i].PrevPosition = Vector3.zero;
                drops[i].Velocity = new Vector4(0, -1, 0, 1);
                drops[i].Params = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);  // InitVhCoef, InitVvCoef, UpdatePosCoef, UpdateVelCoef
            }

            dropsBuffer.SetData(drops);
            dropsMaterial = new Material(dropsRenderShader);
            dropsMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        void DrawStreams(Stream[] streams)
        {
            streamObjects = new GameObject[streamsCount];
            for (int i = 0; i < streamsCount; i++)
            {
                streamObjects[i] = new GameObject("Stream Line [" + i + "]");
                var lineRenderer = streamObjects[i].AddComponent<LineRenderer>();
                lineRenderer.material.shader = Shader.Find("Unlit/Color");
                lineRenderer.SetVertexCount(11);
                lineRenderer.SetWidth(0.01f, 0.01f);
                lineRenderer.SetPositions(GetParabolaPoints(streams[i].BirthPosition, streams[i].DeathPosition, 10));
            }
        }

        void InitializeNoise()
        {
            perlinTexture = new RenderTexture(streamsCount, streamsCount, 0, RenderTextureFormat.ARGB32);
            perlinTexture.hideFlags = HideFlags.DontSave;
            perlinTexture.filterMode = FilterMode.Point;
            perlinTexture.wrapMode = TextureWrapMode.Repeat;
            perlinMaterial = new Material(perlinShader);
            Graphics.Blit(null, perlinTexture, perlinMaterial, 0);
        }

        int GetActiveBuffSize(ComputeBuffer cb)
        {
            int[] args = new int[] { 0, 1, 0, 0 };
            buffArgs.SetData(args);
            ComputeBuffer.CopyCount(cb, buffArgs, 0);
            buffArgs.GetData(args);
            return args[0];
        }

        ComputeBuffer GetActiveBuff(ComputeBuffer cb)
        {
            ComputeBuffer.CopyCount(cb, buffArgs, 0);
            return buffArgs;
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

            InitializeComputeBuffers();
            InitializeStreams();
            InitializeDrops();
            InitializeNoise();


            // Kernel #0 Initialize

            dropsComputeShader.SetBuffer(0, "_DropsPoolBuffer_In", dropsPoolBuffer);
            dropsComputeShader.Dispatch(0, maxDropsCount / numThreadX, 1, 1);
        }

        void Update()
        {

            RenderTexture rt = RenderTexture.GetTemporary(perlinTexture.width, perlinTexture.height, 0);
            Graphics.Blit(perlinTexture, rt, perlinMaterial, 0);
            Graphics.Blit(rt, perlinTexture);
            rt.Release();

            if (Input.GetKeyDown(KeyCode.G))
                showGui = !showGui;

            DetectedObstaclesBuffer.SetData(urg.DetectedObstacles);
        }


        void OnRenderObject()
        {
            // Constants

            dropsComputeShader.SetInt("_StreamsCount", streamsCount);
            dropsComputeShader.SetFloat("_DeltaTime", Time.deltaTime);
            dropsComputeShader.SetFloat("_Gravity", g);
            dropsComputeShader.SetFloat("_Jet", jet);
            dropsComputeShader.SetFloat("_RandSeed", Random.Range(1.0f, 5.0f));
            dropsComputeShader.SetVector("_Duration", duration);
            dropsComputeShader.SetVector("_CollisionParams", collisionParams);
            dropsComputeShader.SetVector("_DropParams", dropParams);
            dropsComputeShader.SetFloat("_DropSize", dropSize);
            dropsComputeShader.SetVector("_SplashParams", splashParams);
            dropsComputeShader.SetFloat("_SplashSize", splashSize);

            // Kernel #1 Emit

            dropsComputeShader.SetBuffer(1, "_DropsBuffer", dropsBuffer);
            dropsComputeShader.SetBuffer(1, "_DropsPoolBuffer_Out", dropsPoolBuffer);
            dropsComputeShader.SetBuffer(1, "_StreamLinesBuffer", streamsBuffer);
            dropsComputeShader.SetTexture(1, "_PerlinTexture", perlinTexture);
            var emitAmount = GetActiveBuffSize(dropsPoolBuffer) > maxEmitQuantity ? 1 : 0;
            dropsComputeShader.Dispatch(1, emitAmount, 1, 1);


            // Kernel #2 Update

            dropsComputeShader.SetBuffer(2, "_DropsBuffer", dropsBuffer);
            dropsComputeShader.SetBuffer(2, "_DropsPoolBuffer_In", dropsPoolBuffer);
            dropsComputeShader.SetBuffer(2, "_StreamLinesBuffer", streamsBuffer);
            dropsComputeShader.SetBuffer(2, "_DetectedObjectsBuffer", DetectedObstaclesBuffer);
            dropsComputeShader.Dispatch(2, maxDropsCount / numThreadX, 1, 1);


            // Render

            dropsMaterial.SetPass(0);
            var inverseViewMatrix = billboardCam.worldToCameraMatrix.inverse;
            dropsMaterial.SetMatrix("_InvViewMatrix", inverseViewMatrix);
            dropsMaterial.SetTexture("_DropTexture", dropTexture);
            dropsMaterial.SetBuffer("_DropsBuffer", dropsBuffer);
            Graphics.DrawProcedural(MeshTopology.Points, maxDropsCount);
        }

        void OnDisable()
        {
            if (streamsBuffer != null) streamsBuffer.Release();
            if (DetectedObstaclesBuffer != null) DetectedObstaclesBuffer.Release();
            if (buffArgs != null) buffArgs.Release();
            if (dropsBuffer != null) dropsBuffer.Release();
            if (dropsPoolBuffer != null) dropsPoolBuffer.Release();

            if (streamMaterial != null) DestroyImmediate(streamMaterial);
            if (dropsMaterial != null) DestroyImmediate(dropsMaterial);
        }

        void OnApplicationQuit()
        {
            if (urg.IsConnected)
                urg.Disconnect();
            urg.Release();
        }

        void OnGUI()
        {
            if (showGui)
            {
                GUIStyle style = new GUIStyle();
                GUIStyleState styleState = new GUIStyleState();
                styleState.background = guiBackground;
                styleState.textColor = Color.white;
                style.normal = styleState;

                GUILayout.BeginArea(new Rect(0, 0, 300, Screen.height), style);

                var urgStatus = urg.IsConnected ? "Connected" : "Not Connected";
                GUILayout.Label("URG Status :  " + urgStatus);

                urg.DrawMesh = GUILayout.Toggle(urg.DrawMesh, "Show URG Data");

                if (GUILayout.Button("Connect"))
                {
                    urg.Connect();
                }

                if (GUILayout.Button("Disconnect"))
                {
                    urg.Disconnect();
                }

                if (GUILayout.Button("Reload"))
                {
                    if (urg.IsConnected)
                    {
                        urg.Disconnect();
                    }
                    UnityEngine.SceneManagement.SceneManager.LoadScene(0);
                }

                GUILayout.Space(30);

                Vector3 pos = new Vector3();
                GUILayout.Label("URG Offset X :  " + urg.PosOffset.x);
                pos.x = GUILayout.HorizontalSlider(urg.PosOffset.x, -20, 20);

                GUILayout.Label("URG Offset Y :  " + urg.PosOffset.y);
                pos.y = GUILayout.HorizontalSlider(urg.PosOffset.y, -60, 60);
                urg.PosOffset = pos;

                GUILayout.Label("Scale :  " + urg.Scale.ToString("0.000"));
                urg.Scale = GUILayout.HorizontalSlider(urg.Scale, 0.001f, 0.2f);

                GUILayout.Space(30);

                GUILayout.Label("Waterfall Size X :  " + waterfallSize.x);
                waterfallSize.x = GUILayout.HorizontalSlider(waterfallSize.x, 1, 30);

                GUILayout.Label("Waterfall Size Y :  " + waterfallSize.y);
                waterfallSize.y = GUILayout.HorizontalSlider(waterfallSize.y, 10, 100);

                GUILayout.Label("Waterfall Size Z :  " + waterfallSize.z);
                waterfallSize.z = GUILayout.HorizontalSlider(waterfallSize.z, 1, 20);

                GUILayout.Space(30);

                GUILayout.Label("G :  " + g);
                g = GUILayout.HorizontalSlider(g, 0.1f, 20);

                GUILayout.Label("Jet  : " + jet);
                jet = GUILayout.HorizontalSlider(jet, 0, 1f);

                GUILayout.Label("Drop Duration :  " + duration.x.ToString("0.00") + "  ~  " + duration.y.ToString("0.00"));
                GUILayout.BeginHorizontal();
                duration.x = GUILayout.HorizontalSlider(duration.x, 1, 20);
                duration.y = GUILayout.HorizontalSlider(duration.y, 1, 40);
                GUILayout.EndHorizontal();

                GUILayout.Label("Splash Duration :  " + duration.z.ToString("0.00") + "  ~  " + duration.w.ToString("0.00"));
                GUILayout.BeginHorizontal();
                duration.z = GUILayout.HorizontalSlider(duration.z, 1, 20);
                duration.w = GUILayout.HorizontalSlider(duration.w, 1, 40);
                GUILayout.EndHorizontal();

                GUILayout.Label("Drop Size :  " + dropSize.ToString("0.000"));
                dropSize = GUILayout.HorizontalSlider(dropSize, 0.0005f, 0.2f);

                GUILayout.Label("Splash Size :  " + splashSize.ToString("0.000"));
                splashSize = GUILayout.HorizontalSlider(splashSize, 0.0001f, 0.1f);

                GUILayout.Space(30);

                GUILayout.Label("Collision Range :  " + collisionParams.w);
                collisionParams.w = GUILayout.HorizontalSlider(collisionParams.w, 0, 1.0f);

                GUILayout.Label("After Collision Speed Multiplier X :  " + collisionParams.x);
                collisionParams.x = GUILayout.HorizontalSlider(collisionParams.x, 0, 1000.0f);

                GUILayout.Label("After Collision Speed Multiplier Y :  " + collisionParams.y);
                collisionParams.y = GUILayout.HorizontalSlider(collisionParams.y, 0.0001f, 30.0f);

                GUILayout.Label("After Collision Speed Multiplier Z :  " + collisionParams.z);
                collisionParams.z = GUILayout.HorizontalSlider(collisionParams.z, 0.0001f, 3.0f);

                GUILayout.Label("Drop Count :  " + (maxDropsCount - GetActiveBuffSize(dropsPoolBuffer)) + " / " + maxDropsCount);
                GUILayout.Label("FPS : " + (1 / Time.deltaTime).ToString("0.00"));

                GUILayout.EndArea();
            }
        }
    }
}