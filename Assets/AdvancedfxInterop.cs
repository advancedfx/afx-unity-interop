using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.IO.Pipes;

/// <summary>
/// Do not edit this class, it is likely to get updated, you will loose your changes often.
/// 
/// This class manages the inter-opreation between with HLAE / AfxHookSource.<br />
///  
/// It is a bit more complicated, because we must not eliminate the advantage of CS:GO's queued rendering
/// (our main thread syncs with the render thread of the game and we take info from the game's engine thread on a dedicated thread.).
/// </summary>
internal class AdvancedfxInterop {

	//
	// Public:

	public interface IFrameInfo
	{
		Int32 FrameCount { get; }
		Single AbsoluteFrameTime { get; }
		Single CurTime { get; }
	}

	public enum RenderType : int
	{
		Unknown = 0,
		Sky = 1,
		Normal = 2,
		Shadow = 3
	}

	public struct Afx4x4
	{
		Single M00;
		Single M01;
		Single M02;
		Single M03;
		Single M10;
		Single M11;
		Single M12;
		Single M13;
		Single M20;
		Single M21;
		Single M22;
		Single M23;
		Single M30;
		Single M31;
		Single M32;
		Single M33;
	}


	public interface ISurfaceInfo
	{
		UInt32 D3D9Width { get; }
		UInt32 D3D9Height { get; }
		UInt32 D3D9Format { get; }
		UInt32 D3D9MultiSample { get; }
		UInt32 D3D9MultiSampleQuality { get; }
		Boolean D3D9Lockable { get; }
		IntPtr SharedHandle { get; }
	}

	public interface IRenderInfo
	{
		RenderType Type { get; }

		IntPtr SurfaceSharedHandle { get; }
		IntPtr DepthSurfaceSharedHandle { get; }

		Afx4x4 WorldToScreenMatrix { get; }

		/// <remarks>Can be null if not available, so handle this! Assume this to happen especially at start-up!</remarks>
		IFrameInfo FrameInfo { get; }
	}

	public interface IImplentation
	{
		void Log(object message);

		void LogException(Exception exception);

		void Render (IRenderInfo renderInfo);

		void RegisterSurface (ISurfaceInfo surfaceInfo);

		void DestroySurface (IntPtr sharedHandle);
	}

	public AdvancedfxInterop(IImplentation implementation)
	{
		this.implementation = implementation;
	}

