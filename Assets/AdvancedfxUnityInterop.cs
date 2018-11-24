using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

public class AdvancedfxUnityInterop : MonoBehaviour, advancedfx.Interop.IImplementation
{
    public string pipeName = "advancedfxInterop";

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
        if(IntPtr.Zero == GetModuleHandle("AfxHookUnity.dll"))
        {
            Debug.LogError("AfxHookUnity.dll is not injected. It needs to be injected into Unity early.");
        }

        if (!AfxHookUnityInit(0))
            Debug.LogError("AfxHookUnityInit failed (version mismatch or init failed).");

        Application.runInBackground = true; // don't sleep when not having focus
        QualitySettings.vSyncCount = 0; // render as fast as possible

        interOp = new advancedfx.Interop(this);
        interOp.PipeName = pipeName;

        Debug.Log(SystemInfo.graphicsDeviceVersion);
    }

    public void OnEnable() {
        CommandBuffer commandBuffer = new CommandBuffer();
        commandBuffer.name = "AfxHookUnityWaitForGPU synchronization";
        commandBuffer.IssuePluginEvent(GetRenderEventFunc(), 1);
        GetComponent<Camera>().AddCommandBuffer(CameraEvent.AfterEverything, commandBuffer);

        interOp.OnEnable();
    }

    public void OnDisable() {
        interOp.OnDisable();
    }

    public void OnDestroy() {

    }

    public void OnPreRender()
    {
        GL.InvalidateState();
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

                Debug.Log(Time.captureFramerate);
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

		switch (renderInfo.Type) {
		case advancedfx.Interop.RenderType.Normal:
			{
                    AfxHookUnityBeginCreateRenderTexture(renderInfo.FbSurfaceHandle.Value, renderInfo.FbDepthSurfaceHandle.Value);

                    cam.targetTexture = renderTexture;

                    renderTexture.Create();
                    cam.Render ();

                    AfxHookUnityWaitOne();

                    cam.targetTexture = null;

                    AfxHookUnityEndCreateRenderTexture();
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

	[DllImport ("AfxHookUnity")]
	private static extern void AfxHookUnityBeginCreateRenderTexture (IntPtr fbSharedHandle, IntPtr fbDepthSharedHandle);

	[DllImport ("AfxHookUnity")]
	private static extern void AfxHookUnityEndCreateRenderTexture ();

    [DllImport("AfxHookUnity")]
    private static extern IntPtr GetRenderEventFunc();

    [DllImport("AfxHookUnity")]
    private static extern void AfxHookUnityWaitOne();

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

		AfxHookUnityBeginCreateRenderTexture (fbSurfaceInfo.SharedHandle, fbDepthSurfaceInfo.SharedHandle);

		renderTexture = new RenderTexture (rdesc.Value);       

        AfxHookUnityEndCreateRenderTexture ();

        renderTextures [key] = renderTexture;

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

        Debug.Log("GetRenderTextureDescriptor back buffer: " + fbSurfaceInfo.Format);

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

        Debug.Log("GetRenderTextureDescriptor depth stencil: "+ fbDepthSurfaceInfo.Format);

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

        // TODO: MSAA should be handled maybe.
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

