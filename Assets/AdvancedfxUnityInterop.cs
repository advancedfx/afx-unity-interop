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

        ReleaseTextures();
    }

    void advancedfx.Interop.IImplementation.Render (advancedfx.Interop.IRenderInfo renderInfo) {
		Camera cam = GetComponent<Camera> ();

		if (null == cam)
			return;

        // Currently we require a frame and a depth buffer, this might change later:
        if (!renderInfo.FbTextureId.HasValue || !renderInfo.FbDepthTextureId.HasValue)
            return;

		RenderTexture renderTexture = GetRenderTexture(renderInfo.FbTextureId.Value, renderInfo.FbDepthTextureId.Value);

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

	void advancedfx.Interop.IImplementation.RegisterTexture (advancedfx.Interop.ITextureInfo info) {

		textureIdToTextureInfo [info.TextureID] = info;
	}

	void advancedfx.Interop.IImplementation.ReleaseTexture(UInt32 textureId) {

        this.ReleaseTexture(textureId);
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
        public RenderTextureKey(UInt32 fbTextureId, UInt32 fbDepthTextureId)
        {
            this.FbTextureId = fbTextureId;
            this.FbDepthTextureId = fbDepthTextureId;
        }

		public readonly UInt32 FbTextureId;
        public readonly UInt32 FbDepthTextureId;
	}

    private Dictionary<RenderTextureKey, RenderTexture> renderTextures = new Dictionary<RenderTextureKey, RenderTexture> ();
	private Dictionary<UInt32, List<RenderTextureKey>> textureIdToRenderTextureKeys = new Dictionary<UInt32, List<RenderTextureKey>>();
	private Dictionary<UInt32, advancedfx.Interop.ITextureInfo> textureIdToTextureInfo = new Dictionary<UInt32, advancedfx.Interop.ITextureInfo> ();

	private RenderTexture GetRenderTexture(UInt32 fbTextureId, UInt32 fbDepthTextureId)
	{
		RenderTextureKey key = new RenderTextureKey (fbTextureId, fbDepthTextureId);
		RenderTexture renderTexture = null;

		if (renderTextures.TryGetValue (key, out renderTexture))
			return renderTexture;

        advancedfx.Interop.ITextureInfo fbTextureInfo;
        advancedfx.Interop.ITextureInfo fbDepthTextureInfo;

		if (!(textureIdToTextureInfo.TryGetValue (fbTextureId, out fbTextureInfo) && textureIdToTextureInfo.TryGetValue (fbDepthTextureId, out fbDepthTextureInfo)))
			return null;

		Nullable<RenderTextureDescriptor> rdesc = GetRenderTextureDescriptor (fbTextureInfo, fbDepthTextureInfo);

		if (!rdesc.HasValue)
			return null;

		AfxHookUnityBeginCreateRenderTexture (fbTextureInfo.SharedHandle, fbDepthTextureInfo.SharedHandle);

		renderTexture = new RenderTexture (rdesc.Value);
		renderTexture.Create ();

		AfxHookUnityEndCreateRenderTexture ();

		renderTextures [key] = renderTexture;

		List<RenderTextureKey> list = null;
		if (!textureIdToRenderTextureKeys.TryGetValue (fbTextureId, out list)) {
			list = new List<RenderTextureKey>();
            textureIdToRenderTextureKeys[fbTextureId] = list;
		}
		list.Add (key);

		list = null;
		if (!textureIdToRenderTextureKeys.TryGetValue (fbDepthTextureId, out list)) {
			list = new List<RenderTextureKey>();
            textureIdToRenderTextureKeys[fbDepthTextureId] = list;
		}
		list.Add (key);

		return renderTexture;
	}

	private Nullable<RenderTextureDescriptor> GetRenderTextureDescriptor(advancedfx.Interop.ITextureInfo fbTextureInfo, advancedfx.Interop.ITextureInfo fbDepthTextureInfo)
	{
        if (
            fbTextureInfo.D3D9Width != fbDepthTextureInfo.D3D9Width
            || fbTextureInfo.D3D9Height != fbDepthTextureInfo.D3D9Height
        )
            return null;

        RenderTextureDescriptor desc = new RenderTextureDescriptor((int)fbTextureInfo.D3D9Width, (int)fbTextureInfo.D3D9Height);

        switch (fbTextureInfo.D3D9Format)
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

        switch(fbDepthTextureInfo.D3D9Format) // these might be wrong:
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

        desc.autoGenerateMips = false;
        desc.bindMS = false;
        desc.enableRandomWrite = false;
        desc.sRGB = false; // for windowed CS:GO at least (?).
        desc.msaaSamples = 1;
        desc.useMipMap = false;

        return new Nullable<RenderTextureDescriptor>(desc);
	}

    private void ReleaseTexture(UInt32 textureId)
    {

        List<RenderTextureKey> renderTextureKeys = null;

        if (textureIdToRenderTextureKeys.TryGetValue(textureId, out renderTextureKeys))
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

            textureIdToRenderTextureKeys.Remove(textureId);
        }

        textureIdToTextureInfo.Remove(textureId);
    }

    private void ReleaseTextures()
    {
       while(0 < textureIdToTextureInfo.Count)
       {
            ReleaseTexture(textureIdToTextureInfo.GetEnumerator().Current.Key);
       }
    }
		
}