	public void OnEnable() {
		
		pipeServer = new NamedPipeServerStream (pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

		PipeConnect ();
	}

	public void OnDisable() {

		PipeDisconnect ();

		pipeServer.Dispose ();
	}

	public void Update() {

		if (IsPipeConnected) {
			try {
				if(waitingForConnection)
				{
					// Is new connection.

					implementation.Log("Pipe connected.");

					// Check if our version is supported by client:

					WriteInt32(pipeServer, version);

					Flush(pipeServer);

					bool versionSupported = ReadBoolean(pipeServer);

					if(!versionSupported) throw new ApplicationException ("Version "+version+" not supported by client.");

					// Supply server info required by client:

					WriteBoolean(pipeServer, Environment.Is64BitProcess);
					WriteSafeHandle(pipeServer, engineThread.WritePipeHandle);
					WriteSafeHandle(pipeServer, engineThread.ReadPipeHandle);

					Flush(pipeServer);

					// Get textures data

					int texturesCount = ReadInt32(pipeServer);

					//

					waitingForConnection = false;
				}

				IFrameInfo frameInfo = null;
				bool frameInfoAvailable = ReadBoolean(pipeServer);

				if(frameInfoAvailable)
				{
					Int32 frameCount = ReadInt32(pipeServer);

					do
					{
						while(!frameInfoQueueEnqueued.WaitOne( 1000 ))
						{
							if(!engineThread.IsAlive) throw new ApplicationException("Engine message handling thread died.");

							lock(frameInfoQueue)
							{
								if(0 < frameInfoQueue.Count)
									break;
							}
						}
		
						lock(frameInfoQueue)
						{
							while(0 < frameInfoQueue.Count)
							{
								IFrameInfo curFrameInfo = frameInfoQueue.Dequeue();

								Int32 cmp = frameCount - curFrameInfo.FrameCount;

								if(cmp > 0)
								{
									// This is an old info, skip
								}
								else if(cmp < 0)
								{
									// Too far ahead, missing info, abort
									break;
								}
								else
								{
									// Exactly right info, let's go!
									frameInfo = curFrameInfo;
									break;
								}
							}
						}
					} while (null == frameInfo);
				}

			}
			catch(Exception e) {
				implementation.LogException (e);
				PipeDisconnect ();
			}
		} else if (!waitingForConnection) {
			PipeConnect ();
		}
	}

	public Int32 Version {
		get {
			return version;
		}
	}

	public string PipeName {
		get {
			return pipeName;
		}
		set {
			pipeName = value;
		}
	}

	//
	// Private:

	private IImplentation implementation;

	private const Int32 version = 0;
	private string pipeName = "advancedfxInterop";

	private AutoResetEvent frameInfoQueueEnqueued = new AutoResetEvent (false);
	private Queue<IFrameInfo> frameInfoQueue = new Queue<IFrameInfo>();

	private void AddFrameInfo(IFrameInfo frameInfo)
	{
		lock (frameInfoQueue) {
			frameInfoQueue.Enqueue (frameInfo);
		}		
	}

	private int theradedPipeConnected = 0;
	private bool waitingForConnection = false;
	private NamedPipeServerStream pipeServer = null;
	private EngineThread engineThread = null;

	private void PipeConnect() {
		
		PipeDisconnect ();

		try{
			waitingForConnection = true;

			engineThread = new EngineThread(this);
			engineThread.Init();

			pipeServer.BeginWaitForConnection (new AsyncCallback(WaitForConnectionCallback), this);
		}
		catch(Exception e) {
			implementation.LogException (e);
			PipeDisconnect ();
		}
	}

	private void PipeDisconnect() {

		try {
			if(null != engineThread)
			{
				engineThread.Dispose();
			}
		}
		finally {
			engineThread = null;
		}
			
		try {
			if(null != pipeServer) pipeServer.Close();
		}
		catch(Exception e) {
			implementation.LogException (e);
		}
		finally {
			pipeServer = null;
		}

		waitingForConnection = false;
		IsPipeConnected = false;
	}

	internal static void WaitForConnectionCallback(IAsyncResult ar) {

		AdvancedfxInterop advancedfxInterop = (AdvancedfxInterop)ar.AsyncState;

		advancedfxInterop.pipeServer.EndWaitForConnection (ar);
		advancedfxInterop.IsPipeConnected = true;
	}

	private bool IsPipeConnected{
		
		get {
			return 1 == Interlocked.CompareExchange (ref theradedPipeConnected, 1, 1);
		}
		set {
			if (value)
				Interlocked.CompareExchange (ref theradedPipeConnected, 1, 0);
			else
				Interlocked.CompareExchange (ref theradedPipeConnected, 0, 1);
		}
	}

	static void ReadBytes(Stream stream, byte[] bytes, int offset, int length) {

		do {
			int bytesRead = stream.Read (bytes, offset, length);

			offset += bytesRead;
			length -= bytesRead;

		} while(0 < length);
	}

	static byte[] ReadBytes(Stream stream, int length) {

		byte[] result = new byte[length];

		ReadBytes(stream, result, 0, length);

		return result;
	}

	static Boolean ReadBoolean(Stream stream) {

		return BitConverter.ToBoolean (ReadBytes (stream, sizeof(Boolean)), 0);
	}

	static Int32 ReadInt32(Stream stream) {

		return BitConverter.ToInt32 (ReadBytes (stream, sizeof(Int32)), 0);
	}

	static String ReadUTF8String(Stream stream) {

		byte[] result = ReadBytes (stream, 1);

		int length = result [0] < 255 ? result [0] : ReadInt32 (stream);

		return System.Text.UnicodeEncoding.UTF8.GetString(ReadBytes(stream, length));
	}

	static Single ReadFloat(Stream stream) {

		return BitConverter.ToSingle (ReadBytes (stream, sizeof(Single)), 0);
	}

	static void WriteBytes(Stream stream, byte[] bytes, int offset, int length)
	{
		stream.Write (bytes, offset, length);
	}

	static void WriteBytes(Stream stream, byte[] bytes) {
		
		WriteBytes (stream, bytes, 0, bytes.Length);
	}

	static void Flush(Stream stream) {

		stream.Flush ();
	}

	static void WriteBoolean(Stream stream, Boolean value) {

		WriteBytes (stream, BitConverter.GetBytes (value));
	}

	static void WriteByte(Stream stream, Byte value) {

		stream.WriteByte (value);
	}

	static void WriteInt32(Stream stream, Int32 value) {

		WriteBytes (stream, BitConverter.GetBytes (value));
	}

	static void WriteSafeHandle(Stream stream, System.Runtime.InteropServices.SafeHandle value) {

		WriteInt32 (stream, value.DangerousGetHandle ().ToInt32 ());		
	}

	static void WriteStringUTF8(Stream stream, String value) {

		byte[] bytes = System.Text.UnicodeEncoding.UTF8.GetBytes (value);

		if (bytes.Length < 255) {
			WriteByte (stream, (byte)bytes.Length);
		} else {
			WriteByte (stream, 255);
			WriteInt32 (stream, bytes.Length);
		}

		WriteBytes (stream, bytes);
	}

	private class EngineThread : IDisposable {

		public EngineThread(AdvancedfxInterop interOp)
		{
			this.interOp = interOp;
		}

		public void Init()
		{
			engineWritePipeServer = new AnonymousPipeServerStream(PipeDirection.Out);

			engineReadPipeServer = new AnonymousPipeServerStream(PipeDirection.In);

			thread = new Thread(ThreadWorker);
		}

		public bool IsAlive {
			get {
				return null != thread ? thread.IsAlive : false;
			}
		}

		public void Dispose() {
			try {
				if(null != thread) {
					thread.Abort();
					thread.Join();
				}
			}
			catch(Exception e) {
				interOp.implementation.LogException (e);
			}
			finally {
				thread = null;
			}

			try {
				if(null != engineReadPipeServer) engineReadPipeServer.Close();
			}
			catch(Exception e) {
				interOp.implementation.LogException (e);
			}
			finally {
				engineReadPipeServer = null;
			}

			try {
				if(null != engineWritePipeServer) engineWritePipeServer.Close();
			}
			catch(Exception e) {
				interOp.implementation.LogException (e);
			}
			finally {
				engineWritePipeServer = null;
			}
		}

		public System.Runtime.InteropServices.SafeHandle ReadPipeHandle {
			get {
				return engineReadPipeServer.SafePipeHandle;
			}
		}

		public System.Runtime.InteropServices.SafeHandle WritePipeHandle {
			get {
				return engineWritePipeServer.SafePipeHandle;
			}
		}

		private enum ClientMessage:int {
			LevelInitPreEntity = 1,
			LevelShutDown = 2,
			BeforeFrameStart = 3,
			BeforeFrameRenderStart = 4,
			AfterFrameRenderEnd = 5,
			EntityCreated = 6,
			EntityDeleted = 7
		};

		private enum BeforFrameRenderStartServerMessage:int {
			EOT = 0,
			RequestEntityList = 1
		};

		private AdvancedfxInterop interOp;
		private AnonymousPipeServerStream engineWritePipeServer = null;
		private AnonymousPipeServerStream engineReadPipeServer = null;
		private Thread thread = null;
		private bool initalized = false;

		private Queue<String> commandQueue = new Queue<String>();

		private class FrameInfo : IFrameInfo {
			public Int32 FrameCount {
				get {
					return frameCount;
				}
				set {
					frameCount = value;
				}
			}
			public Single AbsoluteFrameTime {
				get {
					return absoluteFrameTime;
				}
				set {
					absoluteFrameTime = value;
				}
			}
			public Single CurTime {
				get {
					return curTime;
				}
				set {
					curTime = value;
				}
			}

			Int32 frameCount;
			Single absoluteFrameTime;
			Single curTime;
		}

		private void ThreadWorker() {

			ClientMessage clientMessage = (ClientMessage)ReadInt32 (engineReadPipeServer);

			switch (clientMessage) {
			case ClientMessage.BeforeFrameStart:
				{
					lock (commandQueue) {

						if (commandQueue.Count < 255)
							WriteByte (engineWritePipeServer, (Byte)commandQueue.Count);
						else {
							WriteByte (engineWritePipeServer, 255);
							WriteInt32 (engineWritePipeServer, commandQueue.Count);
						}

						while (0 < commandQueue.Count) {
							WriteStringUTF8(engineWritePipeServer, commandQueue.Dequeue());
						}
					}

					Flush (engineWritePipeServer);
				}
				break;
			case ClientMessage.BeforeFrameRenderStart:
				{
					FrameInfo frameInfo = new FrameInfo();

					frameInfo.FrameCount = ReadInt32 (engineReadPipeServer);
					frameInfo.AbsoluteFrameTime = ReadFloat (engineReadPipeServer);
					frameInfo.CurTime = ReadFloat (engineReadPipeServer);

					if (!initalized) {

						initalized = true;
					}
				}
				break;
			case ClientMessage.EntityCreated:
				{
					Int32 entityHandle = ReadInt32 (engineReadPipeServer);
				}
				break;
			case ClientMessage.EntityDeleted:
				{
					Int32 entityHandle = ReadInt32 (engineWritePipeServer);
				}
				break;
			}
		}
	}
}
