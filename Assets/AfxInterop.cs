using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.IO.Pipes;

// TODO: Fix CreateEvent handle leakage.

namespace advancedfx
{

    /// <summary>
    /// Do not edit this class, it is likely to get updated, you will loose your changes often.
    /// 
    /// This class manages the inter-opreation between with HLAE / AfxHookSource.<br />
    ///  
    /// It is a bit more complicated, because we must not eliminate the advantage of CS:GO's queued rendering
    /// (our main thread syncs with the render thread of the game and we take info from the game's engine thread on a dedicated thread.).
    /// </summary>
    public class Interop
    {

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

        public interface ILogging
        {
            /// <remarks>Must be threadsafe!</remarks>
            void Log(object message);

            /// <remarks>Must be threadsafe!</remarks>
            void LogException(Exception exception);
        }

        public interface IImplementation : ILogging
        {
            void Render(IRenderInfo renderInfo);

            void RegisterSurface(ISurfaceInfo surfaceInfo);

            void DestroySurface(IntPtr sharedHandle);
        }

        public Interop(IImplementation implementation)
        {
            this.implementation = implementation;
        }

        public void OnEnable()
        {

            PipeConnect();
        }

        public void OnDisable()
        {

            PipeDisconnect();
        }

        public void Update()
        {

            Interlocked.Exchange(ref watchDogMs, 0);

            if (null != pipeServer && pipeServer.Connect())
            {
                try
                {
                    if (waitingForConnection)
                    {
                        // Is new connection.

                        implementation.Log("Pipe connected.");

                        //IntPtr ownProcessHandle = System.Diagnostics.Process.GetCurrentProcess ().Handle;

                        //long clientProcessId = 0;

                        //if(!GetNamedPipeClientProcessId(pipeServer.SafePipeHandle.DangerousGetHandle(), out clientProcessId))
                        //	throw new ApplicationException("Could not get process id of client.");

                        //System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById((int)clientProcessId);
                        //if(null == process)
                        //	throw new ApplicationException("Could not get client process from id.");

                        //IntPtr clientProcessHandle = process.Handle;

                        // Check if our version is supported by client:

                        implementation.Log("Writing version.");

                        pipeServer.WriteInt32(version, cancellationToken);

                        implementation.Log("Flushing version.");

                        pipeServer.Flush(cancellationToken);

                        implementation.Log("Waiting for version reply.");

                        bool versionSupported = pipeServer.ReadBoolean(cancellationToken);

                        if (!versionSupported)
                            throw new ApplicationException("Version " + version + " not supported by client.");

                        // Supply server info required by client:

                        pipeServer.WriteBoolean(Environment.Is64BitProcess, cancellationToken);

                        pipeServer.Flush(cancellationToken);

                        //

                        waitingForConnection = false;
                    }

                    IFrameInfo frameInfo = null;
                    bool frameInfoAvailable = pipeServer.ReadBoolean(cancellationToken);

                    if (frameInfoAvailable)
                    {
                        Int32 frameCount = pipeServer.ReadInt32(cancellationToken);

                        do
                        {
                            while (!frameInfoQueueEnqueued.WaitOne(1000))
                            {
                                if (!engineThread.IsAlive)
                                    throw new ApplicationException("Engine message handling thread died.");

                                lock (frameInfoQueue)
                                {
                                    if (0 < frameInfoQueue.Count)
                                        break;
                                }
                            }

                            lock (frameInfoQueue)
                            {
                                while (0 < frameInfoQueue.Count)
                                {
                                    IFrameInfo curFrameInfo = frameInfoQueue.Dequeue();

                                    Int32 cmp = frameCount - curFrameInfo.FrameCount;

                                    if (cmp > 0)
                                    {
                                        // This is an old info, skip
                                    }
                                    else if (cmp < 0)
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
                catch (Exception e)
                {
                    implementation.LogException(e);
                    PipeDisconnect();
                    PipeConnect();
                }
            }
        }

        public Int32 Version
        {
            get
            {
                return version;
            }
        }

        public string PipeName
        {
            get
            {
                return pipeName;
            }
            set
            {
                pipeName = value;
            }
        }

        //
        // Private:

        private IImplementation implementation;

        private const Int32 version = 0;
        private string pipeName = "advancedfxInterop";

        bool waitingForConnection = false;

        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;
        private int watchDogMs = 0;
        private const int watchDogCancelAfterMs = 20000;
        private Thread watchDogThread = null;

        private void WatchDog()
        {
            while (true)
            {
                int oldTicks = System.Environment.TickCount;

                Thread.Sleep(1000);

                if (Interlocked.Add(ref watchDogMs, System.Environment.TickCount - oldTicks) >= watchDogCancelAfterMs)
                    break;
            }

            cancellationTokenSource.Cancel();
        }

        private AutoResetEvent frameInfoQueueEnqueued = new AutoResetEvent(false);
        private Queue<IFrameInfo> frameInfoQueue = new Queue<IFrameInfo>();

        private void AddFrameInfo(IFrameInfo frameInfo)
        {
            lock (frameInfoQueue)
            {
                frameInfoQueue.Enqueue(frameInfo);
            }
        }

        private MyNamedPipeServer pipeServer = null;
        private EngineThread engineThread = null;

        private void PipeConnect()
        {

            PipeDisconnect();

            try
            {
                cancellationTokenSource = new CancellationTokenSource();
                cancellationToken = cancellationTokenSource.Token;

                Interlocked.Exchange(ref watchDogMs, 0);
                watchDogThread = new Thread(WatchDog);
                watchDogThread.Start();

                engineThread = new EngineThread(this);
                engineThread.Init();

                pipeServer = new MyNamedPipeServer(pipeName, this.implementation);

                waitingForConnection = true;

                implementation.Log("Waiting for connection.");
            }
            catch (Exception e)
            {
                implementation.LogException(e);
                PipeDisconnect();
            }
        }

        private void PipeDisconnect()
        {
            Interlocked.Exchange(ref watchDogMs, watchDogCancelAfterMs);

            try
            {
                if (null != watchDogThread && watchDogThread.IsAlive)
                {
                    watchDogThread.Join();
                }
            }
            finally
            {
                watchDogThread = null;
            }

            try
            {
                if (null != engineThread)
                {
                    engineThread.Dispose();
                }
            }
            finally
            {
                engineThread = null;
            }

            try
            {
                if (null != pipeServer)
                {
                    pipeServer.Dispose();
                }
            }
            catch (Exception e)
            {
                implementation.LogException(e);
            }
            finally
            {
                pipeServer = null;
            }

            waitingForConnection = false;

            implementation.Log("Pipes closed.");
        }

        private class EngineThread : IDisposable
        {

            public EngineThread(Interop interOp)
            {
                this.interOp = interOp;
                this.cancellationToken = interOp.cancellationToken;
            }

            public void Init()
            {
                pipeServer = new MyNamedPipeServer(interOp.pipeName + "_engine", interOp.implementation);

                thread = new Thread(ThreadWorker);
                thread.Start();
            }

            public bool IsAlive
            {
                get
                {
                    return null != thread ? thread.IsAlive : false;
                }
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

                try
                {
                    if (null != thread && thread.IsAlive)
                    {
                        thread.Join();
                    }
                }
                catch (Exception e)
                {
                    interOp.implementation.LogException(e);
                }
                finally
                {
                    thread = null;
                }

                disposed = true;
            }

            private enum ClientMessage : int
            {
                LevelInitPreEntity = 1,
                LevelShutDown = 2,
                BeforeFrameStart = 3,
                BeforeFrameRenderStart = 4,
                AfterFrameRenderEnd = 5,
                EntityCreated = 6,
                EntityDeleted = 7
            };

            private enum BeforFrameRenderStartServerMessage : int
            {
                EOT = 0,
                RequestEntityList = 1
            };

            private CancellationToken cancellationToken;
            private Interop interOp;
            private MyNamedPipeServer pipeServer = null;
            private Thread thread = null;

            private Queue<String> commandQueue = new Queue<String>();

            private class FrameInfo : IFrameInfo
            {
                public Int32 FrameCount
                {
                    get
                    {
                        return frameCount;
                    }
                    set
                    {
                        frameCount = value;
                    }
                }
                public Single AbsoluteFrameTime
                {
                    get
                    {
                        return absoluteFrameTime;
                    }
                    set
                    {
                        absoluteFrameTime = value;
                    }
                }
                public Single CurTime
                {
                    get
                    {
                        return curTime;
                    }
                    set
                    {
                        curTime = value;
                    }
                }

                Int32 frameCount;
                Single absoluteFrameTime;
                Single curTime;
            }

            private void ThreadWorker()
            {
                bool messageSent = false;

                try
                {
                    while (!cancellationToken.IsCancellationRequested && !pipeServer.Connect())
                    {
                        Thread.Sleep(10);
                    }

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        ClientMessage clientMessage = (ClientMessage)pipeServer.ReadInt32(cancellationToken);

                        switch (clientMessage)
                        {
                            case ClientMessage.BeforeFrameStart:
                                {
                                    lock (commandQueue)
                                    {
                                        if (!messageSent)
                                        {
                                            messageSent = true;
                                            commandQueue.Enqueue("echo ---- Hello World from afx-unity-interop! ----");
                                        }

                                        if (commandQueue.Count < 255)
                                        {
                                            pipeServer.WriteByte((Byte)commandQueue.Count, cancellationToken);
                                        }
                                        else
                                        {
                                            pipeServer.WriteByte(255, cancellationToken);
                                            pipeServer.WriteInt32(commandQueue.Count, cancellationToken);
                                        }

                                        while (0 < commandQueue.Count)
                                        {
                                            pipeServer.WriteStringUTF8(commandQueue.Dequeue(), cancellationToken);
                                        }
                                    }

                                    pipeServer.Flush(cancellationToken);
                                }
                                break;
                            case ClientMessage.BeforeFrameRenderStart:
                                {
                                    FrameInfo frameInfo = new FrameInfo();

                                    frameInfo.FrameCount = pipeServer.ReadInt32(cancellationToken);
                                    frameInfo.AbsoluteFrameTime = pipeServer.ReadFloat(cancellationToken);
                                    frameInfo.CurTime = pipeServer.ReadFloat(cancellationToken);
                                }
                                break;
                            case ClientMessage.EntityCreated:
                                {
                                    Int32 entityHandle = pipeServer.ReadInt32(cancellationToken);
                                }
                                break;
                            case ClientMessage.EntityDeleted:
                                {
                                    Int32 entityHandle = pipeServer.ReadInt32(cancellationToken);
                                }
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    interOp.implementation.LogException(e);
                }
                finally
                {
                    pipeServer.Dispose();
                }
            }
        }

        //[DllImport("kernel32.dll", SetLastError = true)]
        //static extern bool GetNamedPipeClientProcessId(IntPtr Pipe, out long ClientProcessId);

        //[Flags]
        //public enum DuplicateOptions : uint
        //{
        //	DUPLICATE_CLOSE_SOURCE = (0x00000001),// Closes the source handle. This occurs regardless of any error status returned.
        //	DUPLICATE_SAME_ACCESS = (0x00000002), //Ignores the dwDesiredAccess parameter. The duplicate handle has the same access as the source handle.
        //}

        //[DllImport("kernel32.dll", SetLastError=true)]
        //[return: MarshalAs(UnmanagedType.Bool)]
        //static extern bool DuplicateHandle(IntPtr hSourceProcessHandle,
        //	IntPtr hSourceHandle, IntPtr hTargetProcessHandle, out IntPtr lpTargetHandle,
        //	uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwOptions);

        // In case you wonder: we use two pipes instead of a duplex pipe in order to be able to read and write at same time from different processes without deadlocking.
        private class MyNamedPipeServer : IDisposable
        {
            enum State
            {
                Waiting,
                Connected
            }

            private ILogging logging;

            private IntPtr pipeHandle;

            private OVERLAPPED overlappedRead;
            private OVERLAPPED overlappedWrite;
            private GCHandle gcOverlappedRead;
            private GCHandle gcOverlappedWrite;

            private State state = State.Waiting;

            private IntPtr readBuffer;
            private IntPtr writeBuffer;

            private const int readBufferSize = 512;
            private const int writeBufferSize = 512;

            public MyNamedPipeServer(string pipeName, ILogging logging)
            {
                this.logging = logging;

                readBuffer = Marshal.AllocHGlobal(readBufferSize);
                writeBuffer = Marshal.AllocHGlobal(writeBufferSize);

                overlappedRead = new OVERLAPPED();
                gcOverlappedRead = GCHandle.Alloc(overlappedRead, GCHandleType.Pinned);
                overlappedRead.hEvent = CreateEvent(IntPtr.Zero, true, true, null);

                overlappedWrite = new OVERLAPPED();
                gcOverlappedWrite = GCHandle.Alloc(overlappedWrite, GCHandleType.Pinned);
                overlappedWrite.hEvent = CreateEvent(IntPtr.Zero, true, true, null);

                pipeHandle = CreateNamedPipe(
                    "\\\\.\\pipe\\" + pipeName,
                    (uint)(PipeOpenModeFlags.PIPE_ACCESS_INBOUND | PipeOpenModeFlags.PIPE_ACCESS_OUTBOUND | PipeOpenModeFlags.FILE_FLAG_OVERLAPPED),
                    (uint)(PipeModeFlags.PIPE_READMODE_BYTE | PipeModeFlags.PIPE_TYPE_BYTE | PipeModeFlags.PIPE_WAIT | PipeModeFlags.PIPE_REJECT_REMOTE_CLIENTS),
                    1,
                    (uint)writeBufferSize,
                    (uint)readBufferSize,
                    5000,
                    IntPtr.Zero);

                if (INVALID_HANDLE_VALUE != overlappedRead.hEvent
                    && INVALID_HANDLE_VALUE != pipeHandle
                    && false == ConnectNamedPipe(pipeHandle, ref overlappedRead))
                {
                    switch ((uint)Marshal.GetLastWin32Error())
                    {
                        case ERROR_IO_PENDING:
                            state = State.Waiting;
                            break;
                        case ERROR_PIPE_CONNECTED:
                            state = State.Connected;
                            SetEvent(overlappedRead.hEvent);
                            break;
                        default:
                            Dispose();
                            throw new System.ApplicationException("MyNamedPipeServer: Error: " + Marshal.GetLastWin32Error());
                    }
                }
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

                try
                {
                    if (INVALID_HANDLE_VALUE != pipeHandle) CloseHandle(pipeHandle);
                    if (INVALID_HANDLE_VALUE != overlappedWrite.hEvent) CloseHandle(overlappedWrite.hEvent);
                    if (INVALID_HANDLE_VALUE != overlappedRead.hEvent) CloseHandle(overlappedRead.hEvent);
                }
                finally {
                    gcOverlappedWrite.Free();
                    gcOverlappedRead.Free();
                    Marshal.FreeHGlobal(writeBuffer);
                    Marshal.FreeHGlobal(readBuffer);
                }

                disposed = true;
            }

            public bool Connect()
            {
                if (State.Waiting == state)
                {
                    uint waitResult = WaitForSingleObject(overlappedRead.hEvent, 0);

                    if (WAIT_OBJECT_0 == waitResult)
                    {
                        uint cb;

                        if (!GetOverlappedResult(pipeHandle, ref overlappedRead, out cb, false))
                        {
                            throw new System.ApplicationException("Connect: GetOverlappedResult error: " + Marshal.GetLastWin32Error());
                        }

                        state = State.Connected;
                    }
                }

                return State.Connected == state;
            }

            public void ReadBytes(byte[] bytes, int offset, int length, CancellationToken cancellationToken)
            {
                while (true)
                {
                    uint bytesRead = 0;

                    if (!ReadFile(pipeHandle, readBuffer, (uint)Math.Min(readBufferSize, length), IntPtr.Zero, ref overlappedRead))
                    {
                        if (ERROR_IO_PENDING == (long)Marshal.GetLastWin32Error())
                        {
                            bool completed = false;

                            while (!completed)
                            {
                                uint result = WaitForSingleObject(overlappedRead.hEvent, 500);
                                switch (result)
                                {
                                    case WAIT_OBJECT_0:
                                        completed = true;
                                        break;
                                    case WAIT_TIMEOUT:
                                        if (cancellationToken.IsCancellationRequested)
                                            throw new System.ApplicationException("ReadBytes: cancelled.");
                                        break;
                                    default:
                                        throw new System.ApplicationException("ReadBytes: WaitForSingleObject error.");
                                }
                            }
                        }
                        else
                        {
                            throw new System.ApplicationException("ReadBytes: ReadFile failed.");
                        }
                    }

                    if (!GetOverlappedResult(pipeHandle, ref overlappedRead, out bytesRead, false))
                    {
                        throw new System.ApplicationException("ReadBytes: GetOverlappedResult failed: "+Marshal.GetLastWin32Error());
                    }

                    Marshal.Copy(readBuffer, bytes, offset, (int)bytesRead);

                    offset += (int)bytesRead;
                    length -= (int)bytesRead;

                    if (0 >= length)
                        break;
                }               
            }

            public byte[] ReadBytes(int length, CancellationToken cancellationToken)
            {

                byte[] result = new byte[length];

                ReadBytes(result, 0, length, cancellationToken);

                return result;
            }

            public Boolean ReadBoolean(CancellationToken cancellationToken)
            {

                return BitConverter.ToBoolean(ReadBytes(sizeof(Boolean), cancellationToken), 0);
            }

            public Int32 ReadInt32(CancellationToken cancellationToken)
            {

                return BitConverter.ToInt32(ReadBytes(sizeof(Int32), cancellationToken), 0);
            }

            public String ReadUTF8String(CancellationToken cancellationToken)
            {

                byte[] result = ReadBytes(1, cancellationToken);

                int length = result[0] < 255 ? result[0] : ReadInt32(cancellationToken);

                return System.Text.UnicodeEncoding.UTF8.GetString(ReadBytes(length, cancellationToken));
            }

            public Single ReadFloat(CancellationToken cancellationToken)
            {

                return BitConverter.ToSingle(ReadBytes(sizeof(Single), cancellationToken), 0);
            }

            public void WriteBytes(byte[] bytes, int offset, int length, CancellationToken cancellationToken)
            {
                while (true)
                {
                    uint bytesWritten = 0;
                    uint bytesToWrite = (uint)Math.Min(writeBufferSize, length);

                    Marshal.Copy(bytes, offset, writeBuffer, (int)bytesToWrite);

                    if (!WriteFile(pipeHandle, writeBuffer, bytesToWrite, IntPtr.Zero, ref overlappedWrite))
                    {
                        if (ERROR_IO_PENDING == (long)Marshal.GetLastWin32Error())
                        {
                            bool completed = false;

                            while (!completed)
                            {
                                uint result = WaitForSingleObject(overlappedWrite.hEvent, 500);
                                switch (result)
                                {
                                    case WAIT_OBJECT_0:
                                        completed = true;
                                        break;
                                    case WAIT_TIMEOUT:
                                        if (cancellationToken.IsCancellationRequested)
                                            throw new System.ApplicationException("WriteBytes: cancelled.");
                                        break;
                                    default:
                                        throw new System.ApplicationException("WriteBytes: WaitForSingleObject error.");
                                }
                            }
                        }
                        else
                        {
                            throw new System.ApplicationException("WriteBytes: WriteFile failed.");
                        }
                    }

                    if (!GetOverlappedResult(pipeHandle, ref overlappedWrite, out bytesWritten, false))
                    {
                        throw new System.ApplicationException("WriteBytes: GetOverlappedResult failed.");
                    }

                    offset += (int)bytesWritten;
                    length -= (int)bytesWritten;

                    if (0 >= length)
                        break;
                }
            }

            public void WriteBytes(byte[] bytes, CancellationToken cancellationToken)
            {

                WriteBytes(bytes, 0, bytes.Length, cancellationToken);
            }

            public void Flush(CancellationToken cancellationToken)
            {
                if(!FlushFileBuffers(pipeHandle))
                    throw new System.ApplicationException("FlushFileBuffers failed.");
            }

            public void WriteBoolean(Boolean value, CancellationToken cancellationToken)
            {

                WriteBytes(BitConverter.GetBytes(value), cancellationToken);
            }

            public void WriteByte(Byte value, CancellationToken cancellationToken)
            {
                WriteBytes(new Byte[1] { value }, cancellationToken);
            }

            public void WriteInt32(Int32 value, CancellationToken cancellationToken)
            {

                WriteBytes(BitConverter.GetBytes(value), cancellationToken);
            }

            public void WriteHandle(IntPtr value, CancellationToken cancellationToken)
            {

                WriteInt32(value.ToInt32(), cancellationToken);
            }

            public void WriteStringUTF8(String value, CancellationToken cancellationToken)
            {

                byte[] bytes = System.Text.UnicodeEncoding.UTF8.GetBytes(value);

                if (bytes.Length < 255)
                {
                    WriteByte((byte)bytes.Length, cancellationToken);
                }
                else
                {
                    WriteByte(255, cancellationToken);
                    WriteInt32(bytes.Length, cancellationToken);
                }

                WriteBytes(bytes, cancellationToken);
            }

            [Flags]
            public enum PipeOpenModeFlags : uint
            {
                PIPE_ACCESS_DUPLEX = 0x00000003,
                PIPE_ACCESS_INBOUND = 0x00000001,
                PIPE_ACCESS_OUTBOUND = 0x00000002,
                FILE_FLAG_FIRST_PIPE_INSTANCE = 0x00080000,
                FILE_FLAG_WRITE_THROUGH = 0x80000000,
                FILE_FLAG_OVERLAPPED = 0x40000000,
                WRITE_DAC = 0x00040000,
                WRITE_OWNER = 0x00080000,
                ACCESS_SYSTEM_SECURITY = 0x01000000
            }

            [Flags]
            public enum PipeModeFlags : uint
            {
                //One of the following type modes can be specified. The same type mode must be specified for each instance of the pipe.
                PIPE_TYPE_BYTE = 0x00000000,
                PIPE_TYPE_MESSAGE = 0x00000004,
                //One of the following read modes can be specified. Different instances of the same pipe can specify different read modes
                PIPE_READMODE_BYTE = 0x00000000,
                PIPE_READMODE_MESSAGE = 0x00000002,
                //One of the following wait modes can be specified. Different instances of the same pipe can specify different wait modes.
                PIPE_WAIT = 0x00000000,
                PIPE_NOWAIT = 0x00000001,
                //One of the following remote-client modes can be specified. Different instances of the same pipe can specify different remote-client modes.
                PIPE_ACCEPT_REMOTE_CLIENTS = 0x00000000,
                PIPE_REJECT_REMOTE_CLIENTS = 0x00000008
            }

            [StructLayout(LayoutKind.Sequential)] // Be aware of 32 bit vs 64 bit here, LayoutKind.Explicit is tricky.
            public struct OVERLAPPED
            {
                public IntPtr Internal;
                public IntPtr InternalHigh;
                //public uint Offset;
                //public uint OffsetHigh;
                public IntPtr Pointer;
                public IntPtr hEvent;
            }

            const UInt32 ERROR_PIPE_CONNECTED = 535;
            const UInt32 ERROR_IO_PENDING = 997;

            const UInt32 INFINITE = 0xFFFFFFFF;
            const UInt32 WAIT_ABANDONED = 0x00000080;
            const UInt32 WAIT_OBJECT_0 = 0x00000000;
            const UInt32 WAIT_TIMEOUT = 0x00000102;

            private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr CreateNamedPipe(string lpName, uint dwOpenMode,
                uint dwPipeMode, uint nMaxInstances, uint nOutBufferSize, uint nInBufferSize,
                uint nDefaultTimeOut, IntPtr lpSecurityAttributes);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool ConnectNamedPipe(IntPtr hNamedPipe,
                [In] ref OVERLAPPED lpOverlapped);

            [DllImport("kernel32.dll", SetLastError = true)]
            //[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            //[SuppressUnmanagedCodeSecurity]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CloseHandle(IntPtr hObject);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern bool WriteFile(IntPtr hFile, IntPtr lpBuffer,
                uint nNumberOfBytesToWrite, IntPtr lpNumberOfBytesWritten,
                 ref OVERLAPPED lpOverlapped);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern bool ReadFile(IntPtr hFile, IntPtr lpBuffer,
                uint nNumberOfBytesToRead, IntPtr lpNumberOfBytesRead, ref OVERLAPPED lpOverlapped);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool FlushFileBuffers(IntPtr hFile);

            [DllImport("kernel32.dll")]
            static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

            [DllImport("kernel32.dll")]
            static extern bool SetEvent(IntPtr hEvent);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern UInt32 WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern bool GetOverlappedResult(IntPtr hFile,
                [In] ref OVERLAPPED lpOverlapped,
                out uint lpNumberOfBytesTransferred, bool bWait);
        }
    }

}
