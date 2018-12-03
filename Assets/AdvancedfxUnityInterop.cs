using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

/*
 
QuakeTRS
= /[cos(quakeZ)*cos(quakeY)] * S(quakeX), [cos(quakeZ)*sin(quakeY)*sin(quakeX) -sin(quakeZ)*cos(quakeX)] * S(quakeY), [cos(quakeZ)*sin(quakeY)*cos(quakeX) +sin(quakeZ)*sin(quakeX)]  * S(quakeZ), T(quakeX)\
  |[sin(quakeZ)*cos(quakeY)] * S(quakeX), [sin(quakeZ)*sin(quakeY)*sin(quakeX) +cos(quakeZ)*cos(quakeX)] * S(quakeY), [sin(quakeZ)*sin(quakeY)*cos(quakeX) +cos(quakeZ)*-sin(quakeX)] * S(quakeZ), T(quakeY)|
  |[-sin(quakeY)]            * S(quakeX), [cos(quakeY)*sin(quakeX)]                                      * S(quakeY), [cos(quakeY)*cos(quakeX)]                                       * S(quakeZ), T(quakeZ)|
  \0                                    , 0                                                                         , 0                                                                          , 1        /

UnityTRS
= /[cos(unityZ)*cos(unityY)] * S(unityX), [sin(unitytY)*cos(unityX) + cos(unityZ) * -sin(unityY) * -sin(unityX)] * S(unityY), [sin(unityY) * sin(unityX) + cos(unityZ) * -sin(unityY) * cos(unityX)] * S(unityZ), T(unityX)\
  |[-sin(unityZ)*cos(unitY)] * S(unityX), [cos(unityY)*cos(unityX) - sin(unityZ) * -sin(unityY) * -sin(unityX)]  * S(unityY), [cos(unityY) * sin(unityX) - sin(unityZ) * -sin(unityY) * cos(unityX)] * S(unityZ), T(unityY)|
  |[sin(unityY)]             * S(unityX), [cos(unityZ) * -sin(unityX)]                                           * S(unityY), [cos(unityZ) * cos(unityX)]                                            * S(unityZ), T(unityZ)|
  \0                                    , 0                                                                                 , 0                                                                                 ,         1/

UnityTRS * 254 * unityX = -QuakeTRS * quakeY
UnityTRS * 254 * unityY =  QuakeTRS * quakeZ
UnityTRS * 254 * unityZ =  QuakeTRS * quakeX

QuakeTRS * U2Q * (unityX, unityY, unityZ, 1)^T = UnityTRS * (unityX, unityY, unityZ, 1)^T

----

(quakeX, quakeY, quakeZ, 1)^T
= U2Q * (unityX, unityY, unityZ, 1)^T = (QuakeTRS)^(-1) * UnityTRS * (unityX, unityY, unityZ, 1)^T

Qinv = (QuakeTRS)^(-1)

0 = (QuakeTRS * U2Q - UnityTRS) * (unityX, unityY, unityZ, 1)^T

= /[cos(quakeZ)*cos(quakeY)] * S(quakeX), [cos(quakeZ)*sin(quakeY)*sin(quakeX) -sin(quakeZ)*cos(quakeX)] * S(quakeY), [cos(quakeZ)*sin(quakeY)*cos(quakeX) +sin(quakeZ)*sin(quakeX)]  * S(quakeZ), T(quakeX)\
  |[sin(quakeZ)*cos(quakeY)] * S(quakeX), [sin(quakeZ)*sin(quakeY)*sin(quakeX) +cos(quakeZ)*cos(quakeX)] * S(quakeY), [sin(quakeZ)*sin(quakeY)*cos(quakeX) +cos(quakeZ)*-sin(quakeX)] * S(quakeZ), T(quakeY)|
  |[-sin(quakeY)]            * S(quakeX), [cos(quakeY)*sin(quakeX)]                                      * S(quakeY), [cos(quakeY)*cos(quakeX)]                                       * S(quakeZ), T(quakeZ)|
  \0                                    , 0                                                                         , 0                                                                          , 1        /

*/

public class AdvancedfxUnityInterop : MonoBehaviour, advancedfx.Interop.IImplementation
{
    public string pipeName = "advancedfxInterop";

    public Int32 offset = 0;

    public Int32 Version {
        get {
            return interOp.Version;
        }
    }

