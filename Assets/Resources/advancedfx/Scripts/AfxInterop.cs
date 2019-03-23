using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

/*
 
QuakeRoll(r) =
/ 1,  0,   0, 0 \
| 0, cr, -sr, 0 |
| 0, sr,  cr, 0 |
\ 0,  0,   0, 1 /

QuakePitch(p) =
/ cp, 0, sp, 0 \
|  0, 1,  0, 0 |
|-sp, 0, cp, 0 |
\  0, 0,  0, 1 /

QuakeYaw(y) =
/ cy, -sy, 0, 0 \
| sy,  cy, 0, 0 |
|  0,   0, 1, 0 |
\  0,   0, 0, 1 /

QuakeScale(a,b,c) =
/ a, 0, 0, 0 \
| 0, b, 0, 0 |
| 0, 0, c, 0 |
\ 0, 0, 0, 1 /

QuakeTranslate(u,v,w) =
/ 1, 0, 0, u \
| 0, 1, 0, v |
| 0, 0, 1, w |
\ 0, 0, 0, 1 /

QuakeRTS = QuakeYaw(y) * QuakePitch(p) * QuakeRoll(r) * QuakeTranslate(u,v,w) * QuakeScale(a,b,c)

u =  100 / 2.54 * unityZ
v = -100 / 2.54 * unityX
w =  100 / 2.54 * unityY

*/

// TODO: Might leak texture handles.

public class AfxInterop : MonoBehaviour
{
    public string pipeName = "advancedfxInterop";

    public volatile bool suspended = false;

    public Camera afxOverrideCamera = null;

    //

    public void Awake() {

        if (IntPtr.Zero == GetModuleHandle("AfxHookUnity.dll"))
        {
            Debug.LogError("AfxHookUnity.dll is not injected. It needs to be injected into Unity early.");
        }

        if (!AfxHookUnityInit(3))
            Debug.LogError("AfxHookUnityInit failed (version mismatch or init failed).");

        Application.runInBackground = true; // don't sleep when not having focus
        QualitySettings.vSyncCount = 0; // render as fast as possible

        this.drawDepthMaterial = Resources.Load("advancedfx/Materials/DrawDepth") as Material;

        afxDrawBegin = new CommandBuffer();
        afxDrawBegin.name = "AfxHookUnity: AfxDrawBeginCallBack.";
        afxDrawBegin.IssuePluginEvent(AfxHookUnityGetRenderEventFunc(), 2);
        afxDrawBegin.SetInvertCulling(true);

        afxDrawEnd = new CommandBuffer();
        afxDrawEnd.name = "AfxHookUnity: AfxDrawEndCallBack.";
        afxDrawEnd.SetInvertCulling(false);
        afxDrawEnd.IssuePluginEvent(AfxHookUnityGetRenderEventFunc(), 3);

        m_AfxInteropCommandsDelegate += AfxInteropCommands;
        m_AfxInteropRenderDelegate += AfxInteropRender;
    }

    public void OnEnable() {

        AfxInteropCreate(pipeName, m_AfxInteropCommandsDelegate, m_AfxInteropRenderDelegate);
    }

    public void OnDisable() {

        AfxInteropDestroy();

        if (null != m_ReplacementSurface)
        {
            m_ReplacementSurface.Dispose();
            m_ReplacementSurface = null;
        }
    }


    public void Start()
    {
        afxCamera = GetComponent<Camera>();

        if (null != afxCamera)
        {
            afxCamera.allowHDR = false;
            afxCamera.allowMSAA = false;
            afxCamera.allowDynamicResolution = false;
        }
        else Debug.LogError("AfxInterop component must be attached to a Camera component.", this);
    }

    public void Update()
    {
        AfxInteropUpdateEngineThread();
    }

    void LateUpdate()
    {
        if (!afxCameraUpdated) return;

        afxCameraUpdated = false;

        Graphics.ExecuteCommandBuffer(afxDrawBegin);

        if (null != afxDrawDepth) afxCamera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, afxDrawDepth);

        //

        afxCamera.Render();

        //

