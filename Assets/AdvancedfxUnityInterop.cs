using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class AdvancedfxUnityInterop :  MonoBehaviour, advancedfx.Interop.IImplementation
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
        //if (!AfxHookUnityStatus ())
        //	Debug.LogError ("AfxHookUnityInit failed.");

        Application.runInBackground = true;

        interOp = new advancedfx.Interop(this);
		interOp.PipeName = pipeName;

		Debug.Log (SystemInfo.graphicsDeviceVersion);
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
		Debug.Log (message, this);
	}

	void advancedfx.Interop.ILogging.LogException(Exception exception) {
		Debug.Log (exception, this);
	}

	void advancedfx.Interop.IImplementation.Render (advancedfx.Interop.IRenderInfo renderInfo) {
		Camera cam = GetComponent<Camera> ();

		if (null == cam)
			return;

		RenderTexture renderTexture = GetRenderTexture(renderInfo.SurfaceSharedHandle, renderInfo.DepthSurfaceSharedHandle);

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

	void advancedfx.Interop.IImplementation.RegisterSurface (advancedfx.Interop.ISurfaceInfo surfaceInfo) {

		surfaceSharedHandleToSurfaceInfo [surfaceInfo.SharedHandle] = surfaceInfo;
	}

	void advancedfx.Interop.IImplementation.DestroySurface (IntPtr sharedHandle) {

		List<RenderTextureKey> renderTextureKeys = null;

		if (surfaceSharedHandleToRenderTextureKeys.TryGetValue (sharedHandle, out renderTextureKeys)) {

			foreach (RenderTextureKey renderTextureKey in renderTextureKeys) {

				RenderTexture renderTexture = null;

				if (renderTextures.TryGetValue(renderTextureKey, out renderTexture)) {

					renderTexture.Release ();

					renderTextures.Remove (renderTextureKey);
				}
			}

			surfaceSharedHandleToRenderTextureKeys.Remove (sharedHandle);
		}

		surfaceSharedHandleToSurfaceInfo.Remove (sharedHandle);
	}

	//
	// Private:

	private advancedfx.Interop interOp;


	private enum D3DFORMAT : uint {
		D3DFMT_UNKNOWN              =  0,

		D3DFMT_R8G8B8               = 20,
		D3DFMT_A8R8G8B8             = 21,
		D3DFMT_X8R8G8B8             = 22,
		D3DFMT_R5G6B5               = 23,
		D3DFMT_X1R5G5B5             = 24,
		D3DFMT_A1R5G5B5             = 25,
		D3DFMT_A4R4G4B4             = 26,
		D3DFMT_R3G3B2               = 27,
		D3DFMT_A8                   = 28,
		D3DFMT_A8R3G3B2             = 29,
		D3DFMT_X4R4G4B4             = 30,
		D3DFMT_A2B10G10R10          = 31,
		D3DFMT_A8B8G8R8             = 32,
		D3DFMT_X8B8G8R8             = 33,
		D3DFMT_G16R16               = 34,
		D3DFMT_A2R10G10B10          = 35,
		D3DFMT_A16B16G16R16         = 36,

		D3DFMT_A8P8                 = 40,
		D3DFMT_P8                   = 41,

		D3DFMT_L8                   = 50,
		D3DFMT_A8L8                 = 51,
		D3DFMT_A4L4                 = 52,

		D3DFMT_V8U8                 = 60,
		D3DFMT_L6V5U5               = 61,
		D3DFMT_X8L8V8U8             = 62,
		D3DFMT_Q8W8V8U8             = 63,
		D3DFMT_V16U16               = 64,
		D3DFMT_A2W10V10U10          = 67,

		//D3DFMT_UYVY                 = MAKEFOURCC('U', 'Y', 'V', 'Y'),
		//D3DFMT_R8G8_B8G8            = MAKEFOURCC('R', 'G', 'B', 'G'),
		//D3DFMT_YUY2                 = MAKEFOURCC('Y', 'U', 'Y', '2'),
		//DFMT_G8R8_G8B8            = MAKEFOURCC('G', 'R', 'G', 'B'),
		//D3DFMT_DXT1                 = MAKEFOURCC('D', 'X', 'T', '1'),
		//D3DFMT_DXT2                 = MAKEFOURCC('D', 'X', 'T', '2'),
		//D3DFMT_DXT3                 = MAKEFOURCC('D', 'X', 'T', '3'),
		//D3DFMT_DXT4                 = MAKEFOURCC('D', 'X', 'T', '4'),
		//D3DFMT_DXT5                 = MAKEFOURCC('D', 'X', 'T', '5'),

		D3DFMT_D16_LOCKABLE         = 70,
		D3DFMT_D32                  = 71,
		D3DFMT_D15S1                = 73,
		D3DFMT_D24S8                = 75,
		D3DFMT_D24X8                = 77,
		D3DFMT_D24X4S4              = 79,
		D3DFMT_D16                  = 80,

		D3DFMT_D32F_LOCKABLE        = 82,
		D3DFMT_D24FS8               = 83,

		//#if !defined(D3D_DISABLE_9EX)
		D3DFMT_D32_LOCKABLE         = 84,
		D3DFMT_S8_LOCKABLE          = 85,
		//#endif // !D3D_DISABLE_9EX

		D3DFMT_L16                  = 81,

		D3DFMT_VERTEXDATA           =100,
		D3DFMT_INDEX16              =101,
		D3DFMT_INDEX32              =102,

		D3DFMT_Q16W16V16U16         =110,

		//D3DFMT_MULTI2_ARGB8         = MAKEFOURCC('M','E','T','1'),

		D3DFMT_R16F                 = 111,
		D3DFMT_G16R16F              = 112,
		D3DFMT_A16B16G16R16F        = 113,

		D3DFMT_R32F                 = 114,
		D3DFMT_G32R32F              = 115,
		D3DFMT_A32B32G32R32F        = 116,

		D3DFMT_CxV8U8               = 117,

		//#if !defined(D3D_DISABLE_9EX)
		D3DFMT_A1                   = 118,
		D3DFMT_A2B10G10R10_XR_BIAS  = 119,
		D3DFMT_BINARYBUFFER         = 199,
		//#endif // !D3D_DISABLE_9EX

		//D3DFMT_FORCE_DWORD          =0x7fffffff
		Notsupported = 0x7fffffff
	};


	private enum D3DMULTISAMPLE_TYPE : uint { 
		D3DMULTISAMPLE_NONE          = 0,
		D3DMULTISAMPLE_NONMASKABLE   = 1,
		D3DMULTISAMPLE_2_SAMPLES     = 2,
		D3DMULTISAMPLE_3_SAMPLES     = 3,
		D3DMULTISAMPLE_4_SAMPLES     = 4,
		D3DMULTISAMPLE_5_SAMPLES     = 5,
		D3DMULTISAMPLE_6_SAMPLES     = 6,
		D3DMULTISAMPLE_7_SAMPLES     = 7,
		D3DMULTISAMPLE_8_SAMPLES     = 8,
		D3DMULTISAMPLE_9_SAMPLES     = 9,
		D3DMULTISAMPLE_10_SAMPLES    = 10,
		D3DMULTISAMPLE_11_SAMPLES    = 11,
		D3DMULTISAMPLE_12_SAMPLES    = 12,
		D3DMULTISAMPLE_13_SAMPLES    = 13,
		D3DMULTISAMPLE_14_SAMPLES    = 14,
		D3DMULTISAMPLE_15_SAMPLES    = 15,
		D3DMULTISAMPLE_16_SAMPLES    = 16,
		//D3DMULTISAMPLE_FORCE_DWORD   = 0xffffffff
		Notsupported = 0xffffffff
	};

    [DllImport ("AfxHookUnity")]
	private static extern bool AfxHookUnityStatus ();

	[DllImport ("AfxHookUnity")]
	private static extern void AfxHookUnityBeginCreateRenderTexture (IntPtr surfaceSharedHandle, IntPtr depthSurfaceSharedHandle);

	[DllImport ("AfxHookUnity")]
	private static extern void AfxHookUnityEndCreateRenderTexture ();

	private struct RenderTextureKey
	{
		public RenderTextureKey(IntPtr surfaceSharedHandle, IntPtr depthSurfaceSharedHandle)
		{
			this.surfaceSharedHandle = surfaceSharedHandle;
			this.depthSurfaceSharedHandle = depthSurfaceSharedHandle;
		}

		private IntPtr surfaceSharedHandle;
		private IntPtr depthSurfaceSharedHandle;
	}

	private Dictionary<RenderTextureKey, RenderTexture> renderTextures = new Dictionary<RenderTextureKey, RenderTexture> ();
	private Dictionary<IntPtr, List<RenderTextureKey>> surfaceSharedHandleToRenderTextureKeys = new Dictionary<IntPtr, List<RenderTextureKey>>();
	private Dictionary<IntPtr, advancedfx.Interop.ISurfaceInfo> surfaceSharedHandleToSurfaceInfo = new Dictionary<IntPtr, advancedfx.Interop.ISurfaceInfo> ();

	private RenderTexture GetRenderTexture(IntPtr surfaceSharedHandle, IntPtr depthSurfaceSharedHandle)
	{
		RenderTextureKey key = new RenderTextureKey (surfaceSharedHandle, depthSurfaceSharedHandle);
		RenderTexture renderTexture = null;

		if (renderTextures.TryGetValue (key, out renderTexture))
			return renderTexture;

        advancedfx.Interop.ISurfaceInfo surfaceInfo = null;
        advancedfx.Interop.ISurfaceInfo depthSurfaceInfo = null;

		if (!(surfaceSharedHandleToSurfaceInfo.TryGetValue (surfaceSharedHandle, out surfaceInfo) && surfaceSharedHandleToSurfaceInfo.TryGetValue (depthSurfaceSharedHandle, out depthSurfaceInfo)))
			return null;

		Nullable<RenderTextureDescriptor> rdesc = GetRenderTextureDescriptor (surfaceInfo, depthSurfaceInfo);

		if (!rdesc.HasValue)
			return null;

		AfxHookUnityBeginCreateRenderTexture (surfaceSharedHandle, depthSurfaceSharedHandle);

		renderTexture = new RenderTexture (rdesc.Value);
		renderTexture.Create ();

		AfxHookUnityEndCreateRenderTexture ();

		renderTextures [key] = renderTexture;

		List<RenderTextureKey> list = null;
		if (!surfaceSharedHandleToRenderTextureKeys.TryGetValue (surfaceSharedHandle, out list)) {
			list = new List<RenderTextureKey>();
			surfaceSharedHandleToRenderTextureKeys [surfaceSharedHandle] = list;
		}
		list.Add (key);

		list = null;
		if (!surfaceSharedHandleToRenderTextureKeys.TryGetValue (depthSurfaceSharedHandle, out list)) {
			list = new List<RenderTextureKey>();
			surfaceSharedHandleToRenderTextureKeys [depthSurfaceSharedHandle] = list;
		}
		list.Add (key);

		return renderTexture;
	}

	private Nullable<RenderTextureDescriptor> GetRenderTextureDescriptor(advancedfx.Interop.ISurfaceInfo surfaceInfo, advancedfx.Interop.ISurfaceInfo depthSurfaceInfo)
	{
		return null;
	}
		
}