    public string PipeName {
        get {
            return interOp.PipeName;
        }
        set {
            interOp.PipeName = value;
        }
    }

    public void Awake() {


        if (IntPtr.Zero == GetModuleHandle("AfxHookUnity.dll"))
        {
            Debug.LogError("AfxHookUnity.dll is not injected. It needs to be injected into Unity early.");
        }

        if (!AfxHookUnityInit(0))
            Debug.LogError("AfxHookUnityInit failed (version mismatch or init failed).");

        Application.runInBackground = true; // don't sleep when not having focus
        QualitySettings.vSyncCount = 0; // render as fast as possible

        interOp = new advancedfx.Interop(this);
        interOp.PipeName = pipeName;
    }

    public void OnEnable() {

        interOp.OnEnable();
    }

    public void OnDisable() {
        interOp.OnDisable();
    }

    public void OnDestroy() {

    }

    public void Update()
    {
        interOp.Update();
    }

    //
    // advancedfx.Interop.IImplentation:

    void advancedfx.Interop.ILogging.Log(object message) {
        Debug.Log(message, this);
    }

    void advancedfx.Interop.ILogging.LogException(Exception exception) {
        Debug.LogError(exception, this);
    }

    void advancedfx.Interop.IImplementation.ConnectionLost() {

        ReleaseSurfaces();

        deltaTime = 0;
    }

