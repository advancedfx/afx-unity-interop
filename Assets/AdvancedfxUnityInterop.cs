using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

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

        if (!AfxHookUnityStatus())
            Debug.LogError("AfxHookUnityInit failed.");

        Application.runInBackground = true;

        interOp = new advancedfx.Interop(this);
        interOp.PipeName = pipeName;

        Debug.Log(SystemInfo.graphicsDeviceVersion);
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
        Debug.Log(exception, this);
    }

    void advancedfx.Interop.IImplementation.ConnectionLost() {

        ReleaseSurfaces();
    }

    void advancedfx.Interop.IImplementation.Render (advancedfx.Interop.IRenderInfo renderInfo) {
		Camera cam = GetComponent<Camera> ();

		if (null == cam)
			return;

        // Currently we require a frame and a depth buffer, this might change later:
        if (!renderInfo.FbSurfaceID.HasValue || !renderInfo.FbDepthSurfaceID.HasValue)
            return;

        Debug.Log("FbSurfaceID="+ renderInfo.FbSurfaceID.Value+", FbDepthTextureId="+ renderInfo.FbDepthSurfaceID.Value);
        return;

        RenderTexture renderTexture = GetRenderTexture(renderInfo.FbSurfaceID.Value, renderInfo.FbDepthSurfaceID.Value);

		if (null == renderTexture)
			return;

        cam.targetTexture = renderTexture;

		switch (renderInfo.Type) {
		case advancedfx.Interop.RenderType.Normal:
			{
				cam.Render ();
			}
			break;
		}

	}

    void advancedfx.Interop.IImplementation.RegisterSurface(advancedfx.Interop.ISurfaceInfo info)
    {
        Debug.Log("Registering Surface: " + info.SurfaceID);

        surfaceIDToSurfaceInfo[info.SurfaceID] = info;
    }

    void advancedfx.Interop.IImplementation.ReleaseSurface(UInt32 surfaceID)
    {
        Debug.Log("REleasing Surface: " + surfaceID);

        this.ReleaseSurface(surfaceID);
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
	private static extern bool AfxHookUnityStatus ();

	[DllImport ("AfxHookUnity")]
	private static extern void AfxHookUnityBeginCreateRenderTexture (IntPtr fbSharedHandle, IntPtr fbDepthSharedHandle);

	[DllImport ("AfxHookUnity")]
	private static extern void AfxHookUnityEndCreateRenderTexture ();

	private struct RenderTextureKey
	{
        public RenderTextureKey(UInt32 fbSurfaceID, UInt32 fbDepthSurfaceID)
        {
            this.FbSurfaceID = fbSurfaceID;
            this.FbDepthSurfaceID = fbDepthSurfaceID;
        }

		public readonly UInt32 FbSurfaceID;
        public readonly UInt32 FbDepthSurfaceID;
	}

    private Dictionary<RenderTextureKey, RenderTexture> renderTextures = new Dictionary<RenderTextureKey, RenderTexture> ();
	private Dictionary<UInt32, List<RenderTextureKey>> surfaceIDToRenderTextureKeys = new Dictionary<UInt32, List<RenderTextureKey>>();
	private Dictionary<UInt32, advancedfx.Interop.ISurfaceInfo> surfaceIDToSurfaceInfo = new Dictionary<UInt32, advancedfx.Interop.ISurfaceInfo> ();

	private RenderTexture GetRenderTexture(UInt32 fbSurfaceID, UInt32 fbDepthSufaceId)
	{
		RenderTextureKey key = new RenderTextureKey (fbSurfaceID, fbDepthSufaceId);
		RenderTexture renderTexture = null;

		if (renderTextures.TryGetValue (key, out renderTexture))
			return renderTexture;

        advancedfx.Interop.ISurfaceInfo fbSurfaceInfo;
        advancedfx.Interop.ISurfaceInfo fbDepthSurfaceInfo;

		if (!(surfaceIDToSurfaceInfo.TryGetValue (fbSurfaceID, out fbSurfaceInfo) && surfaceIDToSurfaceInfo.TryGetValue (fbDepthSufaceId, out fbDepthSurfaceInfo)))
			return null;

		Nullable<RenderTextureDescriptor> rdesc = GetRenderTextureDescriptor (fbSurfaceInfo, fbDepthSurfaceInfo);

		if (!rdesc.HasValue)
			return null;

		AfxHookUnityBeginCreateRenderTexture (fbSurfaceInfo.SharedHandle, fbDepthSurfaceInfo.SharedHandle);

		renderTexture = new RenderTexture (rdesc.Value);
		renderTexture.Create ();

		AfxHookUnityEndCreateRenderTexture ();

		renderTextures [key] = renderTexture;

		List<RenderTextureKey> list = null;
		if (!surfaceIDToRenderTextureKeys.TryGetValue (fbSurfaceID, out list)) {
			list = new List<RenderTextureKey>();
            surfaceIDToRenderTextureKeys[fbSurfaceID] = list;
		}
		list.Add (key);

		list = null;
		if (!surfaceIDToRenderTextureKeys.TryGetValue (fbDepthSufaceId, out list)) {
			list = new List<RenderTextureKey>();
            surfaceIDToRenderTextureKeys[fbDepthSufaceId] = list;
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
            return null;

        RenderTextureDescriptor desc = new RenderTextureDescriptor((int)fbSurfaceInfo.Width, (int)fbSurfaceInfo.Height);

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
                return null;
        }

        switch(fbSurfaceInfo.Format) // these might be wrong:
        {
            case advancedfx.Interop.D3DFORMAT.D3DFMT_D16_LOCKABLE:
                desc.depthBufferBits = 16;
                break;
            case advancedfx.Interop.D3DFORMAT.D3DFMT_D24S8:
                desc.depthBufferBits = 24;
                break;
            case advancedfx.Interop.D3DFORMAT.D3DFMT_D16:
                desc.depthBufferBits = 16;
                break;
            case advancedfx.Interop.D3DFORMAT.D3DFMT_D32F_LOCKABLE:
                desc.depthBufferBits = 32;
                break;
            case advancedfx.Interop.D3DFORMAT.D3DFMT_D24FS8:
                desc.depthBufferBits = 24;
                break;
            case advancedfx.Interop.D3DFORMAT.D3DFMT_D32_LOCKABLE:
                desc.depthBufferBits = 32;
                break;
            default:
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

    private void ReleaseSurface(UInt32 surfaceID)
    {
        List<RenderTextureKey> renderTextureKeys = null;

        if (surfaceIDToRenderTextureKeys.TryGetValue(surfaceID, out renderTextureKeys))
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

            surfaceIDToRenderTextureKeys.Remove(surfaceID);
        }

        surfaceIDToSurfaceInfo.Remove(surfaceID);
    }

    private void ReleaseSurfaces()
    {
       while(true)
       {
            IEnumerator<uint> keyEnumerator = surfaceIDToSurfaceInfo.Keys.GetEnumerator();
            if (!keyEnumerator.MoveNext())
                return;

            ReleaseSurface(keyEnumerator.Current);
       }
    }
		
}

