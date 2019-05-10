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


// TODO: Might leak texture handles. Not anymore? Dunno.

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class AfxInterop : UnityEngine.Rendering.LWRP.AfxInteropBase
{
    public class BaseCalc : IDisposable
    {
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected IntPtr iterator;
        protected GCHandle gcHandle;

        protected virtual void Dispose(bool disposing)
        {
            if (disposed || !disposing) return;

            AfxInteropRemoveCallback(iterator);

            gcHandle.Free();

            disposed = true;
        }

        private bool disposed = false;
    }

    public class HandleCalc : BaseCalc, IDisposable
    {
        public delegate void CallbackDelegate(Nullable<AfxInteropHandleCalcResult> result);

        public HandleCalc(UnityEngine.Object owner, string name, CallbackDelegate callback)
        {
            this.owner = owner;
            this.callback = callback;
            this.ownCallback = Callback;
            this.gcHandle = GCHandle.Alloc(this.ownCallback, GCHandleType.Pinned);
            this.iterator = AfxInteropAddHandleCalcCallback(name, ownCallback);
        }

        private AfxInteropHandleCalcCallbackDelegate ownCallback;
        private CallbackDelegate callback;
        private UnityEngine.Object owner;

        private void Callback(IntPtr result)
        {
            // Exceptions must not go into native code:
            try
            {
                callback(IntPtr.Zero == result ? new Nullable<AfxInteropHandleCalcResult>() : new Nullable<AfxInteropHandleCalcResult>(Marshal.PtrToStructure<AfxInteropHandleCalcResult>(result)));
            }
            catch (Exception e)
            {
                Debug.LogException(e, owner);
            }
        }
    }

    public class VecAngCalc : BaseCalc, IDisposable
    {
        public delegate void CallbackDelegate(Nullable<AfxInteropVecAngCalcResult> result);

        public VecAngCalc(UnityEngine.Object owner, string name, CallbackDelegate callback)
        {
            this.owner = owner;
            this.callback = callback;
            this.ownCallback = Callback;
            this.gcHandle = GCHandle.Alloc(this.ownCallback, GCHandleType.Pinned);
            this.iterator = AfxInteropAddVecAngCalcCallback(name, ownCallback);
        }

        private AfxInteropVecAngCalcCallbackDelegate ownCallback;
        private CallbackDelegate callback;
        private UnityEngine.Object owner;

        private void Callback(IntPtr result)
        {
            // Exceptions must not go into native code:
            try
            {
                callback(IntPtr.Zero == result ? new Nullable<AfxInteropVecAngCalcResult>() : new Nullable<AfxInteropVecAngCalcResult>(Marshal.PtrToStructure<AfxInteropVecAngCalcResult>(result)));
            }
            catch (Exception e)
            {
                Debug.LogException(e, owner);
            }
        }
    }

    public class CamCalc : BaseCalc, IDisposable
    {
        public delegate void CallbackDelegate(Nullable<AfxInteropCamCalcResult> result);

        public CamCalc(UnityEngine.Object owner, string name, CallbackDelegate callback)
        {
            this.owner = owner;
            this.callback = callback;
            this.ownCallback = Callback;
            this.gcHandle = GCHandle.Alloc(this.ownCallback, GCHandleType.Pinned);
            this.iterator = AfxInteropAddCamCalcCallback(name, ownCallback);
        }

        private AfxInteropCamCalcCallbackDelegate ownCallback;
        private CallbackDelegate callback;
        private UnityEngine.Object owner;

        private void Callback(IntPtr result)
        {
            // Exceptions must not go into native code:
            try
            {
                callback(IntPtr.Zero == result ? new Nullable<AfxInteropCamCalcResult>() : new Nullable<AfxInteropCamCalcResult>(Marshal.PtrToStructure<AfxInteropCamCalcResult>(result)));
            }
            catch (Exception e)
            {
                Debug.LogException(e, owner);
            }
        }
    }

    public class FovCalc : BaseCalc, IDisposable
    {
        public delegate void CallbackDelegate(Nullable<AfxInteropFovCalcResult> result);

        public FovCalc(UnityEngine.Object owner, string name, CallbackDelegate callback)
        {
            this.owner = owner;
            this.callback = callback;
            this.ownCallback = Callback;
            this.gcHandle = GCHandle.Alloc(this.ownCallback, GCHandleType.Pinned);
            this.iterator = AfxInteropAddFovCalcCallback(name, ownCallback);
        }

        private AfxInteropFovCalcCallbackDelegate ownCallback;
        private CallbackDelegate callback;
        private UnityEngine.Object owner;

        private void Callback(IntPtr result)
        {
            // Exceptions must not go into native code:
            try
            {
                callback(IntPtr.Zero == result ? new Nullable<AfxInteropFovCalcResult>() : new Nullable<AfxInteropFovCalcResult>(Marshal.PtrToStructure<AfxInteropFovCalcResult>(result)));
            }
            catch (Exception e)
            {
                Debug.LogException(e, owner);
            }
        }
    }

    public class BoolCalc : BaseCalc, IDisposable
    {
        public delegate void CallbackDelegate(Nullable<AfxInteropBoolCalcResult> result);

        public BoolCalc(UnityEngine.Object owner, string name, CallbackDelegate callback)
        {
            this.owner = owner;
            this.callback = callback;
            this.ownCallback = Callback;
            this.gcHandle = GCHandle.Alloc(this.ownCallback, GCHandleType.Pinned);
            this.iterator = AfxInteropAddBoolCalcCallback(name, ownCallback);
        }

        private AfxInteropBoolCalcCallbackDelegate ownCallback;
        private CallbackDelegate callback;
        private UnityEngine.Object owner;

        private void Callback(IntPtr result)
        {
            // Exceptions must not go into native code:
            try
            {
                callback(IntPtr.Zero == result ? new Nullable<AfxInteropBoolCalcResult>() : new Nullable<AfxInteropBoolCalcResult>(Marshal.PtrToStructure<AfxInteropBoolCalcResult>(result)));
            }
            catch (Exception e)
            {
                Debug.LogException(e, owner);
            }
        }
    }

    public class IntCalc : BaseCalc, IDisposable
    {
        public delegate void CallbackDelegate(Nullable<AfxInteropIntCalcResult> result);

        public IntCalc(UnityEngine.Object owner, string name, CallbackDelegate callback)
        {
            this.owner = owner;
            this.callback = callback;
            this.ownCallback = Callback;
            this.gcHandle = GCHandle.Alloc(this.ownCallback, GCHandleType.Pinned);
            this.iterator = AfxInteropAddIntCalcCallback(name, ownCallback);
        }

        private AfxInteropIntCalcCallbackDelegate ownCallback;
        private CallbackDelegate callback;
        private UnityEngine.Object owner;

        private void Callback(IntPtr result)
        {
            // Exceptions must not go into native code:
            try
            {
                callback(IntPtr.Zero == result ? new Nullable<AfxInteropIntCalcResult>() : new Nullable<AfxInteropIntCalcResult>(Marshal.PtrToStructure<AfxInteropIntCalcResult>(result)));
            }
            catch (Exception e)
            {
                Debug.LogException(e, owner);
            }
        }
    }


    /// <summary>
    /// Commands accessor for reading commands from client and sending them back.<br />
    /// Should not send too much per one call (might freeze client).<br />
    /// ATTENTION: It is illegal to hold on to this interface longer than the call of the delegate lasts!
    /// </summary>
    public interface ICommands
    {
        /// <summary>
        /// Compute count of commands sent to us.
        /// </summary>
        /// <returns>Count of commands sent to us.</returns>
        UInt32 GetCount();

        /// <summary>
        /// Get number of arguemnts for a command.
        /// </summary>
        /// <param name="commandIndex">Index of command, first is 0.</param>
        /// <returns></returns>
        UInt32 GetCommandArgCount(UInt32 commandIndex);

        /// <summary>
        /// Get argument of a command.
        /// </summary>
        /// <param name="commandIndex">Index of command, first is 0.</param>
        /// <param name="argumentIndex">Index of argument, first is 0.</param>
        /// <returns></returns>
        string GetCommandArg(UInt32 commandIndex, UInt32 argumentIndex);

        /// <summary>
        /// Send a command to the client.
        /// </summary>
        /// <param name="command">Command to send.</param>
        /// <returns>false on fail.</returns>
        bool ScheduleCommand(string command);

        /// <summary>
        /// Call this if you can't handle a command to call the next default handler (if any).
        /// </summary>
        /// <param name="commandIndex">Index of command, first is 0.</param>
        /// <returns>true if handled.</returns>
        bool Fallback(UInt32 commandIndex);
    }

    /// <summary>
    /// Callback to handle commands.
    /// </summary>
    /// <param name="commands">The commands to handle.</param>
    /// <returns>True if handled, otherwise false.</returns>    
    public delegate void AfxInteropCommandsCallbackDelegate(ICommands commands);

    public const float quakeToUntiyScaleFac = (12f / 16f) * (2.54f / 100f); // https://developer.valvesoftware.com/wiki/Dimensions

    public const float unityToQuakeScaleFac = (100f / 2.54f) * (16f / 12f);

    //

    public string pipeName = "advancedfxInterop";

    public bool suspended = false;

    public Camera afxOverrideCamera = null;

    public bool afxOverrideCameraEnabled = true;

    public AfxInteropCommandsCallbackDelegate afxInteropCommandsCallback;

    //

    public static Vector3 ToUnityVector(AfxInteropVector value)
    {
        return new Vector3(-value.Y / unityToQuakeScaleFac, value.Z / unityToQuakeScaleFac, value.X / unityToQuakeScaleFac);
    }

    public static Quaternion ToUnityQuaternion(AfxInteropQAngle value)
    {
        return
            Quaternion.AngleAxis(-value.Yaw, Vector3.up)
            * Quaternion.AngleAxis(value.Pitch, Vector3.right)
            * Quaternion.AngleAxis(-value.Roll, Vector3.forward);
    }

    public static float CsgoFovToRealQuakeFov(float fov, float width, float height)
    {
        if (0f == height) return fov;

        float engineAspectRatio = width / height;
        float defaultAscpectRatio = 4f / 3f;
        float ratio = engineAspectRatio / defaultAscpectRatio;
        float halfAngle = 0.5f * fov * Mathf.Deg2Rad;
        float t = ratio * Mathf.Tan(halfAngle);
        return 2f * Mathf.Atan(t) * Mathf.Rad2Deg;
    }

    public static float RealQuakeFovToCsgoFov(float width, float height, float fov)
    {
        if (0 == height) return fov;

        float engineAspectRatio = width / height;
        float defaultAscpectRatio = 4f / 3f;
        float ratio = engineAspectRatio / defaultAscpectRatio;
        float t = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
        float halfAngle = Mathf.Atan(t / ratio);
        return 2f * halfAngle * Mathf.Rad2Deg;
    }

    public static float ToUnityFov(float value, float width, float height)
    {
        float verticalFovDeg = 2f * Mathf.Atan(Mathf.Tan(value * Mathf.Deg2Rad / 2f) * height / width) * Mathf.Rad2Deg;
        return verticalFovDeg;
    }

    public static AfxInteropVector FromUnityVector(Vector3 value)
    {
        AfxInteropVector result = new AfxInteropVector();

        result.X = value.z * unityToQuakeScaleFac;
        result.Y = -value.x * unityToQuakeScaleFac;
        result.Z = value.y * unityToQuakeScaleFac;

        return result;
    }

    public static AfxInteropQAngle FromUnityQuaternion(Quaternion value)
    {
        AfxInteropQAngle result = new AfxInteropQAngle();

        float W = value[3];
        float X = value[2];
        float Y = -value[0];
        float Z = value[1];

        float sqw = W * W;
        float sqx = X * X;
        float sqy = Y * Y;
        float sqz = Z * Z;

        float ssq = sqx + sqy + sqz + sqw;
        float invs = 0f != ssq ? 1f / ssq : 0f;
        float m00 = (sqx - sqy - sqz + sqw) * invs;
        //float m11 = (-sqx + sqy - sqz + sqw)*invs;
        float m22 = (-sqx - sqy + sqz + sqw) * invs;

        float tmp1 = X * Y;
        float tmp2 = Z * W;
        float m10 = 2.0f * (tmp1 + tmp2) * invs;
        //float m01 = 2.0f * (tmp1 - tmp2)*invs;

        tmp1 = X * Z;
        tmp2 = Y * W;
        float m20 = 2.0f * (tmp1 - tmp2) * invs;
        //float m02 = 2.0f * (tmp1 + tmp2)*invs;

        tmp1 = Y * Z;
        tmp2 = X * W;
        float m21 = 2.0f * (tmp1 + tmp2) * invs;
        //float m12 = 2.0f * (tmp1 - tmp2)*invs;

        float sinYPitch = -m20;
        float yPitch;
        float zYaw;
        float xRoll;

        if (sinYPitch > 1.0 - 1.0e-6f)
        {
            // sout pole singularity:

            yPitch = Mathf.PI / 2.0f;

            xRoll = -2.0f * Mathf.Atan2(Z * invs, W * invs);
            zYaw = 0;
        }
        else
        if (sinYPitch < -1.0 + 1.0e-6f)
        {
            // north pole singularity:

            yPitch = -Mathf.PI / 2.0f;
            xRoll = 2.0f * Mathf.Atan2(Z * invs, W * invs);
            zYaw = 0;
        }
        else
        {
            // hopefully no singularity:

            yPitch = Mathf.Asin(sinYPitch);
            zYaw = Mathf.Atan2(m10, m00);
            xRoll = Mathf.Atan2(m21, m22);
        }

        result.Pitch = -yPitch * Mathf.Rad2Deg;
        result.Yaw = -zYaw * Mathf.Rad2Deg;
        result.Roll = -xRoll * Mathf.Rad2Deg;

        return result;
    }

    public static float FromUnityFov(float value, float width, float height)
    {
        float horizontalFovDeg = 2f * Mathf.Atan(Mathf.Tan(value * Mathf.Deg2Rad / 2f) * width / height) * Mathf.Rad2Deg;

        return horizontalFovDeg;
    }

    //

    public override bool GetAfxActive()
    {
        return afxActive;
    }

    public override CommandBuffer GetAfxBegin()
    {
        if (afxActive) return afxDrawBegin;
        return null;
    }

    public override CommandBuffer GetAfxEnd()
    {
        if (afxActive) return afxDrawEnd;
        return null;
    }


    public override CommandBuffer GetAfxLoadDepth()
    {
        if (afxActive) return afxDrawDepth;
        return null;
    }

    //

    public void Awake()
    {

        if (4 != AfxInteropVersion())
            Debug.LogError("AfxInterop.dll version mismatch (try to rebuild).");

        Application.runInBackground = true; // don't sleep when not having focus
        QualitySettings.vSyncCount = 0; // render as fast as possible

        this.drawColorMaterial = Resources.Load("advancedfx/Materials/DrawColor") as Material;
        this.drawDepthMaterial = Resources.Load("advancedfx/Materials/DrawDepth") as Material;

        afxDrawBegin = new CommandBuffer();
        afxDrawDepth = new CommandBuffer();
        afxDrawEnd = new CommandBuffer();

        m_AfxInteropCommandsDelegate += AfxInteropCommands;
        m_AfxInteropRenderDelegate += AfxInteropRender;
        m_AfxInteropRenderPassDelegate += AfxInteropRenderPass;
        m_AfxInteropOnViewOverrideCallbackDelegate += AfxInteropOnViewOverride;
    }

    public void OnEnable()
    {

        AfxInteropSetOnViewOverrideCallback(m_AfxInteropOnViewOverrideCallbackDelegate);

        AfxInteropCreate(pipeName, m_AfxInteropCommandsDelegate, m_AfxInteropRenderDelegate, m_AfxInteropRenderPassDelegate);
    }

    public void OnDisable()
    {

        AfxInteropDestroy();

        AfxInteropSetOnViewOverrideCallback(null);

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
            afxCamera.allowHDR = true;
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
        if (!afxCameraUpdated)
        {
            return;
        }

        afxCameraUpdated = false;

        afxActive = true;
        GL.invertCulling = true;
        afxCamera.Render();
        GL.invertCulling = false;
        afxActive = false;

        //

        this.drawColorMaterial.mainTexture = null;
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

    private Material drawColorMaterial;
    private Material drawDepthMaterial;

    private bool afxActive = false;
    private bool afxCameraUpdated = false;
    private Camera afxCamera;
    private CommandBuffer afxDrawBegin;
    private CommandBuffer afxDrawDepth;
    private CommandBuffer afxDrawEnd;
    private CameraClearFlags afxOldCameraClearFlags;

    SurfaceData m_ReplacementSurface;

    AfxInteropCommandsDelegate m_AfxInteropCommandsDelegate;
    AfxInteropRenderDelegate m_AfxInteropRenderDelegate;
    AfxInteropRenderPassDelegate m_AfxInteropRenderPassDelegate;
    AfxInteropOnViewOverrideCallbackDelegate m_AfxInteropOnViewOverrideCallbackDelegate;


    enum AfxRenderPassType : UInt32
    {
        AfxRenderPassType_BeforeTranslucentShadow = 2,
        AfxRenderPassType_AfterTranslucentShadow = 3,
        AfxRenderPassType_BeforeTranslucent = 4,
        AfxRenderPassType_AfterTranslucent = 5
    }

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
    public struct AfxInteropView
    {
        public Int32 X;
        public Int32 Y;
        public Int32 Width;
        public Int32 Height;
        public AfxInteropMatrix4x4 ViewMatrix;
        public AfxInteropMatrix4x4 ProjectionMatrix;
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct AfxInteropRenderInfo
    {
        public AfxInteropView View;
        public Int32 FrameCount;
        public Single AbsoluteFrameTime;
        public Single CurTime;
        public Single FrameTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AfxInteropHandleCalcResult
    {
        public Int32 IntHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AfxInteropVector
    {
        public Single X;
        public Single Y;
        public Single Z;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AfxInteropQAngle
    {
        public Single Pitch;
        public Single Yaw;
        public Single Roll;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AfxInteropVecAngCalcResult
    {
        public AfxInteropVector Vector;
        public AfxInteropQAngle QAngle;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AfxInteropCamCalcResult
    {
        public AfxInteropVector Vector;
        public AfxInteropQAngle QAngle;
        public Single Fov;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AfxInteropFovCalcResult
    {
        public Single Fov;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AfxInteropBoolCalcResult
    {
        public Boolean Result;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AfxInteropIntCalcResult
    {
        public Int32 Result;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void AfxInteropHandleCalcCallbackDelegate(IntPtr result);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void AfxInteropVecAngCalcCallbackDelegate(IntPtr result);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void AfxInteropCamCalcCallbackDelegate(IntPtr result);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void AfxInteropFovCalcCallbackDelegate(IntPtr result);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void AfxInteropBoolCalcCallbackDelegate(IntPtr result);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void AfxInteropIntCalcCallbackDelegate(IntPtr result);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void AfxInteropCommandsDelegate(IntPtr commands);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate bool AfxInteropOnViewOverrideCallbackDelegate(ref float TX, ref float Ty, ref float Tz, ref float Rx, ref float Ry, ref float Rz, ref float Fov);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void AfxInteropRenderDelegate(
        ref AfxInteropRenderInfo renderInfo, out bool outColorTextureWasLost, out IntPtr outSharedColorTextureHandle, out bool outColorDepthTextureWasLost, out IntPtr outSharedColorDepthTextureHandle,
        out bool outBeforeTranslucentShadow, out bool outAfterTranslucentShadow,
        out bool outBeforeTranslucent, out bool outAfterTranslucent,
        out bool outBeforeHud, out bool outAfterHud);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void AfxInteropRenderPassDelegate(AfxRenderPassType pass, ref AfxInteropView view);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("AfxInterop")]
    private static extern int AfxInteropVersion();

    [DllImport("AfxInterop")]
    private static extern void AfxInteropBeginCreateRenderTexture();

    [DllImport("AfxInterop")]
    private static extern IntPtr AfxInteropGetSharedHandle(IntPtr d3d11ResourcePtr);

    [DllImport("AfxInterop")]
    private static extern IntPtr AfxInteropGetRenderEventFunc();

    [DllImport("AfxInterop")]
    private static extern IntPtr AfxInteropGetRenderEventAndDataFunc();

    [DllImport("AfxInterop")]
    private static extern UInt32 AfxInteropCommands_GetCommandCount(IntPtr commands);

    [DllImport("AfxInterop")]
    private static extern UInt32 AfxInteropCommands_GetCommandArgCount(IntPtr commands, UInt32 index);

    [DllImport("AfxInterop")]
    private static extern IntPtr AfxInteropCommands_GetCommandArg(IntPtr commands, UInt32 index, UInt32 argIndex);

    [DllImport("AfxInterop")]
    private static extern void AfxInteropDestroy();

    [DllImport("AfxInterop")]
    private static extern bool AfxInteropCreate([MarshalAs(UnmanagedType.LPStr)] string pipeName, [MarshalAs(UnmanagedType.FunctionPtr)]AfxInteropCommandsDelegate afxInteropCommands, [MarshalAs(UnmanagedType.FunctionPtr)]AfxInteropRenderDelegate afxInteropRender, [MarshalAs(UnmanagedType.FunctionPtr)]AfxInteropRenderPassDelegate afxInteropRenderPass);

    [DllImport("AfxInterop")]
    private static extern bool AfxInteropUpdateEngineThread();

    [DllImport("AfxInterop")]
    private static extern bool AfxInteropScheduleCommand([MarshalAs(UnmanagedType.LPStr)] string command);

    [DllImport("AfxInterop")]
    private static extern void AfxInteropSetOnViewOverrideCallback([MarshalAs(UnmanagedType.FunctionPtr)]AfxInteropOnViewOverrideCallbackDelegate callback);

    /// <returns>Iterator that has to be unregisterred with AfxInteropRemoveCallback, before the delgate is move or removed.</returns>
    [DllImport("AfxInterop")]
    private static extern IntPtr AfxInteropAddHandleCalcCallback([MarshalAs(UnmanagedType.LPStr)] string name, [MarshalAs(UnmanagedType.FunctionPtr)]AfxInteropHandleCalcCallbackDelegate callback);

    /// <returns>Iterator that has to be unregisterred with AfxInteropRemoveCallback, before the delgate is move or removed.</returns>
    [DllImport("AfxInterop")]
    private static extern IntPtr AfxInteropAddVecAngCalcCallback([MarshalAs(UnmanagedType.LPStr)] string name, [MarshalAs(UnmanagedType.FunctionPtr)]AfxInteropVecAngCalcCallbackDelegate callback);

    /// <returns>Iterator that has to be unregisterred with AfxInteropRemoveCallback, before the delgate is move or removed.</returns>
    [DllImport("AfxInterop")]
    private static extern IntPtr AfxInteropAddCamCalcCallback([MarshalAs(UnmanagedType.LPStr)] string name, [MarshalAs(UnmanagedType.FunctionPtr)]AfxInteropCamCalcCallbackDelegate callback);

    /// <returns>Iterator that has to be unregisterred with AfxInteropRemoveCallback, before the delgate is move or removed.</returns>
    [DllImport("AfxInterop")]
    private static extern IntPtr AfxInteropAddFovCalcCallback([MarshalAs(UnmanagedType.LPStr)] string name, [MarshalAs(UnmanagedType.FunctionPtr)]AfxInteropFovCalcCallbackDelegate callback);

    /// <returns>Iterator that has to be unregisterred with AfxInteropRemoveCallback, before the delgate is move or removed.</returns>
    [DllImport("AfxInterop")]
    private static extern IntPtr AfxInteropAddBoolCalcCallback([MarshalAs(UnmanagedType.LPStr)] string name, [MarshalAs(UnmanagedType.FunctionPtr)]AfxInteropBoolCalcCallbackDelegate callback);

    /// <returns>Iterator that has to be unregisterred with AfxInteropRemoveCallback, before the delgate is move or removed.</returns>
    [DllImport("AfxInterop")]
    private static extern IntPtr AfxInteropAddIntCalcCallback([MarshalAs(UnmanagedType.LPStr)] string name, [MarshalAs(UnmanagedType.FunctionPtr)]AfxInteropIntCalcCallbackDelegate callback);

    [DllImport("AfxInterop")]
    private static extern void AfxInteropRemoveCallback(IntPtr iterator);

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
                cameraTexture = new RenderTexture(rdesc.Value);
                cameraTexture.depth = 32;

                colorTexture = new RenderTexture(rdesc.Value);
                colorTexture.DiscardContents(false, true);
                AfxInteropBeginCreateRenderTexture();
                colorTexture.Create();
                sharedColorTextureHandle = AfxInteropGetSharedHandle(colorTexture.GetNativeTexturePtr());
                Debug.Log("Color: " + colorTexture.GetNativeTexturePtr() + " -> " + sharedColorTextureHandle);
            }

            rdesc = GetRenderTextureDescriptor(surfaceInfo, false);
            if (rdesc.HasValue)
            {
                depthTexture = new RenderTexture(rdesc.Value);
                depthTexture.DiscardContents(false, true);
                AfxInteropBeginCreateRenderTexture();
                depthTexture.Create();
                sharedDepthTextureHandle = AfxInteropGetSharedHandle(depthTexture.GetNativeTexturePtr());
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

        public readonly RenderTexture cameraTexture;
        public readonly ISurfaceInfo surfaceInfo;
        public readonly RenderTexture colorTexture;
        public readonly IntPtr sharedColorTextureHandle;
        public readonly RenderTexture depthTexture;
        public readonly IntPtr sharedDepthTextureHandle;

        private Nullable<RenderTextureDescriptor> GetRenderTextureDescriptor(ISurfaceInfo fbSurfaceInfo, bool isDepth)
        {
            UnityEngine.Experimental.Rendering.GraphicsFormat graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;

            switch (fbSurfaceInfo.Format)
            {
                case D3DFORMAT.D3DFMT_A8R8G8B8:
                    graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.B8G8R8A8_UNorm;
                    break;
                default:
                    Debug.LogError("Unknown back buffer format: " + fbSurfaceInfo.Format);
                    return null;
            }

            RenderTextureDescriptor desc = new RenderTextureDescriptor((int)fbSurfaceInfo.Width, (int)fbSurfaceInfo.Height, graphicsFormat, isDepth ? 0 : 24);

            Debug.Log("GetRenderTextureDescriptor: " + fbSurfaceInfo.Id + "(" + (isDepth ? "depth" : "color") + ")" + ": " + fbSurfaceInfo.Format + " (" + fbSurfaceInfo.Width + "," + fbSurfaceInfo.Height + ")");

            desc.autoGenerateMips = false;
            desc.bindMS = false;
            desc.enableRandomWrite = false;
            desc.sRGB = false; // for windowed CS:GO at least (?).
            desc.msaaSamples = 1;
            desc.useMipMap = false;

            return new Nullable<RenderTextureDescriptor>(desc);
        }
    }

    class AfxInteropCommandsWrapper : ICommands
    {
        public UInt32 GetCount()
        {
            CheckValid();

            return AfxInteropCommands_GetCommandCount(commands);
        }

        public UInt32 GetCommandArgCount(UInt32 commandIndex)
        {
            CheckValid();

            return AfxInteropCommands_GetCommandArgCount(commands, commandIndex);
        }

        public string GetCommandArg(UInt32 commandIndex, UInt32 argumentIndex)
        {
            CheckValid();

            return DoAfxInteropCommands_GetCommandArg(commands, commandIndex, argumentIndex);
        }

        public bool ScheduleCommand(string command)
        {
            CheckValid();

            return AfxInteropScheduleCommand(command);
        }

        public bool Fallback(UInt32 commandIndex)
        {
            CheckValid();

            UInt32 argCount = GetCommandArgCount(commandIndex);

            if (0 < argCount)
            {
                string arg0 = GetCommandArg(commandIndex, 0);

                if (2 <= argCount)
                {
                    string arg1 = GetCommandArg(commandIndex, 1);

                    if (0 == arg1.CompareTo("afx"))
                    {
                        if (4 == argCount)
                        {
                            string arg2 = GetCommandArg(commandIndex, 2);
                            string arg3 = GetCommandArg(commandIndex, 3);

                            if (0 == arg2.CompareTo("suspended"))
                            {
                                int value;

                                if (int.TryParse(arg3, out value))
                                    afxInterop.suspended = 0 != value;

                                return true;
                            }
                        }
                    }

                }

                ScheduleCommand("echo " + arg0 + " afx suspended 0|1 - Suspend / resume rendering.\n");

                return true;
            }

            return false;
        }

        public AfxInteropCommandsWrapper(IntPtr commands, AfxInterop afxInterop)
        {
            this.commands = commands;
            this.afxInterop = afxInterop;
        }

        public void Invalidate()
        {
            valid = false;
        }

        private IntPtr commands;
        private bool valid = true;
        private AfxInterop afxInterop;

        private void CheckValid()
        {
            if (!valid)
            {
                throw new System.ApplicationException("IAfxInteropCommands used outside of valid scope.");
            }
        }
    }


    void AfxInteropCommands(IntPtr commands)
    {
        AfxInteropCommandsWrapper afxInteropCommandsWrapper = null;

        // Exceptions must not make it into native code (this is called from native code):
        try
        {
            bool handled = false;

            afxInteropCommandsWrapper = new AfxInteropCommandsWrapper(commands, this);

            if (null != afxInteropCommandsCallback)
            {
                afxInteropCommandsCallback(afxInteropCommandsWrapper);
                handled = true;
            }

            if (!handled)
            {
                // Default implementation:

                UInt32 commandCount = afxInteropCommandsWrapper.GetCount();

                for (UInt32 i = 0; i < commandCount; ++i)
                {
                    afxInteropCommandsWrapper.Fallback(i);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
        finally
        {
            if (null != afxInteropCommandsWrapper) afxInteropCommandsWrapper.Invalidate();
        }
    }

    void AfxInteropRender(
        ref AfxInteropRenderInfo renderInfo, out bool outColorTextureWasLost, out IntPtr outSharedColorTextureHandle, out bool outColorDepthTextureWasLost, out IntPtr outSharedColorDepthTextureHandle,
        out bool outBeforeTranslucentShadow, out bool outAfterTranslucentShadow,
        out bool outBeforeTranslucent, out bool outAfterTranslucent,
        out bool outBeforeHud, out bool outAfterHud)
    {
        // Exceptions must not make it into native code (this is called from native code):
        try
        {
            float absoluteFrameTime = renderInfo.AbsoluteFrameTime;
            float frameTime = renderInfo.FrameTime;

            //Debug.Log(absoluteFrameTime + " / " + frameTime);

            Time.timeScale = 0 == Time.unscaledDeltaTime ? 1 : frameTime / Time.unscaledDeltaTime;

            bool wasLost = false;

            if (null != m_ReplacementSurface && (m_ReplacementSurface.colorTexture.width != renderInfo.View.Width || m_ReplacementSurface.colorTexture.height != renderInfo.View.Height))
            {
                m_ReplacementSurface.Dispose();
                m_ReplacementSurface = null;
            }

            if (null == m_ReplacementSurface)
            {
                wasLost = true;

                SurfaceInfo surfaceInfo = new SurfaceInfo();
                surfaceInfo.Id = IntPtr.Zero;
                surfaceInfo.Width = (UInt32)renderInfo.View.Width;
                surfaceInfo.Height = (UInt32)renderInfo.View.Height;
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

            outBeforeTranslucentShadow = false; outAfterTranslucentShadow = false;
            outBeforeTranslucent = false; outAfterTranslucent = false;
            outBeforeHud = false; outAfterHud = false;

            if (this.suspended)
            {
                return;
            }

            if (null == afxCamera) return;

            outBeforeHud = true;

            {
                int width = renderInfo.View.Width;
                int height = renderInfo.View.Height;

                Matrix4x4 d3d9QuakeWorldToView = new Matrix4x4();
                d3d9QuakeWorldToView[0, 0] = renderInfo.View.ViewMatrix.M00;
                d3d9QuakeWorldToView[0, 1] = renderInfo.View.ViewMatrix.M01;
                d3d9QuakeWorldToView[0, 2] = renderInfo.View.ViewMatrix.M02;
                d3d9QuakeWorldToView[0, 3] = renderInfo.View.ViewMatrix.M03;
                d3d9QuakeWorldToView[1, 0] = renderInfo.View.ViewMatrix.M10;
                d3d9QuakeWorldToView[1, 1] = renderInfo.View.ViewMatrix.M11;
                d3d9QuakeWorldToView[1, 2] = renderInfo.View.ViewMatrix.M12;
                d3d9QuakeWorldToView[1, 3] = renderInfo.View.ViewMatrix.M13;
                d3d9QuakeWorldToView[2, 0] = renderInfo.View.ViewMatrix.M20;
                d3d9QuakeWorldToView[2, 1] = renderInfo.View.ViewMatrix.M21;
                d3d9QuakeWorldToView[2, 2] = renderInfo.View.ViewMatrix.M22;
                d3d9QuakeWorldToView[2, 3] = renderInfo.View.ViewMatrix.M23;
                d3d9QuakeWorldToView[3, 0] = renderInfo.View.ViewMatrix.M30;
                d3d9QuakeWorldToView[3, 1] = renderInfo.View.ViewMatrix.M31;
                d3d9QuakeWorldToView[3, 2] = renderInfo.View.ViewMatrix.M32;
                d3d9QuakeWorldToView[3, 3] = renderInfo.View.ViewMatrix.M33;

                Matrix4x4 d3d9QuakeProjection = new Matrix4x4();
                d3d9QuakeProjection[0, 0] = renderInfo.View.ProjectionMatrix.M00;
                d3d9QuakeProjection[0, 1] = renderInfo.View.ProjectionMatrix.M01;
                d3d9QuakeProjection[0, 2] = renderInfo.View.ProjectionMatrix.M02;
                d3d9QuakeProjection[0, 3] = renderInfo.View.ProjectionMatrix.M03;
                d3d9QuakeProjection[1, 0] = renderInfo.View.ProjectionMatrix.M10;
                d3d9QuakeProjection[1, 1] = renderInfo.View.ProjectionMatrix.M11;
                d3d9QuakeProjection[1, 2] = renderInfo.View.ProjectionMatrix.M12;
                d3d9QuakeProjection[1, 3] = renderInfo.View.ProjectionMatrix.M13;
                d3d9QuakeProjection[2, 0] = renderInfo.View.ProjectionMatrix.M20;
                d3d9QuakeProjection[2, 1] = renderInfo.View.ProjectionMatrix.M21;
                d3d9QuakeProjection[2, 2] = renderInfo.View.ProjectionMatrix.M22;
                d3d9QuakeProjection[2, 3] = renderInfo.View.ProjectionMatrix.M23;
                d3d9QuakeProjection[3, 0] = renderInfo.View.ProjectionMatrix.M30;
                d3d9QuakeProjection[3, 1] = renderInfo.View.ProjectionMatrix.M31;
                d3d9QuakeProjection[3, 2] = renderInfo.View.ProjectionMatrix.M32;

                //Debug.Log(d3d9QuakeProjection);

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
                afxCamera.clearFlags = CameraClearFlags.Nothing;

                float orthoZ = afxCamera.nearClipPlane;

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
                    2, 1, 0,
                    3, 1, 2,
                };

                var m = new Mesh();
                m.vertices = verticies;
                m.uv = uvs;
                m.triangles = triangles;

                this.drawColorMaterial.mainTexture = renderTexture;
                this.drawDepthMaterial.mainTexture = depthTexture;

                Vector4 zParams = new Vector4((D / C), (D / (C + 1)), 0, 1);

                afxCamera.targetTexture = m_ReplacementSurface.cameraTexture;

                afxDrawBegin.Clear();
                afxDrawBegin.name = "AfxInterop: AfxDrawBeginCallBack.";
                afxDrawBegin.IssuePluginEvent(AfxInteropGetRenderEventFunc(), 2);
                afxDrawBegin.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                afxDrawBegin.SetInvertCulling(false);
                afxDrawBegin.SetGlobalVector("_ZParams", zParams);
                afxDrawBegin.DrawMesh(m, GL.GetGPUProjectionMatrix(flipViewZ * Matrix4x4.Ortho(0, 1, 1, 0, -1, 1), true), this.drawColorMaterial, 0, 0);
                afxDrawBegin.DrawMesh(m, GL.GetGPUProjectionMatrix(flipViewZ * Matrix4x4.Ortho(0, 1, 1, 0, afxCamera.nearClipPlane, afxCamera.farClipPlane), true), this.drawDepthMaterial, 0, 0);
                afxDrawBegin.SetInvertCulling(true);
                afxDrawBegin.SetViewProjectionMatrices(afxCamera.worldToCameraMatrix, afxCamera.projectionMatrix);

                afxDrawDepth.Clear();
                afxDrawDepth.name = "AfxInterop: Draw depth buffer color texture.";
                afxDrawDepth.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                afxDrawDepth.SetInvertCulling(false);
                afxDrawBegin.SetGlobalVector("_ZParams", zParams);
                afxDrawDepth.DrawMesh(m, GL.GetGPUProjectionMatrix(flipViewZ * Matrix4x4.Ortho(0, 1, 1, 0, afxCamera.nearClipPlane, afxCamera.farClipPlane), true), this.drawDepthMaterial, 0, 0);
                afxDrawDepth.SetInvertCulling(true);
                afxDrawDepth.SetViewProjectionMatrices(afxCamera.worldToCameraMatrix, afxCamera.projectionMatrix);

                afxDrawEnd.Clear();
                afxDrawEnd.name = "AfxInterop: AfxDrawEndCallBack.";
                afxDrawEnd.CopyTexture(afxCamera.targetTexture, renderTexture);
                afxDrawEnd.IssuePluginEvent(AfxInteropGetRenderEventFunc(), 3);

                afxCameraUpdated = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);

            outColorTextureWasLost = true;
            outSharedColorTextureHandle = IntPtr.Zero;
            outColorDepthTextureWasLost = true;
            outSharedColorDepthTextureHandle = IntPtr.Zero;

            outBeforeTranslucentShadow = false; outAfterTranslucentShadow = false;
            outBeforeTranslucent = false; outAfterTranslucent = false;
            outBeforeHud = false; outAfterHud = false;
        }
    }

    void AfxInteropRenderPass(AfxRenderPassType pass, ref AfxInteropView view)
    {
        // Exceptions must not make it into native code (this is called from native code):
        try
        {

        }
        catch (Exception e)
        {
            Debug.LogException(e, this);

        }
    }

    static string DoAfxInteropCommands_GetCommandArg(IntPtr commands, UInt32 index, UInt32 argIndex)
    {
        return Marshal.PtrToStringAnsi(AfxInteropCommands_GetCommandArg(commands, index, argIndex));
    }

    bool AfxInteropOnViewOverride(ref float Tx, ref float Ty, ref float Tz, ref float Rx, ref float Ry, ref float Rz, ref float Fov)
    {
        if (afxOverrideCameraEnabled && null != afxOverrideCamera)
        {
            Transform transform = afxOverrideCamera.transform;

            AfxInteropVector vec = FromUnityVector(transform.position);

            AfxInteropQAngle rot = FromUnityQuaternion(transform.rotation);

            Tx = vec.X;
            Ty = vec.Y;
            Tz = vec.Z;

            Rx = rot.Pitch;
            Ry = rot.Yaw;
            Rz = rot.Roll;

            Fov = afxOverrideCamera.fieldOfView;

            return true;
        }

        return false;
    }
}