    void advancedfx.Interop.IImplementation.Render (advancedfx.Interop.IRenderInfo renderInfo) {

        if (null != renderInfo.FrameInfo && 0 != renderInfo.FrameInfo.AbsoluteFrameTime)
        {
            double time = deltaTime + renderInfo.FrameInfo.AbsoluteFrameTime;

            double absTime = Math.Max(0.0, time);

            int frameRate = 0.0 != absTime ? (int)Math.Floor(1.0 / absTime) : 0 ;

            if(0 < frameRate)
            {
                Time.timeScale = 1;
                Time.captureFramerate = frameRate;
            }
            else
            {
                // Can't go back in time, so pause.
                Time.timeScale = 0;
                Time.captureFramerate = 0;
            }

            deltaTime = time - (absTime - (0 != frameRate ?  1.0 / frameRate : 0));

            if(1 < Math.Abs(deltaTime))
            {
                Debug.LogError("Clock is one second off, resetting.");
                deltaTime = 0;
            }
        }
        else
        {
            Debug.Log("No time info available.");

            Time.timeScale = 1;
            Time.captureFramerate = 0;
        }

        Camera cam = GetComponent<Camera> ();

        if (null == cam)
        {
            Debug.LogError("No camera set on component.");
            return;
        }

        if (!renderInfo.FbSurfaceHandle.HasValue)
        {
            Debug.LogError("Back buffer unknown.");
            return;
        }
        if(!renderInfo.FbDepthSurfaceHandle.HasValue)
        {
            Debug.LogError("Depth stencil unknown.");
            return;
        }

        RenderTexture renderTexture = GetRenderTexture(renderInfo.FbSurfaceHandle.Value, renderInfo.FbDepthSurfaceHandle.Value);

        if (null == renderTexture)
        {
            Debug.LogError("GetRenderTexture failed.");
            return;
        }

        //Debug.Log("FbSurfaceHandle=" + renderInfo.FbSurfaceHandle.Value + ", FbDepthTextureHandle=" + renderInfo.FbDepthSurfaceHandle.Value);

        switch (renderInfo.Type)
        {
            case advancedfx.Interop.RenderType.Normal:
                {
                    float unityToQuakeScale = 1 / 2.54f * 100;
                    Matrix4x4 unityToQuakeScaleV = Matrix4x4.Scale(new Vector3(unityToQuakeScale, unityToQuakeScale, unityToQuakeScale));

                    Matrix4x4 unityProjection = cam.projectionMatrix;

                    if (null != renderInfo.FrameInfo)
                    {
                        advancedfx.Interop.Afx4x4 afxWorldToView = renderInfo.FrameInfo.WorldToViewMatrix;
                        Matrix4x4 d3d9QuakeWorldToView = new Matrix4x4();
                        d3d9QuakeWorldToView[0, 0] = afxWorldToView.M00;
                        d3d9QuakeWorldToView[0, 1] = afxWorldToView.M01;
                        d3d9QuakeWorldToView[0, 2] = afxWorldToView.M02;
                        d3d9QuakeWorldToView[0, 3] = afxWorldToView.M03;
                        d3d9QuakeWorldToView[1, 0] = afxWorldToView.M10;
                        d3d9QuakeWorldToView[1, 1] = afxWorldToView.M12;
                        d3d9QuakeWorldToView[1, 2] = afxWorldToView.M13;
                        d3d9QuakeWorldToView[1, 3] = afxWorldToView.M13;
                        d3d9QuakeWorldToView[2, 0] = afxWorldToView.M20;
                        d3d9QuakeWorldToView[2, 1] = afxWorldToView.M21;
                        d3d9QuakeWorldToView[2, 2] = afxWorldToView.M22;
                        d3d9QuakeWorldToView[2, 3] = afxWorldToView.M23;
                        d3d9QuakeWorldToView[3, 0] = afxWorldToView.M30;
                        d3d9QuakeWorldToView[3, 1] = afxWorldToView.M31;
                        d3d9QuakeWorldToView[3, 2] = afxWorldToView.M32;
                        d3d9QuakeWorldToView[3, 3] = afxWorldToView.M33;

                        advancedfx.Interop.Afx4x4 afxWorldToScreen = renderInfo.FrameInfo.WorldToScreenMatrix;
                        Matrix4x4 d3d9QuakeWorldToScreen = new Matrix4x4();
                        d3d9QuakeWorldToScreen[0, 0] = afxWorldToScreen.M00;
                        d3d9QuakeWorldToScreen[0, 1] = afxWorldToScreen.M01;
                        d3d9QuakeWorldToScreen[0, 2] = afxWorldToScreen.M02;
                        d3d9QuakeWorldToScreen[0, 3] = afxWorldToScreen.M03;
                        d3d9QuakeWorldToScreen[1, 0] = afxWorldToScreen.M10;
                        d3d9QuakeWorldToScreen[1, 1] = afxWorldToScreen.M11;
                        d3d9QuakeWorldToScreen[1, 2] = afxWorldToScreen.M12;
                        d3d9QuakeWorldToScreen[1, 3] = afxWorldToScreen.M13;
                        d3d9QuakeWorldToScreen[2, 0] = afxWorldToScreen.M20;
                        d3d9QuakeWorldToScreen[2, 1] = afxWorldToScreen.M21;
                        d3d9QuakeWorldToScreen[2, 2] = afxWorldToScreen.M22;
                        d3d9QuakeWorldToScreen[2, 3] = afxWorldToScreen.M23;
                        d3d9QuakeWorldToScreen[3, 0] = afxWorldToScreen.M30;
                        d3d9QuakeWorldToScreen[3, 1] = afxWorldToScreen.M31;
                        d3d9QuakeWorldToScreen[3, 2] = afxWorldToScreen.M32;
                        d3d9QuakeWorldToScreen[3, 3] = afxWorldToScreen.M33;

                        Matrix4x4 unityToQuake = new Matrix4x4();
                        unityToQuake[0, 0] = 0; unityToQuake[0, 1] = 0; unityToQuake[0, 2] = 1; unityToQuake[0, 3] = 0;
                        unityToQuake[1, 0] = -1; unityToQuake[1, 1] = 0; unityToQuake[1, 2] = 0; unityToQuake[1, 3] = 0;
                        unityToQuake[2, 0] = 0; unityToQuake[2, 1] = 1; unityToQuake[2, 2] = 0; unityToQuake[2, 3] = 0;
                        unityToQuake[3, 0] = 0; unityToQuake[3, 1] = 0; unityToQuake[3, 2] = 0; unityToQuake[3, 3] = 1;

                        Matrix4x4 unityToWorldView = d3d9QuakeWorldToView * (unityToQuake * unityToQuakeScaleV);

                        Matrix4x4 unityToWorldViewInverse = unityToWorldView.inverse;

                        Vector4 lastCol = unityToWorldViewInverse.GetColumn(3);
                        cam.transform.position = new Vector3(lastCol.x, lastCol.y, lastCol.z);
                        cam.transform.rotation = unityToWorldViewInverse.rotation;
                        cam.transform.localScale = new Vector3(1, 1, 1);

                        int width = renderInfo.FrameInfo.Width;
                        int height = renderInfo.FrameInfo.Height;

                        cam.worldToCameraMatrix = unityToWorldView;

                        unityProjection = ((d3d9QuakeWorldToScreen * (unityToQuake * unityToQuakeScaleV)) * unityToWorldView.inverse);

                        float C = unityProjection[2, 2]; // - (f+n) /(f-n)
                        float D = unityProjection[2, 3]; // - 2*f*n / (f-n)

                        Debug.Log(D / (C - 1)+" / "+D / (C + 1));

                        cam.nearClipPlane = D / (C - 1);
                        cam.farClipPlane = D / (C + 1);

                        cam.nearClipPlane = cam.nearClipPlane / cam.farClipPlane;
                        cam.farClipPlane = 1;

                        cam.pixelRect = new Rect(0, 0, width, height);

                        Matrix4x4 d3dToScreen = new Matrix4x4();
                        d3dToScreen[0, 0] = 1; d3dToScreen[0, 1] = 0; d3dToScreen[0, 2] = 0; d3dToScreen[0, 3] = 0;
                        d3dToScreen[1, 0] = 0; d3dToScreen[1, 1] = -1 * height / (float)width; d3dToScreen[1, 2] = 0; d3dToScreen[1, 3] = -1 + height / (float)width; // (1-x) * 1/a -1
                        d3dToScreen[2, 0] = 0; d3dToScreen[2, 1] = 0; d3dToScreen[2, 2] = 1; d3dToScreen[2, 3] = 0;
                        d3dToScreen[3, 0] = 0; d3dToScreen[3, 1] = 0; d3dToScreen[3, 2] = 0; d3dToScreen[3, 3] = 1;

                        Matrix4x4 correction = new Matrix4x4();
                        correction[0, 0] = 1; correction[0, 1] = 0; correction[0, 2] = 0; correction[0, 3] = 0;
                        correction[1, 0] = 0; correction[1, 1] = 1; correction[1, 2] = 0; correction[1, 3] = 0;
                        correction[2, 0] = 0; correction[2, 1] = 0; correction[2, 2] = 1; correction[2, 3] = 0;
                        correction[3, 0] = 0; correction[3, 1] = 0; correction[3, 2] = 0; correction[3, 3] = 1;

                        cam.projectionMatrix = correction * (d3dToScreen * unityProjection);

                        const double Rad2Deg = 180.0 / Math.PI;
                        float horizontalFovRad = (float)Math.Atan(1.0 / unityProjection[0, 0]) * 2.0f;
                        float verticalFovDeg = (float)(2 * Math.Atan(Math.Tan(horizontalFovRad / 2.0) * height / (float)width) * Rad2Deg);

                        //Debug.Log(verticalFovDeg);

                        cam.fieldOfView = fov;
                    }

                    CommandBuffer afxWait = new CommandBuffer();
                    afxWait.name = "AfxHookUnity: wait for GPU sin(z)nchronization.";
                    afxWait.IssuePluginEvent(AfxHookUnityGetRenderEventFunc(), 1);

                    CommandBuffer afxTargets = new CommandBuffer();
                    afxTargets.name = "AfxHookUnity: Enable CreateRenderTargetView hooks.";
                    afxTargets.IssuePluginEventAndData(AfxHookUnityGetRenderEventAndDataFunc(), 2, renderInfo.FbSurfaceHandle.Value); // Color buffer
                    // afxTargets.IssuePluginEventAndData(AfxHookUnityGetRenderEventAndDataFunc(), 3, renderInfo.FbDepthSurfaceHandle.Value); // Depth buffer ( not supported yet )

                    Graphics.ExecuteCommandBuffer(afxTargets);

                    cam.AddCommandBuffer(CameraEvent.AfterEverything, afxWait);

                    GL.invertCulling = true;
                    cam.targetTexture = renderTexture;
                    cam.Render();

                    AfxHookUnityWaitOne();

                    cam.RemoveCommandBuffer(CameraEvent.AfterEverything, afxWait);

                    cam.projectionMatrix = unityProjection;
                    cam.targetTexture = null;
                    GL.invertCulling = false;
                }
                break;
        }
    }