        if (null != afxDrawDepth) { afxCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, afxDrawDepth); afxDrawDepth = null; }

        Graphics.ExecuteCommandBuffer(afxDrawEnd);

        this.drawDepthMaterial.mainTexture = null;
        afxCamera.targetTexture = null;
        afxCamera.clearFlags = afxOldCameraClearFlags;
        afxCamera.ResetProjectionMatrix();
        afxCamera.ResetAspect();
    }

    //
    // Private:

    public enum D3DFORMAT : UInt32
    {
        D3DFMT_UNKNOWN = 0,

        D3DFMT_R8G8B8 = 20,
        D3DFMT_A8R8G8B8 = 21,
        D3DFMT_X8R8G8B8 = 22,
        D3DFMT_R5G6B5 = 23,
        D3DFMT_X1R5G5B5 = 24,
        D3DFMT_A1R5G5B5 = 25,
        D3DFMT_A4R4G4B4 = 26,
        D3DFMT_R3G3B2 = 27,
        D3DFMT_A8 = 28,
        D3DFMT_A8R3G3B2 = 29,
        D3DFMT_X4R4G4B4 = 30,
        D3DFMT_A2B10G10R10 = 31,
        D3DFMT_A8B8G8R8 = 32,
        D3DFMT_X8B8G8R8 = 33,
        D3DFMT_G16R16 = 34,
        D3DFMT_A2R10G10B10 = 35,
        D3DFMT_A16B16G16R16 = 36,

        D3DFMT_A8P8 = 40,
        D3DFMT_P8 = 41,

        D3DFMT_L8 = 50,
        D3DFMT_A8L8 = 51,
        D3DFMT_A4L4 = 52,

        D3DFMT_V8U8 = 60,
        D3DFMT_L6V5U5 = 61,
        D3DFMT_X8L8V8U8 = 62,
        D3DFMT_Q8W8V8U8 = 63,
        D3DFMT_V16U16 = 64,
        D3DFMT_A2W10V10U10 = 67,

        //D3DFMT_UYVY                 = MAKEFOURCC('U', 'Y', 'V', 'Y'),
        //D3DFMT_R8G8_B8G8            = MAKEFOURCC('R', 'G', 'B', 'G'),
        //D3DFMT_YUY2                 = MAKEFOURCC('Y', 'U', 'Y', '2'),
        //DFMT_G8R8_G8B8            = MAKEFOURCC('G', 'R', 'G', 'B'),
        //D3DFMT_DXT1                 = MAKEFOURCC('D', 'X', 'T', '1'),
        //D3DFMT_DXT2                 = MAKEFOURCC('D', 'X', 'T', '2'),
        //D3DFMT_DXT3                 = MAKEFOURCC('D', 'X', 'T', '3'),
        //D3DFMT_DXT4                 = MAKEFOURCC('D', 'X', 'T', '4'),
        //D3DFMT_DXT5                 = MAKEFOURCC('D', 'X', 'T', '5'),

        D3DFMT_D16_LOCKABLE = 70,
        D3DFMT_D32 = 71,
        D3DFMT_D15S1 = 73,
        D3DFMT_D24S8 = 75,
        D3DFMT_D24X8 = 77,
        D3DFMT_D24X4S4 = 79,
        D3DFMT_D16 = 80,

        D3DFMT_D32F_LOCKABLE = 82,
        D3DFMT_D24FS8 = 83,

        //#if !defined(D3D_DISABLE_9EX)
        D3DFMT_D32_LOCKABLE = 84,
        D3DFMT_S8_LOCKABLE = 85,
        //#endif // !D3D_DISABLE_9EX

        D3DFMT_L16 = 81,

        D3DFMT_VERTEXDATA = 100,
        D3DFMT_INDEX16 = 101,
        D3DFMT_INDEX32 = 102,

        D3DFMT_Q16W16V16U16 = 110,

        //D3DFMT_MULTI2_ARGB8         = MAKEFOURCC('M','E','T','1'),

        D3DFMT_R16F = 111,
        D3DFMT_G16R16F = 112,
        D3DFMT_A16B16G16R16F = 113,

        D3DFMT_R32F = 114,
        D3DFMT_G32R32F = 115,
        D3DFMT_A32B32G32R32F = 116,

        D3DFMT_CxV8U8 = 117,

        //#if !defined(D3D_DISABLE_9EX)
        D3DFMT_A1 = 118,
        D3DFMT_A2B10G10R10_XR_BIAS = 119,
        D3DFMT_BINARYBUFFER = 199,
        //#endif // !D3D_DISABLE_9EX

        D3DFMT_INTZ = 1515474505,

        //D3DFMT_FORCE_DWORD          =0x7fffffff
    };

    public enum D3DPOOL : UInt32
    {
        D3DPOOL_DEFAULT = 0,
        D3DPOOL_MANAGED = 1,
        D3DPOOL_SYSTEMMEM = 2,
        D3DPOOL_SCRATCH = 3,

        //D3DPOOL_FORCE_DWORD = 0x7fffffff
    }

    public enum D3DMULTISAMPLE_TYPE
    {
        D3DMULTISAMPLE_NONE = 0,
        D3DMULTISAMPLE_NONMASKABLE = 1,
        D3DMULTISAMPLE_2_SAMPLES = 2,
        D3DMULTISAMPLE_3_SAMPLES = 3,
        D3DMULTISAMPLE_4_SAMPLES = 4,
        D3DMULTISAMPLE_5_SAMPLES = 5,
        D3DMULTISAMPLE_6_SAMPLES = 6,
        D3DMULTISAMPLE_7_SAMPLES = 7,
        D3DMULTISAMPLE_8_SAMPLES = 8,
        D3DMULTISAMPLE_9_SAMPLES = 9,
        D3DMULTISAMPLE_10_SAMPLES = 10,
        D3DMULTISAMPLE_11_SAMPLES = 11,
        D3DMULTISAMPLE_12_SAMPLES = 12,
        D3DMULTISAMPLE_13_SAMPLES = 13,
        D3DMULTISAMPLE_14_SAMPLES = 14,
        D3DMULTISAMPLE_15_SAMPLES = 15,
        D3DMULTISAMPLE_16_SAMPLES = 16,
        //D3DMULTISAMPLE_FORCE_DWORD = 0xffffffff
    }

    [Flags]
    public enum D3DUSAGE : UInt32
    {
        D3DUSAGE_RENDERTARGET = 0x00000001,
        D3DUSAGE_DEPTHSTENCIL = 0x00000002,
        D3DUSAGE_DYNAMIC = 0x00000200
    }

    public struct Afx4x4
    {
        public Single M00;
        public Single M01;
        public Single M02;
        public Single M03;
        public Single M10;
        public Single M11;
        public Single M12;
        public Single M13;
        public Single M20;
        public Single M21;
        public Single M22;
        public Single M23;
        public Single M30;
        public Single M31;
        public Single M32;
        public Single M33;
    }

    public interface ISurfaceInfo
    {
        IntPtr Id { get; }
        UInt32 Width { get; }
        UInt32 Height { get; }
        D3DUSAGE Usage { get; }
        D3DFORMAT Format { get; }
        D3DPOOL Pool { get; }
        D3DMULTISAMPLE_TYPE MultiSampleType { get; }
        UInt32 MultiSampleQuality { get; }
    }

    public class SurfaceInfo : ISurfaceInfo
    {
        IntPtr ISurfaceInfo.Id { get { return m_Id; } }
        UInt32 ISurfaceInfo.Width { get { return m_Width; } }
        UInt32 ISurfaceInfo.Height { get { return m_Height; } }
        D3DUSAGE ISurfaceInfo.Usage { get { return m_Usage; } }
        D3DFORMAT ISurfaceInfo.Format { get { return m_Format; } }
        D3DPOOL ISurfaceInfo.Pool { get { return m_Pool; } }
        D3DMULTISAMPLE_TYPE ISurfaceInfo.MultiSampleType { get { return m_MultiSampleType; } }
        UInt32 ISurfaceInfo.MultiSampleQuality { get { return m_MultiSampleQuality; } }

        public IntPtr Id { get { return m_Id; } set { m_Id = value; } }
        public UInt32 Width { get { return m_Width; } set { m_Width = value; } }
        public UInt32 Height { get { return m_Height; } set { m_Height = value; } }
        public D3DUSAGE Usage { get { return m_Usage; } set { m_Usage = value; } }
        public D3DFORMAT Format { get { return m_Format; } set { m_Format = value; } }
        public D3DPOOL Pool { get { return m_Pool; } set { m_Pool = value; } }
        public D3DMULTISAMPLE_TYPE MultiSampleType { get { return m_MultiSampleType; } set { m_MultiSampleType = value; } }
        public UInt32 MultiSampleQuality { get { return m_MultiSampleQuality; } set { m_MultiSampleQuality = value; } }

        private IntPtr m_Id;
        private UInt32 m_Width;
        private UInt32 m_Height;
        private D3DUSAGE m_Usage;
        private D3DFORMAT m_Format;
        private D3DPOOL m_Pool;
        private D3DMULTISAMPLE_TYPE m_MultiSampleType;
        private UInt32 m_MultiSampleQuality;
    }

    private Material drawDepthMaterial;

    private bool afxCameraUpdated = false;
    private Camera afxCamera;
    private CommandBuffer afxDrawBegin;
    private CommandBuffer afxDrawDepth;
    private CommandBuffer afxDrawEnd;
    private CameraClearFlags afxOldCameraClearFlags;

    SurfaceData m_ReplacementSurface;

    AfxInteropCommandsDelegate m_AfxInteropCommandsDelegate;
    AfxInteropRenderDelegate m_AfxInteropRenderDelegate;


    [StructLayout(LayoutKind.Sequential)]
    public struct AfxInteropMatrix4x4
    {
        public Single M00;
        public Single M01;
        public Single M02;
        public Single M03;
        public Single M10;
        public Single M11;
        public Single M12;
        public Single M13;
        public Single M20;
        public Single M21;
        public Single M22;
        public Single M23;
        public Single M30;
        public Single M31;
        public Single M32;
        public Single M33;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AfxInteropRenderInfo
    {
        public Int32 Width;
        public Int32 Height;
        public AfxInteropMatrix4x4 ViewMatrix;
        public AfxInteropMatrix4x4 ProjectionMatrix;
        public Int32 FrameCount;
        public Single AbsoluteFrameTime;
        public Single CurTime;
        public Single FrameTime;
    }

    delegate void AfxInteropCommandsDelegate(IntPtr commands);

    delegate void AfxInteropRenderDelegate(ref AfxInteropRenderInfo renderInfo, out bool outColorTextureWasLost, out IntPtr outSharedColorTextureHandle, out bool outColorDepthTextureWasLost, out IntPtr outSharedColorDepthTextureHandle);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport ("AfxHookUnity")]
	private static extern bool AfxHookUnityInit(int version);

    [DllImport("AfxHookUnity")]
    private static extern IntPtr AfxHookUnityGetSharedHandle(IntPtr d3d11ResourcePtr);

    [DllImport("AfxHookUnity")]
    private static extern void AfxHookUnityBeginCreateRenderTexture();

    [DllImport("AfxHookUnity")]
    private static extern IntPtr AfxHookUnityGetRenderEventFunc();

    [DllImport("AfxHookUnity")]
    private static extern IntPtr AfxHookUnityGetRenderEventAndDataFunc();

    [DllImport("AfxHookUnity")]
    private static extern UInt32 AfxInteropCommands_GetCommandCount(IntPtr commands);

    [DllImport("AfxHookUnity")]
    private static extern UInt32 AfxInteropCommands_GetCommandArgCount(IntPtr commands, UInt32 index);

    [DllImport("AfxHookUnity")]
    [return: MarshalAs(UnmanagedType.LPStr)]
    private static extern string AfxInteropCommands_GetCommandArg(IntPtr commands, UInt32 index, UInt32 argIndex);

    [DllImport("AfxHookUnity")]
    private static extern void AfxInteropDestroy();

    [DllImport("AfxHookUnity")]
    private static extern bool AfxInteropCreate([MarshalAs(UnmanagedType.LPStr)] string pipeName, AfxInteropCommandsDelegate afxInteropCommands, AfxInteropRenderDelegate afxInteropRender);

    [DllImport("AfxHookUnity")]
    private static extern bool AfxInteropUpdateEngineThread();

    [DllImport("AfxHookUnity")]
    private static extern bool AfxInteropScheduleCommand([MarshalAs(UnmanagedType.LPStr)] string command);

    private class SurfaceData : IDisposable
    {
        public SurfaceData(ISurfaceInfo surfaceInfo)
        {
            this.surfaceInfo = surfaceInfo;

            RenderTexture colorTexture = null;
            IntPtr sharedColorTextureHandle = IntPtr.Zero;
            RenderTexture depthTexture = null;
            IntPtr sharedDepthTextureHandle = IntPtr.Zero;

            Nullable<RenderTextureDescriptor> rdesc;
            
            rdesc = GetRenderTextureDescriptor(surfaceInfo, false);
            if (rdesc.HasValue)
            {
                AfxHookUnityBeginCreateRenderTexture();
                colorTexture = new RenderTexture(rdesc.Value);
                colorTexture.Create();
                sharedColorTextureHandle = AfxHookUnityGetSharedHandle(colorTexture.GetNativeTexturePtr());
                Debug.Log("Color: " + colorTexture.GetNativeTexturePtr() + " -> " + sharedColorTextureHandle);
            }

            rdesc = GetRenderTextureDescriptor(surfaceInfo, true);
            if (rdesc.HasValue)
            {
                AfxHookUnityBeginCreateRenderTexture();
                depthTexture = new RenderTexture(rdesc.Value);
                depthTexture.Create();
                sharedDepthTextureHandle = AfxHookUnityGetSharedHandle(depthTexture.GetNativeTexturePtr());
                Debug.Log("Depth: " + depthTexture.GetNativeTexturePtr() + " -> " + sharedDepthTextureHandle);
            }

            this.colorTexture = colorTexture;
            this.sharedColorTextureHandle = sharedColorTextureHandle;
            this.depthTexture = depthTexture;
            this.sharedDepthTextureHandle = sharedDepthTextureHandle;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        bool disposed = false;

        protected virtual void Dispose(bool disposing)
        {

            if (disposed || !disposing) return;

            if (colorTexture) colorTexture.Release();
            if (depthTexture) depthTexture.Release();

            disposed = true;
        }

        public readonly ISurfaceInfo surfaceInfo;
        public readonly RenderTexture colorTexture;
        public readonly IntPtr sharedColorTextureHandle;
        public readonly RenderTexture depthTexture;
        public readonly IntPtr sharedDepthTextureHandle;

        private Nullable<RenderTextureDescriptor> GetRenderTextureDescriptor(ISurfaceInfo fbSurfaceInfo, bool isDepth)
        {
            RenderTextureDescriptor desc = new RenderTextureDescriptor((int)fbSurfaceInfo.Width, (int)fbSurfaceInfo.Height);

            Debug.Log("GetRenderTextureDescriptor: " + fbSurfaceInfo.Id + "(" + (isDepth ? "depth" : "color") +")" + ": " + fbSurfaceInfo.Format + " (" + fbSurfaceInfo.Width + "," + fbSurfaceInfo.Height + ")");

            switch (fbSurfaceInfo.Format)
            {
                case D3DFORMAT.D3DFMT_A8R8G8B8:
                    desc.colorFormat = RenderTextureFormat.BGRA32;
                    break;
                default:
                    Debug.LogError("Unknown back buffer format: " + fbSurfaceInfo.Format);
                    return null;
            }

            desc.depthBufferBits = isDepth ? 0 : 32;

            desc.autoGenerateMips = false;
            desc.bindMS = false;
            desc.enableRandomWrite = false;
            desc.sRGB = false; // for windowed CS:GO at least (?).
            desc.msaaSamples = 1;
            desc.useMipMap = false;

            return new Nullable<RenderTextureDescriptor>(desc);
        }
    }

    void AfxInteropCommands(IntPtr commands)
    {
        // Exceptions must not make it into native code (this is called from native code):
        try
        {
            UInt32 commandCount = AfxInteropCommands_GetCommandCount(commands);

            for (UInt32 i = 0; i < commandCount; ++i)
            {
                UInt32 argCount = AfxInteropCommands_GetCommandArgCount(commands, i);

                if (0 < argCount)
                {
                    string arg0 = AfxInteropCommands_GetCommandArg(commands, i, 0);

                    if (2 <= argCount)
                    {
                        string arg1 = AfxInteropCommands_GetCommandArg(commands, i, 1);

                        if (0 == arg1.CompareTo("afx"))
                        {
                            if (4 == commandCount)
                            {
                                string arg2 = AfxInteropCommands_GetCommandArg(commands, i, 2);
                                string arg3 = AfxInteropCommands_GetCommandArg(commands, i, 3);

                                if (0 == arg2.CompareTo("suspended"))
                                {
                                    int value;

                                    if (int.TryParse(arg3, out value))
                                        this.suspended = 0 != value;

                                    continue;
                                }
                            }
                        }
                    }

                    AfxInteropScheduleCommand("echo " + arg0 + " afx suspended 0|1 - Suspend / resume rendering.\n");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
    }

    void AfxInteropRender(ref AfxInteropRenderInfo renderInfo, out bool outColorTextureWasLost, out IntPtr outSharedColorTextureHandle, out bool outColorDepthTextureWasLost, out IntPtr outSharedColorDepthTextureHandle)
    {
        // Exceptions must not make it into native code (this is called from native code):
        try
        {
            float absoluteFrameTime = renderInfo.AbsoluteFrameTime;
            float frameTime = renderInfo.FrameTime;

            //Debug.Log(absoluteFrameTime + " / " + frameTime);

            Time.timeScale = 0 == Time.unscaledDeltaTime ? 1 : frameTime / Time.unscaledDeltaTime;

            bool wasLost = false;

            if (null != m_ReplacementSurface && (m_ReplacementSurface.colorTexture.width != renderInfo.Width || m_ReplacementSurface.colorTexture.height != renderInfo.Height))
            {
                m_ReplacementSurface.Dispose();
                m_ReplacementSurface = null;
            }

            if (null == m_ReplacementSurface)
            {
                wasLost = true;

                SurfaceInfo surfaceInfo = new SurfaceInfo();
                surfaceInfo.Id = IntPtr.Zero;
                surfaceInfo.Width = (UInt32)renderInfo.Width;
                surfaceInfo.Height = (UInt32)renderInfo.Height;
                surfaceInfo.Usage = D3DUSAGE.D3DUSAGE_RENDERTARGET;
                surfaceInfo.Format = D3DFORMAT.D3DFMT_A8R8G8B8;
                surfaceInfo.Pool = D3DPOOL.D3DPOOL_DEFAULT;
                surfaceInfo.MultiSampleType = D3DMULTISAMPLE_TYPE.D3DMULTISAMPLE_NONE;
                surfaceInfo.MultiSampleQuality = 0u;

                m_ReplacementSurface = new SurfaceData(surfaceInfo);
            }

            RenderTexture renderTexture = m_ReplacementSurface.colorTexture;
            RenderTexture depthTexture = m_ReplacementSurface.depthTexture;

            if (null == renderTexture)
            {
                Debug.LogWarning("No color surface.");
            }

            if (null == depthTexture)
            {
                Debug.LogWarning("No depth color texture surface.");
            }

            outColorTextureWasLost = wasLost;
            outSharedColorTextureHandle = m_ReplacementSurface.sharedColorTextureHandle;
            outColorDepthTextureWasLost = wasLost;
            outSharedColorDepthTextureHandle = m_ReplacementSurface.sharedDepthTextureHandle;

            if (this.suspended) return;

            if (null == afxCamera) return;

            {
                int width = renderInfo.Width;
                int height = renderInfo.Height;

                Matrix4x4 d3d9QuakeWorldToView = new Matrix4x4();
                d3d9QuakeWorldToView[0, 0] = renderInfo.ViewMatrix.M00;
                d3d9QuakeWorldToView[0, 1] = renderInfo.ViewMatrix.M01;
                d3d9QuakeWorldToView[0, 2] = renderInfo.ViewMatrix.M02;
                d3d9QuakeWorldToView[0, 3] = renderInfo.ViewMatrix.M03;
                d3d9QuakeWorldToView[1, 0] = renderInfo.ViewMatrix.M10;
                d3d9QuakeWorldToView[1, 1] = renderInfo.ViewMatrix.M11;
                d3d9QuakeWorldToView[1, 2] = renderInfo.ViewMatrix.M12;
                d3d9QuakeWorldToView[1, 3] = renderInfo.ViewMatrix.M13;
                d3d9QuakeWorldToView[2, 0] = renderInfo.ViewMatrix.M20;
                d3d9QuakeWorldToView[2, 1] = renderInfo.ViewMatrix.M21;
                d3d9QuakeWorldToView[2, 2] = renderInfo.ViewMatrix.M22;
                d3d9QuakeWorldToView[2, 3] = renderInfo.ViewMatrix.M23;
                d3d9QuakeWorldToView[3, 0] = renderInfo.ViewMatrix.M30;
                d3d9QuakeWorldToView[3, 1] = renderInfo.ViewMatrix.M31;
                d3d9QuakeWorldToView[3, 2] = renderInfo.ViewMatrix.M32;
                d3d9QuakeWorldToView[3, 3] = renderInfo.ViewMatrix.M33;

                Matrix4x4 d3d9QuakeProjection = new Matrix4x4();
                d3d9QuakeProjection[0, 0] = renderInfo.ProjectionMatrix.M00;
                d3d9QuakeProjection[0, 1] = renderInfo.ProjectionMatrix.M01;
                d3d9QuakeProjection[0, 2] = renderInfo.ProjectionMatrix.M02;
                d3d9QuakeProjection[0, 3] = renderInfo.ProjectionMatrix.M03;
                d3d9QuakeProjection[1, 0] = renderInfo.ProjectionMatrix.M10;
                d3d9QuakeProjection[1, 1] = renderInfo.ProjectionMatrix.M11;
                d3d9QuakeProjection[1, 2] = renderInfo.ProjectionMatrix.M12;
                d3d9QuakeProjection[1, 3] = renderInfo.ProjectionMatrix.M13;
                d3d9QuakeProjection[2, 0] = renderInfo.ProjectionMatrix.M20;
                d3d9QuakeProjection[2, 1] = renderInfo.ProjectionMatrix.M21;
                d3d9QuakeProjection[2, 2] = renderInfo.ProjectionMatrix.M22;
                d3d9QuakeProjection[2, 3] = renderInfo.ProjectionMatrix.M23;
                d3d9QuakeProjection[3, 0] = renderInfo.ProjectionMatrix.M30;
                d3d9QuakeProjection[3, 1] = renderInfo.ProjectionMatrix.M31;
                d3d9QuakeProjection[3, 2] = renderInfo.ProjectionMatrix.M32;

                //Debug.Log(d3d9QuakeProjection);

                const float unityToQuakeScaleFac = 100f / 2.54f;
                Matrix4x4 unityToQuakeScale = Matrix4x4.Scale(new Vector3(unityToQuakeScaleFac, unityToQuakeScaleFac, unityToQuakeScaleFac));

                Matrix4x4 unityToQuake = new Matrix4x4();
                unityToQuake[0, 0] = 0; unityToQuake[0, 1] = 0; unityToQuake[0, 2] = 1; unityToQuake[0, 3] = 0;
                unityToQuake[1, 0] = -1; unityToQuake[1, 1] = 0; unityToQuake[1, 2] = 0; unityToQuake[1, 3] = 0;
                unityToQuake[2, 0] = 0; unityToQuake[2, 1] = 1; unityToQuake[2, 2] = 0; unityToQuake[2, 3] = 0;
                unityToQuake[3, 0] = 0; unityToQuake[3, 1] = 0; unityToQuake[3, 2] = 0; unityToQuake[3, 3] = 1;

                Matrix4x4 flipViewZ = new Matrix4x4();
                flipViewZ[0, 0] = 1; flipViewZ[0, 1] = 0; flipViewZ[0, 2] = 0; flipViewZ[0, 3] = 0;
                flipViewZ[1, 0] = 0; flipViewZ[1, 1] = 1; flipViewZ[1, 2] = 0; flipViewZ[1, 3] = 0;
                flipViewZ[2, 0] = 0; flipViewZ[2, 1] = 0; flipViewZ[2, 2] = -1; flipViewZ[2, 3] = 0;
                flipViewZ[3, 0] = 0; flipViewZ[3, 1] = 0; flipViewZ[3, 2] = 0; flipViewZ[3, 3] = 1;

                Matrix4x4 unityToWorldViewInverse = (flipViewZ * (d3d9QuakeWorldToView * (unityToQuake * unityToQuakeScale))).inverse;

                const double Rad2Deg = 180.0 / Math.PI;

                Vector4 quakePosition = unityToWorldViewInverse.GetColumn(3);
                afxCamera.transform.position = new Vector3(quakePosition.x, quakePosition.y, quakePosition.z);

                Quaternion rotation = unityToWorldViewInverse.rotation;
                afxCamera.transform.rotation = new Quaternion(rotation.x, rotation.y, rotation.z, rotation.w);

                Vector3 quakeScale = unityToWorldViewInverse.lossyScale;
                afxCamera.transform.localScale = new Vector3(quakeScale.x, quakeScale.y, quakeScale.z);

                //afxCamera.worldToCameraMatrix = unityToWorldView;

                float C = d3d9QuakeProjection[2, 2]; // - far_plane/(far_plane - near_plane)
                float D = d3d9QuakeProjection[2, 3]; // C * near_plane

                //Debug.Log((D / C) + " / " + (D / (C + 1)));

                afxCamera.nearClipPlane = (D / C) / unityToQuakeScaleFac;
                afxCamera.farClipPlane = (D / (C + 1)) / unityToQuakeScaleFac;

                afxCamera.pixelRect = new Rect(0, 0, width, height);
                afxCamera.rect = new Rect(0, 0, width, height);

                float horizontalFovRad = (float)Math.Atan(1.0 / d3d9QuakeProjection[0, 0]) * 2.0f;
                float verticalFovDeg = (float)(2 * Math.Atan(Math.Tan(horizontalFovRad / 2.0) * height / (float)width) * Rad2Deg);

                //Debug.Log(horizontalFovRad * Rad2Deg + " / " + verticalFovDeg);

                afxCamera.fieldOfView = verticalFovDeg;
                afxCamera.aspect = (0 != height) ? (width / (float)height) : 1.0f;

                Matrix4x4 orgCamProjection = afxCamera.projectionMatrix;
                afxCamera.projectionMatrix = GL.GetGPUProjectionMatrix(flipViewZ * orgCamProjection, true);

                afxOldCameraClearFlags = afxCamera.clearFlags;
                afxCamera.clearFlags = CameraClearFlags.Depth;

                afxDrawDepth = new CommandBuffer();
                afxDrawDepth.name = "AfxHookUnity: Draw depth buffer color texture.";

                float orthoZ = afxCamera.nearClipPlane + (afxCamera.nearClipPlane + afxCamera.farClipPlane) / 2.0f;

                var verticies = new Vector3[4] {
                new Vector3(0, 0, orthoZ),
                new Vector3(1, 0, orthoZ),
                new Vector3(0, 1, orthoZ),
                new Vector3(1, 1, orthoZ)
            };

                var uvs = new Vector2[4] {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
            };

                var triangles = new int[6] {
                0, 1, 2,
                2, 1, 3,
            };

                var m = new Mesh();
                m.vertices = verticies;
                m.uv = uvs;
                m.triangles = triangles;

                this.drawDepthMaterial.mainTexture = depthTexture;

                Vector4 zParams = new Vector4((D / C), (D / (C + 1)), 0);
                this.drawDepthMaterial.SetVector("_ZParams", zParams);

                afxDrawDepth.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                afxDrawDepth.DrawMesh(m, GL.GetGPUProjectionMatrix(flipViewZ * Matrix4x4.Ortho(0, 1, 1, 0, afxCamera.nearClipPlane, afxCamera.farClipPlane), true), this.drawDepthMaterial);

                afxCamera.targetTexture = renderTexture;

                afxCameraUpdated = true;
            }
        }
        catch(Exception e)
        {
            Debug.LogException(e, this);

            outColorTextureWasLost = true;
            outSharedColorTextureHandle = IntPtr.Zero;
            outColorDepthTextureWasLost = true;
            outSharedColorDepthTextureHandle = IntPtr.Zero;
        }
    }
}
