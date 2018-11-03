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

		RenderTexture renderTexture = GetRenderTexture(renderInfo.FbTextureId, renderInfo.FbDepthTextureId);

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
            fbDepthTextureInfo.Width != fbTextureInfo.Width
            || fbTextureInfo.Height != fbDepthTextureInfo.Height
        )
            return null;

        RenderTextureDescriptor desc = new RenderTextureDescriptor(Width, Height);

        switch (fbTextureInfo.Format)
        {
            case Advancedfx.Interop.D3DFORMAT.D3DFMT_A8R8G8B8:
                desc.colorFormat = RenderTextureFormat.ARGB32;
                break;
            case Advancedfx.Interop.D3DFORMAT.D3DFMT_R5G6B5:
                desc.colorFormat = RenderTextureFormat.RGB565;
                break;
            case Advancedfx.Interop.D3DFORMAT.D3DFMT_A1R5G5B5:
                desc.colorFormat = RenderTextureFormat.ARGB1555;
                break;
            case Advancedfx.Interop.D3DFORMAT.D3DFMT_A4R4G4B4:
                desc.colorFormat = RenderTextureFormat.ARGB4444;
                break;
            case Advancedfx.Interop.D3DFORMAT.D3DFMT_A8:
                desc.colorFormat = RenderTextureFormat.R8; // Might be wrong.
                break;

/*
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

            //D3DFMT_FORCE_DWORD          =0x7fffffff*/
        }

        return null;

        return desc;
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