    void advancedfx.Interop.IImplementation.RegisterSurface(advancedfx.Interop.ISurfaceInfo info)
    {
        Debug.Log("Registering Surface: " + info.SharedHandle);

        surfaceHandleToSurfaceInfo[info.SharedHandle] = info;
    }

    void advancedfx.Interop.IImplementation.ReleaseSurface(IntPtr sharedHandle)
    {
        Debug.Log("REleasing Surface: " + sharedHandle);

        this.ReleaseSurface(sharedHandle);
    }


    void advancedfx.Interop.IImplementation.RegisterTexture (advancedfx.Interop.ITextureInfo info) {
	}

	void advancedfx.Interop.IImplementation.ReleaseTexture(UInt32 textureId) {
	}

    //
    // Private:

    private advancedfx.Interop interOp;

    [StructLayout(LayoutKind.Sequential)] // Be aware of 32 bit vs 64 bit here, LayoutKind.Explicit is tricky.
    public struct AFxHookUnityTextureInfo
    {
        public IntPtr Internal;
        public IntPtr InternalHigh;
        //public uint Offset;
        //public uint OffsetHigh;
        public IntPtr Pointer;
        public IntPtr hEvent;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport ("AfxHookUnity")]
	private static extern bool AfxHookUnityInit(int version);

    [DllImport("AfxHookUnity")]
    private static extern void AfxHookUnityWaitOne();

    [DllImport("AfxHookUnity")]
    private static extern IntPtr AfxHookUnityGetRenderEventFunc();

    [DllImport("AfxHookUnity")]
    private static extern IntPtr AfxHookUnityGetRenderEventAndDataFunc();

    private struct RenderTextureKey
	{
        public RenderTextureKey(IntPtr fbSurfaceHandle, IntPtr fbDepthSurfaceHandle)
        {
            this.FbSurfaceHandle = fbSurfaceHandle;
            this.FbDepthSurfaceHandle = fbDepthSurfaceHandle;
        }

		public readonly IntPtr FbSurfaceHandle;
        public readonly IntPtr FbDepthSurfaceHandle;
	}

    private double deltaTime = 0.0;

    private Dictionary<RenderTextureKey, RenderTexture> renderTextures = new Dictionary<RenderTextureKey, RenderTexture> ();
	private Dictionary<IntPtr, List<RenderTextureKey>> surfaceHandleToRenderTextureKeys = new Dictionary<IntPtr, List<RenderTextureKey>>();
	private Dictionary<IntPtr, advancedfx.Interop.ISurfaceInfo> surfaceHandleToSurfaceInfo = new Dictionary<IntPtr, advancedfx.Interop.ISurfaceInfo> ();

	private RenderTexture GetRenderTexture(IntPtr fbSurfaceHandle, IntPtr fbDepthSufaceHandle)
	{
		RenderTextureKey key = new RenderTextureKey (fbSurfaceHandle, fbDepthSufaceHandle);
		RenderTexture renderTexture = null;

		if (renderTextures.TryGetValue (key, out renderTexture))
			return renderTexture;

        advancedfx.Interop.ISurfaceInfo fbSurfaceInfo;
        advancedfx.Interop.ISurfaceInfo fbDepthSurfaceInfo;

		if (!(surfaceHandleToSurfaceInfo.TryGetValue (fbSurfaceHandle, out fbSurfaceInfo) && surfaceHandleToSurfaceInfo.TryGetValue (fbDepthSufaceHandle, out fbDepthSurfaceInfo)))
			return null;

		Nullable<RenderTextureDescriptor> rdesc = GetRenderTextureDescriptor (fbSurfaceInfo, fbDepthSurfaceInfo);

		if (!rdesc.HasValue)
			return null;

		renderTexture = new RenderTexture (rdesc.Value);

        renderTextures[key] = renderTexture;

		List<RenderTextureKey> list = null;
		if (!surfaceHandleToRenderTextureKeys.TryGetValue (fbSurfaceHandle, out list)) {
			list = new List<RenderTextureKey>();
            surfaceHandleToRenderTextureKeys[fbSurfaceHandle] = list;
		}
		list.Add (key);

		list = null;
		if (!surfaceHandleToRenderTextureKeys.TryGetValue (fbDepthSufaceHandle, out list)) {
			list = new List<RenderTextureKey>();
            surfaceHandleToRenderTextureKeys[fbDepthSufaceHandle] = list;
		}
		list.Add (key);

		return renderTexture;
	}

	private Nullable<RenderTextureDescriptor> GetRenderTextureDescriptor(advancedfx.Interop.ISurfaceInfo fbSurfaceInfo, advancedfx.Interop.ISurfaceInfo fbDepthSurfaceInfo)
	{
        if (
            fbSurfaceInfo.Width != fbDepthSurfaceInfo.Width
            || fbSurfaceInfo.Height != fbDepthSurfaceInfo.Height
        )
        {
            Debug.LogError("Back buffer and depth stencil dimensions don't match");
            return null;
        }

        RenderTextureDescriptor desc = new RenderTextureDescriptor((int)fbSurfaceInfo.Width, (int)fbSurfaceInfo.Height);

        Debug.Log("GetRenderTextureDescriptor back buffer: " + fbSurfaceInfo.Format+" ("+ fbSurfaceInfo.Width+","+ fbSurfaceInfo.Height + ")");

        switch (fbSurfaceInfo.Format)
        {
            case advancedfx.Interop.D3DFORMAT.D3DFMT_A8R8G8B8:
                desc.colorFormat = RenderTextureFormat.ARGB32;
                break;
            case advancedfx.Interop.D3DFORMAT.D3DFMT_R5G6B5:
                desc.colorFormat = RenderTextureFormat.RGB565;
                break;
            case advancedfx.Interop.D3DFORMAT.D3DFMT_A1R5G5B5:
                desc.colorFormat = RenderTextureFormat.ARGB1555;
                break;
            case advancedfx.Interop.D3DFORMAT.D3DFMT_A4R4G4B4:
                desc.colorFormat = RenderTextureFormat.ARGB4444;
                break;
            case advancedfx.Interop.D3DFORMAT.D3DFMT_A2R10G10B10:
                desc.colorFormat = RenderTextureFormat.ARGB2101010;
                break;
            default:
                Debug.LogError("Unknown back buffer format: "+ fbSurfaceInfo.Format);
                return null;
        }

        Debug.Log("GetRenderTextureDescriptor depth stencil: "+ fbDepthSurfaceInfo.Format + " (" + fbDepthSurfaceInfo.Width + "," + fbDepthSurfaceInfo.Height + ")");

        switch (fbDepthSurfaceInfo.Format) // these might be wrong:
        {
            case advancedfx.Interop.D3DFORMAT.D3DFMT_D16_LOCKABLE:
                desc.depthBufferBits = 16;
                break;
            case advancedfx.Interop.D3DFORMAT.D3DFMT_D24S8:
                desc.depthBufferBits = 32;
                break;
            case advancedfx.Interop.D3DFORMAT.D3DFMT_D16:
                desc.depthBufferBits = 16;
                break;
            case advancedfx.Interop.D3DFORMAT.D3DFMT_D32F_LOCKABLE:
                desc.depthBufferBits = 32;
                break;
            case advancedfx.Interop.D3DFORMAT.D3DFMT_D24FS8:
                desc.depthBufferBits = 32;
                break;
            case advancedfx.Interop.D3DFORMAT.D3DFMT_D32_LOCKABLE:
                desc.depthBufferBits = 32;
                break;
            case advancedfx.Interop.D3DFORMAT.D3DFMT_INTZ:
                desc.depthBufferBits = 32;
                break;
            default:
                Debug.LogError("Unknown depth stencil format: " + fbDepthSurfaceInfo.Format);
                return null;
        }

        desc.autoGenerateMips = false;
        desc.bindMS = false;
        desc.enableRandomWrite = false;
        desc.sRGB = false; // for windowed CS:GO at least (?).
        desc.msaaSamples = 1;
        desc.useMipMap = false;

        return new Nullable<RenderTextureDescriptor>(desc);
	}

    private void ReleaseSurface(IntPtr surfaceHandle)
    {
        List<RenderTextureKey> renderTextureKeys = null;

        if (surfaceHandleToRenderTextureKeys.TryGetValue(surfaceHandle, out renderTextureKeys))
        {

            foreach (RenderTextureKey renderTextureKey in renderTextureKeys)
            {
                RenderTexture renderTexture = null;

                if (renderTextures.TryGetValue(renderTextureKey, out renderTexture))
                {

                    renderTexture.Release();

                    renderTextures.Remove(renderTextureKey);
                }
            }

            surfaceHandleToRenderTextureKeys.Remove(surfaceHandle);
        }

        surfaceHandleToSurfaceInfo.Remove(surfaceHandle);
    }

    private void ReleaseSurfaces()
    {
       while(true)
       {
            IEnumerator<IntPtr> keyEnumerator = surfaceHandleToSurfaceInfo.Keys.GetEnumerator();
            if (!keyEnumerator.MoveNext())
                return;

            ReleaseSurface(keyEnumerator.Current);
       }
    }	
}

