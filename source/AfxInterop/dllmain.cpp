#include "stdafx.h"

#include <windows.h>

#include <d3d11.h>

#include <tchar.h>

#include <map>
#include <set>
#include <queue>
#include <vector>

#include <atomic>
#include <mutex>
#include <condition_variable>

class CNamedPipeServer
{
public:
	enum State
	{
		State_Error,
		State_Waiting,
		State_Connected
	};

private:
	HANDLE m_PipeHandle;

	OVERLAPPED m_OverlappedRead = {};
	OVERLAPPED m_OverlappedWrite = {};

	State m_State = State_Error;

	const DWORD m_ReadBufferSize = 512;
	const DWORD m_WriteBufferSize = 512;

	const DWORD m_ReadTimeoutMs = 5000;
	const DWORD m_WriteTimeoutMs = 5000;

public:
	CNamedPipeServer(const char * pipeName)
	{
		m_OverlappedRead.hEvent = CreateEventA(NULL, true, true, NULL);
		m_OverlappedWrite.hEvent = CreateEventA(NULL, true, true, NULL);

		std::string strPipeName("\\\\.\\pipe\\");
		strPipeName.append(pipeName);

		m_PipeHandle = CreateNamedPipeA(
			strPipeName.c_str(),
			PIPE_ACCESS_INBOUND | PIPE_ACCESS_OUTBOUND | FILE_FLAG_OVERLAPPED,
			PIPE_READMODE_BYTE | PIPE_TYPE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
			1,
			m_ReadBufferSize,
			m_WriteBufferSize,
			5000,
			NULL);

		if (INVALID_HANDLE_VALUE != m_OverlappedRead.hEvent
			&& INVALID_HANDLE_VALUE != m_OverlappedWrite.hEvent
			&& INVALID_HANDLE_VALUE != m_PipeHandle
			&& FALSE == ConnectNamedPipe(m_PipeHandle, &m_OverlappedRead))
		{
			switch (GetLastError())
			{
			case ERROR_IO_PENDING:
				m_State = State_Waiting;
				break;
			case ERROR_PIPE_CONNECTED:
				m_State = State_Connected;
				SetEvent(m_OverlappedRead.hEvent);
				break;
			}
		}
	}

	~CNamedPipeServer()
	{
		if (INVALID_HANDLE_VALUE != m_PipeHandle) CloseHandle(m_PipeHandle);
		if (INVALID_HANDLE_VALUE != m_OverlappedWrite.hEvent) CloseHandle(m_OverlappedWrite.hEvent);
		if (INVALID_HANDLE_VALUE != m_OverlappedRead.hEvent) CloseHandle(m_OverlappedRead.hEvent);
	}

	State Connect()
	{
		if (State_Waiting == m_State)
		{
			DWORD waitResult = WaitForSingleObject(m_OverlappedRead.hEvent, 0);

			if (WAIT_OBJECT_0 == waitResult)
			{
				DWORD cb;

				if (!GetOverlappedResult(m_PipeHandle, &m_OverlappedRead, &cb, FALSE))
					m_State = State_Error;
				else
					m_State = State_Connected;
			}
		}

		return m_State;
	}

	bool ReadBytes(LPVOID bytes, DWORD offset, DWORD length)
	{
		while (true)
		{
			DWORD bytesRead = 0;

			if (!ReadFile(m_PipeHandle, (LPVOID)&(((char *)bytes)[offset]), length, NULL, &m_OverlappedRead))
			{
				if (ERROR_IO_PENDING == GetLastError())
				{
					bool completed = false;

					while (!completed)
					{
						DWORD result = WaitForSingleObject(m_OverlappedRead.hEvent, m_ReadTimeoutMs);
						switch (result)
						{
						case WAIT_OBJECT_0:
							completed = true;
							break;
						case WAIT_TIMEOUT:
							return false;
						default:
							return false;
						}
					}
				}
				else
				{
					return false;
				}
			}

			if (!GetOverlappedResult(m_PipeHandle, &m_OverlappedRead, &bytesRead, FALSE))
			{
				return false;
			}

			offset += bytesRead;
			length -= bytesRead;

			if (0 == length)
				break;
		}

		return true;
	}

	bool ReadBoolean(bool & outValue)
	{
		BYTE tmp;

		if (ReadBytes(&tmp, 0, sizeof(tmp)))
		{
			outValue = 0 != tmp;
			return true;
		}

		return false;
	}

	bool ReadByte(BYTE & outValue)
	{
		return ReadBytes(&outValue, 0, (DWORD)sizeof(outValue));
	}

	bool ReadSByte(signed char & outValue)
	{
		return ReadBytes(&outValue, 0, (DWORD)sizeof(outValue));
	}

	bool ReadUInt32(UINT32 & outValue)
	{
		return ReadBytes(&outValue, 0, (DWORD)sizeof(outValue));
	}

	bool ReadCompressedUInt32(UINT32 & outValue)
	{
		BYTE value;
		
		if (!ReadByte(value))
			return false;

		if (value < 255)
		{
			outValue = value;
			return true;
		}

		return ReadUInt32(outValue);
	}

	bool ReadInt32(INT32 & outValue)
	{
		return ReadBytes(&outValue, 0, (DWORD)sizeof(outValue));
	}

	bool ReadCompressedInt32(INT32 & outValue)
	{
		signed char value;
		
		if (!ReadSByte(value))
			return false;

		if (value < 127)
		{
			outValue = value;
			return value;
		}

		return ReadInt32(outValue);
	}

	bool ReadStringUTF8(std::string & outValue)
	{
		UINT32 length;

		if (!ReadCompressedUInt32(length)) return false;

		outValue.resize(length);

		if (!ReadBytes(&outValue[0], 0, length)) return false;

		return true;
	}

	bool ReadHandle(HANDLE & outValue)
	{
		DWORD value32;

		if (ReadBytes(&value32, 0, (DWORD)sizeof(value32)))
		{
			outValue = ULongToHandle(value32);
			return true;
		}

		return false;
	}
	
	bool ReadSingle(float & outValue)
	{
		return ReadBytes(&outValue, 0, (DWORD)sizeof(outValue));
	}


	bool WriteBytes(const LPVOID bytes, DWORD offset, DWORD length)
	{
		while (true)
		{
			DWORD bytesWritten = 0;
			DWORD bytesToWrite = length;

			if (!WriteFile(m_PipeHandle, (LPVOID)&(((char *)bytes)[offset]), bytesToWrite, NULL, &m_OverlappedWrite))
			{
				if (ERROR_IO_PENDING == GetLastError())
				{
					bool completed = false;

					while (!completed)
					{
						DWORD result = WaitForSingleObject(m_OverlappedWrite.hEvent, m_WriteTimeoutMs);
						switch (result)
						{
						case WAIT_OBJECT_0:
							completed = true;
							break;
						case WAIT_TIMEOUT:
							return false;
						default:
							return false;
						}
					}
				}
				else
				{
					return false;
				}
			}

			if (!GetOverlappedResult(m_PipeHandle, &m_OverlappedWrite, &bytesWritten, FALSE))
			{
				return false;
			}

			offset += bytesWritten;
			length -= bytesWritten;

			if (0 == length)
				break;
		}

		return true;
	}

	bool Flush()
	{
		if (!FlushFileBuffers(m_PipeHandle))
			return false;

		return true;
	}

	bool WriteBoolean(bool value)
	{
		BYTE tmp = value ? 1 : 0;

		return WriteBytes(&tmp, 0, sizeof(tmp));
	}

	bool WriteByte(BYTE value)
	{
		return WriteBytes(&value, 0, sizeof(value));
	}

	bool WriteSByte(signed char value)
	{
		return WriteBytes(&value, 0, sizeof(value));
	}

	bool WriteUInt32(UINT32 value)
	{
		return WriteBytes(&value, 0, sizeof(value));
	}

	bool WriteCompressedUInt32(UINT32 value)
	{
		if (0 <= value && value <= 255 - 1)
		{
			return WriteByte((BYTE)value);
		}
		else
		{
			return WriteByte(255)
				&& WriteUInt32(value);
		}
	}

	bool WriteInt32(INT32 value)
	{
		return WriteBytes(&value, 0, sizeof(value));
	}

	bool WriteCompressedInt32(INT32 value)
	{
		if (-128 <= value && value <= 127 - 1)
		{
			WriteSByte((signed char)value);
		}
		else
		{
			return WriteSByte(127)
				&& WriteInt32(value);
		}
	}

	bool WriteHandle(HANDLE value)
	{
		DWORD value32 = HandleToULong(value);

		return WriteBytes(&value32, 0, sizeof(value32));
	}

	bool WriteSingle(FLOAT value)
	{
		return WriteBytes(&value, 0, sizeof(value));
	}

	bool WriteStringUTF8(const std::string value)
	{
		UINT32 length = (UINT32)value.length();

		return WriteCompressedUInt32(length)
			&& WriteBytes((LPVOID)value.c_str(), 0, length);
	}
};

typedef struct CAfxInteropMatrix4x4 {
	FLOAT M00;
	FLOAT M01;
	FLOAT M02;
	FLOAT M03;
	FLOAT M10;
	FLOAT M11;
	FLOAT M12;
	FLOAT M13;
	FLOAT M20;
	FLOAT M21;
	FLOAT M22;
	FLOAT M23;
	FLOAT M30;
	FLOAT M31;
	FLOAT M32;
	FLOAT M33;
} AfxInterop_Matrix4x4_t;

typedef enum AfxRenderPassType_e {
	AfxRenderPassType_BeforeTranslucentShadow = 2,
	AfxRenderPassType_AfterTranslucentShadow = 3,
	AfxRenderPassType_BeforeTranslucent = 4,
	AfxRenderPassType_AfterTranslucent = 5
} AfxRenderPassType_t;

typedef struct CAfxInteropView {
	INT32 X;
	INT32 Y;
	INT32 Width;
	INT32 Height;
	CAfxInteropMatrix4x4 ViewMatrix;
	CAfxInteropMatrix4x4 ProjectionMatrix;
} AfxInterop_View_t;

typedef struct CAfxInteropRenderInfo {
	AfxInterop_View_t View;
	INT32 FrameCount;
	FLOAT AbsoluteFrameTime;
	FLOAT CurTime;
	FLOAT FrameTime;
} AfxInterop_RenderInfo_t;

class CAfxCommand
{
public:
	UINT32 GetArgs() const
	{
		return (UINT32)m_Args.size();
	}

	const char * GetArg(UINT32 index) const
	{
		if (index >= m_Args.size()) return "";

		return m_Args[index].c_str();
	}

	void SetArgs(UINT32 numArgs)
	{
		m_Args.resize(numArgs);
	}

	std::string & GetArgString(UINT32 index)
	{
		return m_Args[index];
	}

private:
	std::vector<std::string> m_Args;
};

class CAfxCommands
{
public:
	UINT32 GetCommands() const
	{
		return (UINT32)m_Commands.size();
	}

	UINT32 GetArgs(UINT32 index) const
	{
		if (index >= m_Commands.size()) return 0;

		return m_Commands[index].GetArgs();
	}

	const char * GetArg(UINT32 index, UINT32 argIndex) const
	{
		if (index >= m_Commands.size()) return "";

		return m_Commands[index].GetArg(argIndex);
	}

	void SetCommands(UINT32 numCommands)
	{
		m_Commands.resize(numCommands);
	}

	void SetArgs(UINT32 index, UINT32 numArgs)
	{
		m_Commands[index].SetArgs(numArgs);
	}

	std::string & GetArgString(UINT32 index, UINT32 argIndex)
	{
		return m_Commands[index].GetArgString(argIndex);
	}

private:
	std::vector<CAfxCommand> m_Commands;
};

extern "C" __declspec(dllexport) UINT32 __stdcall AfxInteropCommands_GetCommandCount(const CAfxCommands * commands)
{
	return commands->GetCommands();
}

extern "C" __declspec(dllexport) UINT32 __stdcall AfxInteropCommands_GetCommandArgCount(const CAfxCommands * commands, UINT32 index)
{
	return commands->GetArgs(index);
}

extern "C" __declspec(dllexport) const char * __stdcall AfxInteropCommands_GetCommandArg(const CAfxCommands * commands, UINT32 index, UINT32 argIndex)
{
	return commands->GetArg(index, argIndex);
}

typedef void (__stdcall * AfxInteropCommands_t)(const CAfxCommands * commands);

typedef void (__stdcall * AfxInteropRender_t)(
	const CAfxInteropRenderInfo * renderInfo, BOOL * outColorTextureWasLost, HANDLE * outSharedColorTextureHandle, BOOL * outColorDepthTextureWasLost, HANDLE * outSharedColorDepthTextureHandle,
	BOOL * outBeforeTranslucentShadow, BOOL * outAfterTranslucentShadow,
	BOOL * outBeforeTranslucent, BOOL * outAfterTranslucent,
	BOOL * outBeforeHud, BOOL * outAfterHud);

typedef void(__stdcall * AfxInteropRenderPass_t)(AfxRenderPassType_t pass, const CAfxInteropView * view);

typedef struct AfxInteropHandleCalcResult_s {
	INT32 IntHandle;
} AfxInteropHandleCalcResult_t;

struct AfxInteropVector_s {
	FLOAT X;
	FLOAT Y;
	FLOAT Z;
};

struct AfxInteropQAngle_s {
	FLOAT Pitch;
	FLOAT Yaw;
	FLOAT Roll;
};

typedef struct AfxInteropVecAngCalcResult_s {
	AfxInteropVector_s Vector;
	AfxInteropQAngle_s QAngle;
} AfxInteropVecAngCalcResult_t;

typedef struct AfxInteropCamCalcResult_s {
	AfxInteropVector_s Vector;
	AfxInteropQAngle_s QAngle;
	FLOAT Fov;
} AfxInteropCamCalcResult_t;

typedef struct AfxInteropFovCalcResult_s {
	FLOAT Fov;
} AfxInteropFovCalcResult_t;

typedef struct AfxInteropBoolCalcResult_s {
	BOOL Result;
} AfxInteropBoolCalcResult_t;

typedef struct AfxInteropIntCalcResult_s {
	INT32 Result;
} AfxInteropIntCalcResult_t;

typedef void(__stdcall * AfxInteropHandleCalcCallback_t)(AfxInteropHandleCalcResult_t * result);

typedef void(__stdcall * AfxInteropVecAngCalcCallback_t)(AfxInteropVecAngCalcResult_t * result);

typedef void(__stdcall * AfxInteropCamCalcCallback_t)(AfxInteropCamCalcResult_t * result);

typedef void(__stdcall * AfxInteropFovCalcCallback_t)(AfxInteropFovCalcResult_t * result);

typedef void(__stdcall * AfxInteropBoolCalcCallback_t)(AfxInteropBoolCalcResult_t * result);

typedef void(__stdcall * AfxInteropIntCalcCallback_t)(AfxInteropIntCalcResult_t * result);

class __declspec(novtable) IAfxInteropCalcCallbacksIterator abstract
{
public:
	virtual void Destroy() = 0;
};

template<typename T, typename R> class CAfxInteropCalcCallbacks abstract
{
public:
	IAfxInteropCalcCallbacksIterator * Add(const char * name, T callback)
	{
		return new CAfxInteropCalcCallbacksIterator(m_Map, name, callback);
	}

	bool BatchUpdateRequest(CNamedPipeServer * pipeServer)
	{
		if (!pipeServer->WriteCompressedUInt32((UINT32)m_Map.size())) return false;
		for (typename std::map<std::string, std::set<T>>::iterator it = m_Map.begin(); it != m_Map.end(); ++it)
		{
			if (!pipeServer->WriteStringUTF8(it->first)) return false;
		}

		return true;
	}

	bool BatchUpdateResult(CNamedPipeServer * pipeServer)
	{
		R result;

		for (typename std::map<std::string, std::set<T>>::iterator it = m_Map.begin(); it != m_Map.end(); ++it)
		{
			R * resultPtr = nullptr;
			bool hasResult;

			if (!pipeServer->ReadBoolean(hasResult)) return false;

			if (hasResult)
			{
				if (!ReadResult(pipeServer, result)) return false;
				resultPtr = &result;
			}

			for (typename std::set<T>::iterator setIt = (*it).second.begin(); setIt != (*it).second.end(); ++setIt)
			{
				CallResult(*setIt, resultPtr);
			}
		}

		return true;
	}

protected:
	virtual bool ReadResult(CNamedPipeServer * pipeServer, R & outResult) = 0;
	virtual void CallResult(T callback, R * result) = 0;

private:
	typename typedef std::set<T> Callbacks_t;
	typedef std::map<std::string, Callbacks_t> NameToCallbacks_t;
	NameToCallbacks_t m_Map;

	class CAfxInteropCalcCallbacksIterator : public IAfxInteropCalcCallbacksIterator
	{
	public:
		CAfxInteropCalcCallbacksIterator(NameToCallbacks_t & map, const char * name, T callback)
			: m_Map(map)
			, m_Key(name)
			, m_Callback(callback)
		{
			map[name].emplace(callback);
		}

		virtual void Destroy() {
			delete this;
		}

	protected:
		~CAfxInteropCalcCallbacksIterator() {
			typename NameToCallbacks_t::iterator mapIterator = m_Map.find(m_Key);

			if (mapIterator != m_Map.end())
			{
				typename Callbacks_t::iterator setIterator = (*mapIterator).second.find(m_Callback);
				if (setIterator != (*mapIterator).second.end())
				{
					(*mapIterator).second.erase(setIterator);
					if ((*mapIterator).second.empty())
					{
						m_Map.erase(mapIterator);
					}
				}
			}
		}

	private:
		typename NameToCallbacks_t & m_Map;
		std::string m_Key;
		T m_Callback;
	};
};

class CAfxInteropHandleCalcCallbacks : public CAfxInteropCalcCallbacks<AfxInteropHandleCalcCallback_t, AfxInteropHandleCalcResult_t>
{
protected:
	virtual bool ReadResult(CNamedPipeServer * pipeServer, AfxInteropHandleCalcResult_t & outResult) override
	{
		if (!pipeServer->ReadInt32(outResult.IntHandle)) return false;

		return true;
	}

	virtual void CallResult(AfxInteropHandleCalcCallback_t callback, AfxInteropHandleCalcResult_t * result) override
	{
		callback(result);
	}
};

class CAfxInteropVecAngCalcCallbacks : public CAfxInteropCalcCallbacks<AfxInteropVecAngCalcCallback_t, AfxInteropVecAngCalcResult_t>
{
protected:
	virtual bool ReadResult(CNamedPipeServer * pipeServer, AfxInteropVecAngCalcResult_t & outResult) override
	{
		if (!pipeServer->ReadSingle(outResult.Vector.X)) return false;
		if (!pipeServer->ReadSingle(outResult.Vector.Y)) return false;
		if (!pipeServer->ReadSingle(outResult.Vector.Z)) return false;

		if (!pipeServer->ReadSingle(outResult.QAngle.Pitch)) return false;
		if (!pipeServer->ReadSingle(outResult.QAngle.Yaw)) return false;
		if (!pipeServer->ReadSingle(outResult.QAngle.Roll)) return false;

		return true;
	}

	virtual void CallResult(AfxInteropVecAngCalcCallback_t callback, AfxInteropVecAngCalcResult_t * result) override
	{
		callback(result);
	}
};

class CAfxInteropCamCalcCallbacks : public CAfxInteropCalcCallbacks<AfxInteropCamCalcCallback_t, AfxInteropCamCalcResult_t>
{
protected:
	virtual bool ReadResult(CNamedPipeServer * pipeServer, AfxInteropCamCalcResult_t & outResult) override
	{
		if (!pipeServer->ReadSingle(outResult.Vector.X)) return false;
		if (!pipeServer->ReadSingle(outResult.Vector.Y)) return false;
		if (!pipeServer->ReadSingle(outResult.Vector.Z)) return false;

		if (!pipeServer->ReadSingle(outResult.QAngle.Pitch)) return false;
		if (!pipeServer->ReadSingle(outResult.QAngle.Yaw)) return false;
		if (!pipeServer->ReadSingle(outResult.QAngle.Roll)) return false;

		if (!pipeServer->ReadSingle(outResult.Fov)) return false;

		return true;
	}

	virtual void CallResult(AfxInteropCamCalcCallback_t callback, AfxInteropCamCalcResult_t * result) override
	{
		callback(result);
	}
};

class CAfxInteropFovCalcCallbacks : public CAfxInteropCalcCallbacks<AfxInteropFovCalcCallback_t, AfxInteropFovCalcResult_t>
{
protected:
	virtual bool ReadResult(CNamedPipeServer * pipeServer, AfxInteropFovCalcResult_t & outResult) override
	{
		if (!pipeServer->ReadSingle(outResult.Fov)) return false;

		return true;
	}

	virtual void CallResult(AfxInteropFovCalcCallback_t callback, AfxInteropFovCalcResult_t * result) override
	{
		callback(result);
	}
};

class CAfxInteropBoolCalcCallbacks : public CAfxInteropCalcCallbacks<AfxInteropBoolCalcCallback_t, AfxInteropBoolCalcResult_t>
{
protected:
	virtual bool ReadResult(CNamedPipeServer * pipeServer, AfxInteropBoolCalcResult_t & outResult) override
	{
		bool result;
		if (!pipeServer->ReadBoolean(result)) return false;

		outResult.Result = result;

		return true;
	}

	virtual void CallResult(AfxInteropBoolCalcCallback_t callback, AfxInteropBoolCalcResult_t * result) override
	{
		callback(result);
	}
};

class CAfxInteropIntCalcCallbacks : public CAfxInteropCalcCallbacks<AfxInteropIntCalcCallback_t, AfxInteropIntCalcResult_t>
{
protected:
	virtual bool ReadResult(CNamedPipeServer * pipeServer, AfxInteropIntCalcResult_t & outResult) override
	{
		INT32 result;
		if (!pipeServer->ReadInt32(result)) return false;

		outResult.Result = result;

		return true;
	}

	virtual void CallResult(AfxInteropIntCalcCallback_t callback, AfxInteropIntCalcResult_t * result) override
	{
		callback(result);
	}
};

typedef bool(__stdcall * AfxInteropOnViewOverrideCallback_t)(float & Tx, float  & Ty, float  & Tz, float  & Rx, float  & Ry, float  & Rz, float & Fov);

bool AfxHookUnityWaitForGPU();

class CAfxInterop
{
public:
	const INT32 Version = 5;

	CAfxInterop(
		const char * pipeName,
		AfxInteropCommands_t afxInteropCommands,
		AfxInteropRender_t afxInteropRender,
		AfxInteropRenderPass_t afxInteropRenderPass)
		: m_PipeName(pipeName)
		, m_AfxInteropCommands(afxInteropCommands)
		, m_AfxInteropRender(afxInteropRender)
		, m_AfxInteropRenderPass(afxInteropRenderPass)
		, m_DrawingDo(&CAfxInterop::DrawingThread_Nop)
	{
		
	}

	static void SetOnViewOverrideCallback(AfxInteropOnViewOverrideCallback_t callback)
	{
		m_OnViewOverrideCallback = callback;
	}

	static IAfxInteropCalcCallbacksIterator * AddHandleCalcCallback(const char * name, AfxInteropHandleCalcCallback_t callback)
	{
		return m_HandleCalcCallbacks.Add(name, callback);
	}

	static IAfxInteropCalcCallbacksIterator * AddVecAngCalcCallback(const char * name, AfxInteropVecAngCalcCallback_t callback)
	{
		return m_VecAngCalcCallbacks.Add(name, callback);
	}

	static IAfxInteropCalcCallbacksIterator * AddCamCalcCallback(const char * name, AfxInteropCamCalcCallback_t callback)
	{
		return m_CamCalcCallbacks.Add(name, callback);
	}

	static IAfxInteropCalcCallbacksIterator * AddFovCalcCallback(const char * name, AfxInteropFovCalcCallback_t callback)
	{
		return m_FovCalcCallbacks.Add(name, callback);
	}

	static IAfxInteropCalcCallbacksIterator * AddBoolCalcCallback(const char * name, AfxInteropBoolCalcCallback_t callback)
	{
		return m_BoolCalcCallbacks.Add(name, callback);
	}

	static IAfxInteropCalcCallbacksIterator * AddIntCalcCallback(const char * name, AfxInteropIntCalcCallback_t callback)
	{
		return m_IntCalcCallbacks.Add(name, callback);
	}

	void ScheduleCommand(const char * command)
	{
		m_Commands.emplace(command);
	}

	bool UpdateEngineThread(void)
	{
		std::unique_lock<std::mutex> lock(m_EngineServerMutex);

		if (nullptr == m_EnginePipeServer)
		{
			m_EnginePipeServer = new CNamedPipeServer(m_PipeName.c_str());
			m_EngineServerNew = true;
		}

		CNamedPipeServer::State state = m_EnginePipeServer->Connect();

		switch (state)
		{
		case CNamedPipeServer::State_Connected:
			{
				if (m_EngineServerNew)
				{
					m_EngineReconnected = true;

					{
						std::unique_lock<std::mutex> lock(m_DrawingServerMutex);

						if (nullptr == m_DrawingPipeServer)
						{
							std::string pipeName(m_PipeName);
							pipeName.append("_drawing");

							m_DrawingPipeServer = new CNamedPipeServer(pipeName.c_str());
							m_Connecting = true;
						}

						CNamedPipeServer::State drawingState = m_DrawingPipeServer->Connect();

						switch (drawingState)
						{
						case CNamedPipeServer::State_Connected:
							m_Connecting = false;
							break;
						case CNamedPipeServer::State_Error:
							goto locked_error;
						default:
							return true;
						}
					}

					// Check if our version is supported by client:

					if (!m_EnginePipeServer->WriteInt32(Version)) goto locked_error;

					if (!m_EnginePipeServer->Flush()) goto locked_error;

					bool versionSupported;				
					if (!m_EnginePipeServer->ReadBoolean(versionSupported)) goto locked_error;

					if (!versionSupported) goto locked_error;

					// Supply server info required by client:

#if _WIN64
					if (!m_EnginePipeServer->WriteBoolean(true)) goto locked_error;
#else
					if (!m_EnginePipeServer->WriteBoolean(false)) goto locked_error;
#endif

					if (!m_EnginePipeServer->Flush()) goto locked_error;

					//

					m_EngineServerNew = false;
				}

				bool done = false;

				BOOL beforeTranslucentShadow;
				BOOL afterTranslucentShadow;
				BOOL beforeTranslucent;
				BOOL afterTranslucent;
				BOOL beforeHud;
				BOOL afterHud;

				while (!done)
				{
					INT32 engineMessage;
					if (!m_EnginePipeServer->ReadInt32(engineMessage)) goto locked_error;

					switch (engineMessage)
					{
					case EngineMessage_BeforeFrameStart:
					{
						// Read incoming commands from client:
						{
							CAfxCommands commands;

							UINT32 commandIndex = 0;
							UINT32 commandCount;
							if (!m_EnginePipeServer->ReadCompressedUInt32(commandCount)) goto locked_error;

							commands.SetCommands(commandCount);

							while (0 < commandCount)
							{
								UINT32 argIndex = 0;
								UINT32 argCount;
								if (!m_EnginePipeServer->ReadCompressedUInt32(argCount)) goto locked_error;

								commands.SetArgs(commandIndex, argCount);

								while (0 < argCount)
								{
									if (!m_EnginePipeServer->ReadStringUTF8(commands.GetArgString(commandIndex, argIndex))) goto locked_error;

									--argCount;
									++argIndex;
								}

								--commandCount;
								++commandIndex;
							}

							m_AfxInteropCommands(&commands);
						}

						if(!m_EnginePipeServer->WriteCompressedUInt32((UINT32)m_Commands.size())) goto locked_error;

						while (!m_Commands.empty())
						{
							if (!m_EnginePipeServer->WriteStringUTF8(m_Commands.front().c_str())) goto locked_error;
							m_Commands.pop();
						}

						if (!m_EnginePipeServer->Flush()) goto locked_error;
					}
					break;

					case EngineMessage_BeforeFrameRenderStart:
					{

					}
					break;

					case EngineMessage_AfterFrameRenderStart:
					{
						if (!m_HandleCalcCallbacks.BatchUpdateRequest(m_EnginePipeServer)) goto locked_error;
						if (!m_VecAngCalcCallbacks.BatchUpdateRequest(m_EnginePipeServer)) goto locked_error;
						if (!m_CamCalcCallbacks.BatchUpdateRequest(m_EnginePipeServer)) goto locked_error;
						if (!m_FovCalcCallbacks.BatchUpdateRequest(m_EnginePipeServer)) goto locked_error;
						if (!m_BoolCalcCallbacks.BatchUpdateRequest(m_EnginePipeServer)) goto locked_error;
						if (!m_IntCalcCallbacks.BatchUpdateRequest(m_EnginePipeServer)) goto locked_error;

						if (!m_EnginePipeServer->Flush()) goto locked_error;

						if (!m_HandleCalcCallbacks.BatchUpdateResult(m_EnginePipeServer)) goto locked_error;
						if (!m_VecAngCalcCallbacks.BatchUpdateResult(m_EnginePipeServer)) goto locked_error;
						if (!m_CamCalcCallbacks.BatchUpdateResult(m_EnginePipeServer)) goto locked_error;
						if (!m_FovCalcCallbacks.BatchUpdateResult(m_EnginePipeServer)) goto locked_error;
						if (!m_BoolCalcCallbacks.BatchUpdateResult(m_EnginePipeServer)) goto locked_error;
						if (!m_IntCalcCallbacks.BatchUpdateResult(m_EnginePipeServer)) goto locked_error;
					}
					break;

					case EngineMessage_OnRenderView:
					{
						CAfxInteropRenderInfo renderInfo;

						if(!m_EnginePipeServer->ReadInt32(renderInfo.FrameCount)) goto locked_error;

						if (!m_EnginePipeServer->ReadSingle(renderInfo.AbsoluteFrameTime)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.CurTime)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.FrameTime)) goto locked_error;

						if (!m_EnginePipeServer->ReadInt32(renderInfo.View.X)) goto locked_error;
						if (!m_EnginePipeServer->ReadInt32(renderInfo.View.Y)) goto locked_error;
						if (!m_EnginePipeServer->ReadInt32(renderInfo.View.Width)) goto locked_error;
						if (!m_EnginePipeServer->ReadInt32(renderInfo.View.Height)) goto locked_error;

						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ViewMatrix.M00)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ViewMatrix.M01)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ViewMatrix.M02)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ViewMatrix.M03)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ViewMatrix.M10)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ViewMatrix.M11)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ViewMatrix.M12)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ViewMatrix.M13)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ViewMatrix.M20)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ViewMatrix.M21)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ViewMatrix.M22)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ViewMatrix.M23)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ViewMatrix.M30)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ViewMatrix.M31)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ViewMatrix.M32)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ViewMatrix.M33)) goto locked_error;

						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ProjectionMatrix.M00)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ProjectionMatrix.M01)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ProjectionMatrix.M02)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ProjectionMatrix.M03)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ProjectionMatrix.M10)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ProjectionMatrix.M11)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ProjectionMatrix.M12)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ProjectionMatrix.M13)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ProjectionMatrix.M20)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ProjectionMatrix.M21)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ProjectionMatrix.M22)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ProjectionMatrix.M23)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ProjectionMatrix.M30)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ProjectionMatrix.M31)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ProjectionMatrix.M32)) goto locked_error;
						if (!m_EnginePipeServer->ReadSingle(renderInfo.View.ProjectionMatrix.M33)) goto locked_error;

						BOOL colorTextureWasLost;
						HANDLE sharedColorTextureHandle;
						BOOL colorDepthTextureWasLost;
						HANDLE shareDepthColorTextureHandle;

						m_AfxInteropRender(
							&renderInfo, &colorTextureWasLost, &sharedColorTextureHandle, &colorDepthTextureWasLost, &shareDepthColorTextureHandle,
							&beforeTranslucentShadow, &afterTranslucentShadow,
							&beforeTranslucent, &afterTranslucent,
							&beforeHud, &afterHud);

						if (!m_EnginePipeServer->WriteBoolean(beforeTranslucentShadow)) goto locked_error;
						if (!m_EnginePipeServer->WriteBoolean(afterTranslucentShadow)) goto locked_error;
						if (!m_EnginePipeServer->WriteBoolean(beforeTranslucent)) goto locked_error;
						if (!m_EnginePipeServer->WriteBoolean(afterTranslucent)) goto locked_error;
						if (!m_EnginePipeServer->WriteBoolean(beforeHud)) goto locked_error;
						if (!m_EnginePipeServer->WriteBoolean(afterHud)) goto locked_error;

						if (!(beforeTranslucentShadow || afterTranslucentShadow || beforeTranslucent || afterTranslucent || beforeHud || afterHud)) done = true;

						if(!done)
						{
							std::unique_lock<std::mutex> lock(m_DrawingDataQueueMutex);

							m_DrawingDataQueue.emplace(renderInfo.FrameCount, colorTextureWasLost || m_EngineReconnected, sharedColorTextureHandle, colorDepthTextureWasLost || m_EngineReconnected, shareDepthColorTextureHandle);

							m_EngineReconnected = false;
						}
					} break;

					case EngineMessage_OnRenderViewEnd:
						done = true;
						break;

					case EngineMessage_BeforeTranslucentShadow:
						if(!DoRenderPass(AfxRenderPassType_BeforeTranslucentShadow)) goto locked_error;
						break;
					case EngineMessage_AfterTranslucentShadow:
						if (!DoRenderPass(AfxRenderPassType_AfterTranslucentShadow)) goto locked_error;
						break;
					case EngineMessage_BeforeTranslucent:
						if (!DoRenderPass(AfxRenderPassType_BeforeTranslucent)) goto locked_error;
						break;
					case EngineMessage_AfterTranslucent:
						if (!DoRenderPass(AfxRenderPassType_AfterTranslucent)) goto locked_error;
						break;

					case EngineMessage_OnViewOverride:
					{
						if (m_OnViewOverrideCallback)
						{
							float Tx, Ty, Tz, Rx, Ry, Rz, Fov;
							if (m_OnViewOverrideCallback(Tx, Ty, Tz, Rx, Ry, Rz, Fov))
							{
								if (!m_EnginePipeServer->WriteBoolean(true)) goto locked_error;

								if (!m_EnginePipeServer->WriteSingle(Tx)) goto locked_error;
								if (!m_EnginePipeServer->WriteSingle(Ty)) goto locked_error;
								if (!m_EnginePipeServer->WriteSingle(Tz)) goto locked_error;
								if (!m_EnginePipeServer->WriteSingle(Rx)) goto locked_error;
								if (!m_EnginePipeServer->WriteSingle(Ry)) goto locked_error;
								if (!m_EnginePipeServer->WriteSingle(Rz)) goto locked_error;
								if (!m_EnginePipeServer->WriteSingle(Fov)) goto locked_error;
							}
							else
							{
								if (!m_EnginePipeServer->WriteBoolean(false)) goto locked_error;
							}
						}
						else
						{
							if (!m_EnginePipeServer->WriteBoolean(false)) goto locked_error;
						}

						if(!m_EnginePipeServer->Flush()) goto locked_error; // client is waiting
					}
					break;
					}
				}
			}
			break;
		case CNamedPipeServer::State_Error:
			goto locked_error;
		}

		return true;

	locked_error:
		lock.unlock();
		Close();
		return false;
	}

	void UpdateDrawingThreadBegin()
	{
		if (&CAfxInterop::DrawingThread_Nop != m_DrawingDo)
		{
			OutputDebugStringA("AfxInterop.dll error: Invalid state in DrawingThread_Prepare.\n");
			m_DrawingServerMutex.unlock();
		}

		DrawingThread_Prepare();
	}

	void UpdateDrawingThreadContinue()
	{
		if (m_DrawingDo)
		{
			(((*this).*(m_DrawingDo)))();
		}
		else
		{
			OutputDebugStringA("AfxInterop.dll error: Invalid state in UpdateTrawingThreadContinue.\n");
		}
	}

	~CAfxInterop()
	{
		Close();
	}

private:
	enum EngineMessage
	{
		EngineMessage_Invalid = 0,
		EngineMessage_LevelInitPreEntity = 1,
		EngineMessage_LevelShutDown = 2,
		EngineMessage_BeforeFrameStart = 3,
		EngineMessage_OnRenderView = 4,
		EngineMessage_OnRenderViewEnd = 5,
		EngineMessage_BeforeFrameRenderStart = 6,
		EngineMessage_AfterFrameRenderStart = 7,
		EngineMessage_OnViewOverride = 8,
		EngineMessage_BeforeTranslucentShadow = 9,
		EngineMessage_AfterTranslucentShadow = 10,
		EngineMessage_BeforeTranslucent = 11,
		EngineMessage_AfterTranslucent = 12
	};

	enum DrawingMessage
	{
		DrawingMessage_Invalid = 0,
		DrawingMessage_PreapareDraw = 1,
		DrawingMessage_BeforeTranslucentShadow = 2,
		DrawingMessage_AfterTranslucentShadow = 3,
		DrawingMessage_BeforeTranslucent = 4,
		DrawingMessage_AfterTranslucent = 5,
		DrawingMessage_BeforeHud = 6,
		DrawingMessage_AfterHud = 7,
		DrawingMessage_OnRenderViewEnd = 8
	};

	enum PrepareDrawReply
	{
		PrepareDrawReply_Skip = 1,
		PrepareDrawReply_Retry = 2,
		PrepareDrawReply_Continue = 3
	};

	struct CDrawingData
	{
		bool HasData;
		INT32 FrameNumber;
		BOOL ColorTextureWasLost;
		HANDLE SharedColorTextureHandle;
		BOOL ColorDepthTextureWasLost;
		HANDLE SharedColorDepthTextureHandle;

		CDrawingData()
			: HasData(false)
		{
		}

		CDrawingData(INT32 frameNumber, BOOL colorTextureWasLost, HANDLE sharedColorTextureHandle, BOOL colorDepthTextureWasLost, HANDLE sharedColorDepthTextureHandle)
			: HasData(true)
			, FrameNumber(frameNumber)
			, ColorTextureWasLost(colorTextureWasLost)
			, SharedColorTextureHandle(sharedColorTextureHandle)
			, ColorDepthTextureWasLost(colorDepthTextureWasLost)
			, SharedColorDepthTextureHandle(sharedColorDepthTextureHandle)
		{

		}
	};

	typedef void (CAfxInterop::*Do_t)(void);

	std::string m_PipeName;

	AfxInteropCommands_t m_AfxInteropCommands;
	AfxInteropRender_t m_AfxInteropRender;
	AfxInteropRenderPass_t m_AfxInteropRenderPass;

	bool m_EngineReconnected = true;

	std::mutex m_EngineServerMutex;
	CNamedPipeServer * m_EnginePipeServer = nullptr;
	bool m_EngineServerNew = false;

	std::mutex m_DrawingServerMutex;
	bool m_Connecting = false;
	CNamedPipeServer * m_DrawingPipeServer = nullptr;

	std::mutex m_DrawingDataQueueMutex;
	std::queue<CDrawingData> m_DrawingDataQueue;
	Do_t m_DrawingDo;

	std::queue<std::string> m_Commands;

	static AfxInteropOnViewOverrideCallback_t m_OnViewOverrideCallback;

	static CAfxInteropHandleCalcCallbacks m_HandleCalcCallbacks;
	static CAfxInteropVecAngCalcCallbacks m_VecAngCalcCallbacks;
	static CAfxInteropCamCalcCallbacks m_CamCalcCallbacks;
	static CAfxInteropFovCalcCallbacks m_FovCalcCallbacks;
	static CAfxInteropBoolCalcCallbacks m_BoolCalcCallbacks;
	static CAfxInteropIntCalcCallbacks m_IntCalcCallbacks;

	void Close()
	{
		std::unique_lock<std::mutex> lock(m_EngineServerMutex);

		{
			std::unique_lock<std::mutex> lock(m_DrawingServerMutex);

			if (nullptr != m_DrawingPipeServer)
			{
				delete m_DrawingPipeServer;
				m_DrawingPipeServer = nullptr;
			}
		}

		if (nullptr != m_EnginePipeServer)
		{
			delete m_EnginePipeServer;
			m_EnginePipeServer = nullptr;
		}
	}

	void DrawingThread_Nop()
	{
	}

	void DrawingThread_Wait()
	{
		AfxHookUnityWaitForGPU();

		// Signal okay to continue for waiting CS:GO client:
		if (!m_DrawingPipeServer->WriteBoolean(true)) goto locked_error;
		if (!m_DrawingPipeServer->Flush()) goto locked_error;

		return DrawingThread_Continue();

	locked_error:
		m_DrawingServerMutex.unlock();
		Close();

		// Abort.
		m_DrawingDo = &CAfxInterop::DrawingThread_Nop;
		return;
	}

	void DrawingThread_Continue()
	{
		bool done = false;

		while (!done)
		{
			// Wait for client:
			INT32 followUpMessage;
			if (!m_DrawingPipeServer->ReadInt32(followUpMessage)) goto locked_error;

			switch (followUpMessage)
			{
			case DrawingMessage_BeforeTranslucentShadow:
			case DrawingMessage_AfterTranslucentShadow:
			case DrawingMessage_BeforeTranslucent:
			case DrawingMessage_AfterTranslucent:
			case DrawingMessage_BeforeHud:
			case DrawingMessage_AfterHud:
				// Wait for Unity:
				m_DrawingDo = &CAfxInterop::DrawingThread_Wait;
				return;

			case DrawingMessage_OnRenderViewEnd:
				done = true;
				break;
		
			default:
				goto locked_error;
			}
		}

		m_DrawingServerMutex.unlock();

		// Done.
		m_DrawingDo = &CAfxInterop::DrawingThread_Nop;
		return;

	locked_error:
		m_DrawingServerMutex.unlock();
		Close();

		// Abort.
		m_DrawingDo = &CAfxInterop::DrawingThread_Nop;
		return; 
	}

	void DrawingThread_Prepare()
	{
		m_DrawingServerMutex.lock();

		std::unique_lock<std::mutex> drawingDataQueueLock(m_DrawingDataQueueMutex);
		CDrawingData drawingData = m_DrawingDataQueue.front();

		if (!drawingData.HasData)
		{
			m_DrawingDataQueue.pop();
			m_DrawingServerMutex.unlock();
			return;
		}

		drawingDataQueueLock.unlock();

		if (nullptr == m_DrawingPipeServer || m_Connecting)
		{
			drawingDataQueueLock.lock();
			m_DrawingDataQueue.pop();

			m_DrawingServerMutex.unlock();

			// Pipe not avilable.
			m_DrawingDo = &CAfxInterop::DrawingThread_Nop;
			return;
		}

		CNamedPipeServer::State state = m_DrawingPipeServer->Connect();

		switch (state)
		{
		case CNamedPipeServer::State_Connected:
		{
			while (true)
			{
				INT32 drawingMessage;
				if (!m_DrawingPipeServer->ReadInt32(drawingMessage)) goto locked_error;

				switch (drawingMessage)
				{
				case DrawingMessage_PreapareDraw:
				{
					INT32 clientFrameNumber;
					if (!m_DrawingPipeServer->ReadInt32(clientFrameNumber)) goto locked_error;

					INT32 frameDiff = drawingData.HasData ? drawingData.FrameNumber - clientFrameNumber : -1;

					if (frameDiff < 0)
					{
						// Error: client is ahead, otherwise we would have correct data by now.

						if (!m_DrawingPipeServer->WriteInt32(PrepareDrawReply_Retry)) goto locked_error;
						if (!m_DrawingPipeServer->Flush()) goto locked_error;

						drawingDataQueueLock.lock();
						m_DrawingDataQueue.pop();

						m_DrawingServerMutex.unlock();

						// Try again next frame.
						m_DrawingDo = &CAfxInterop::DrawingThread_Nop;
						return;
					}
					else if (frameDiff > 0)
					{
						// client is behind.

						if (!m_DrawingPipeServer->WriteInt32(PrepareDrawReply_Skip)) goto locked_error;
						if (!m_DrawingPipeServer->Flush()) goto locked_error;
					}
					else
					{
						// we are right on.

						//drawingThreadImplementation.Log("AfxHookSource client is in sync");

						if (!m_DrawingPipeServer->WriteInt32(PrepareDrawReply_Continue)) goto locked_error;

						if (!m_DrawingPipeServer->WriteBoolean(drawingData.ColorTextureWasLost)) goto locked_error;
						if (!m_DrawingPipeServer->WriteHandle(drawingData.SharedColorTextureHandle)) goto locked_error;
						if (!m_DrawingPipeServer->WriteBoolean(drawingData.ColorDepthTextureWasLost)) goto locked_error;
						if (!m_DrawingPipeServer->WriteHandle(drawingData.SharedColorDepthTextureHandle)) goto locked_error;

						if (!m_DrawingPipeServer->Flush()) goto locked_error;

						drawingDataQueueLock.lock();
						m_DrawingDataQueue.pop();
						drawingDataQueueLock.unlock();

						return DrawingThread_Continue();
					}
				} break;
				case DrawingMessage_OnRenderViewEnd:
				{
					drawingDataQueueLock.lock();
					m_DrawingDataQueue.pop();

					m_DrawingServerMutex.unlock();

					m_DrawingDo = &CAfxInterop::DrawingThread_Nop;
				} return;
				default:
					goto locked_error;
				}
			}
		} break;
		case CNamedPipeServer::State_Error:
			goto locked_error;
		}

		// This is an error too, because we should be connected here.
		goto locked_error;

	locked_error:
		drawingDataQueueLock.lock();
		m_DrawingDataQueue.pop();
		drawingDataQueueLock.unlock();

		m_DrawingServerMutex.unlock();
		Close();

		// Abort.
		m_DrawingDo = &CAfxInterop::DrawingThread_Nop;
		return;
	}

	bool DoRenderPass(AfxRenderPassType_t pass)
	{
		CAfxInteropView view;

		if (!m_EnginePipeServer->ReadInt32(view.X)) return false;
		if (!m_EnginePipeServer->ReadInt32(view.Y)) return false;
		if (!m_EnginePipeServer->ReadInt32(view.Width)) return false;
		if (!m_EnginePipeServer->ReadInt32(view.Height)) return false;

		if (!m_EnginePipeServer->ReadSingle(view.ViewMatrix.M00)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ViewMatrix.M01)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ViewMatrix.M02)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ViewMatrix.M03)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ViewMatrix.M10)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ViewMatrix.M11)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ViewMatrix.M12)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ViewMatrix.M13)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ViewMatrix.M20)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ViewMatrix.M21)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ViewMatrix.M22)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ViewMatrix.M23)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ViewMatrix.M30)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ViewMatrix.M31)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ViewMatrix.M32)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ViewMatrix.M33)) return false;

		if (!m_EnginePipeServer->ReadSingle(view.ProjectionMatrix.M00)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ProjectionMatrix.M01)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ProjectionMatrix.M02)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ProjectionMatrix.M03)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ProjectionMatrix.M10)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ProjectionMatrix.M11)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ProjectionMatrix.M12)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ProjectionMatrix.M13)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ProjectionMatrix.M20)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ProjectionMatrix.M21)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ProjectionMatrix.M22)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ProjectionMatrix.M23)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ProjectionMatrix.M30)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ProjectionMatrix.M31)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ProjectionMatrix.M32)) return false;
		if (!m_EnginePipeServer->ReadSingle(view.ProjectionMatrix.M33)) return false;

		m_AfxInteropRenderPass(pass, &view);

		return true;
	}
};

CAfxInterop * g_AfxInterop = nullptr;

AfxInteropOnViewOverrideCallback_t CAfxInterop::m_OnViewOverrideCallback = nullptr;

CAfxInteropHandleCalcCallbacks CAfxInterop::m_HandleCalcCallbacks;
CAfxInteropVecAngCalcCallbacks CAfxInterop::m_VecAngCalcCallbacks;
CAfxInteropCamCalcCallbacks CAfxInterop::m_CamCalcCallbacks;
CAfxInteropFovCalcCallbacks CAfxInterop::m_FovCalcCallbacks;
CAfxInteropBoolCalcCallbacks CAfxInterop::m_BoolCalcCallbacks;
CAfxInteropIntCalcCallbacks CAfxInterop::m_IntCalcCallbacks;


extern "C" __declspec(dllexport) void __stdcall AfxInteropDestroy()
{
	if (g_AfxInterop)
	{
		delete g_AfxInterop;
		g_AfxInterop = nullptr;
	}
}

extern "C" __declspec(dllexport) bool __stdcall AfxInteropCreate(
	const char * pipeName,
	AfxInteropCommands_t afxInteropCommands,
	AfxInteropRender_t afxInteropRender,
	AfxInteropRenderPass_t afxInteropRenderPass)
{
	AfxInteropDestroy();

	g_AfxInterop = new CAfxInterop(pipeName, afxInteropCommands, afxInteropRender, afxInteropRenderPass);

	return true;
}


extern "C" __declspec(dllexport) bool __stdcall AfxInteropUpdateEngineThread()
{
	if (g_AfxInterop)
	{
		g_AfxInterop->UpdateEngineThread();
		return true;
	}

	return false;
}

extern "C" __declspec(dllexport) bool __stdcall AfxInteropScheduleCommand(const char * command)
{
	if (g_AfxInterop)
	{
		g_AfxInterop->ScheduleCommand(command);
		return true;
	}

	return false;
}

extern "C" __declspec(dllexport) void __stdcall AfxInteropSetOnViewOverrideCallback(AfxInteropOnViewOverrideCallback_t callback)
{
	CAfxInterop::SetOnViewOverrideCallback(callback);
}

extern "C" __declspec(dllexport) IAfxInteropCalcCallbacksIterator * __stdcall AfxInteropAddHandleCalcCallback(const char * name, AfxInteropHandleCalcCallback_t callback)
{
	return CAfxInterop::AddHandleCalcCallback(name, callback);
}

extern "C" __declspec(dllexport) IAfxInteropCalcCallbacksIterator * __stdcall AfxInteropAddVecAngCalcCallback(const char * name, AfxInteropVecAngCalcCallback_t callback)
{
	return CAfxInterop::AddVecAngCalcCallback(name, callback);
}

extern "C" __declspec(dllexport) IAfxInteropCalcCallbacksIterator * __stdcall AfxInteropAddCamCalcCallback(const char * name, AfxInteropCamCalcCallback_t callback)
{
	return CAfxInterop::AddCamCalcCallback(name, callback);
}

extern "C" __declspec(dllexport) IAfxInteropCalcCallbacksIterator * __stdcall AfxInteropAddFovCalcCallback(const char * name, AfxInteropFovCalcCallback_t callback)
{
	return CAfxInterop::AddFovCalcCallback(name, callback);
}

extern "C" __declspec(dllexport) IAfxInteropCalcCallbacksIterator * __stdcall AfxInteropAddBoolCalcCallback(const char * name, AfxInteropBoolCalcCallback_t callback)
{
	return CAfxInterop::AddBoolCalcCallback(name, callback);
}

extern "C" __declspec(dllexport) IAfxInteropCalcCallbacksIterator * __stdcall AfxInteropAddIntCalcCallback(const char * name, AfxInteropIntCalcCallback_t callback)
{
	return CAfxInterop::AddIntCalcCallback(name, callback);
}


extern "C" __declspec(dllexport) void __stdcall AfxInteropRemoveCallback(IAfxInteropCalcCallbacksIterator * iterator)
{
	if (iterator) iterator->Destroy();
}

////////////////////////////////////////////////////////////////////////////////

#include <shared/Detours/src/detours.h>

#include "IUnityInterface.h"
#include "IUnityGraphics.h"
#include "IUnityGraphicsD3D11.h"

bool g_ColorTextureWasLost = false;
bool g_ColorDepthTextureWasLost = false;
HANDLE g_SharedColorTextureHandle = NULL;
HANDLE g_SharedColorDepthTextureHandle = NULL;

BOOL APIENTRY DllMain(HMODULE hModule,
	DWORD  ul_reason_for_call,
	LPVOID lpReserved
)
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
		break;
	case DLL_THREAD_ATTACH:
		break;
	case DLL_THREAD_DETACH:
		break;
	case DLL_PROCESS_DETACH:
		break;
	}
	return TRUE;
}

extern "C" __declspec(dllexport) int __stdcall AfxInteropVersion(void) {
	return 4;
}

extern "C" __declspec(dllexport) HANDLE __stdcall AfxInteropGetSharedHandle(void * d3d11ResourcePtr)
{
	HANDLE result = NULL;

	if (d3d11ResourcePtr)
	{
		ID3D11Resource * resource = (ID3D11Resource *)d3d11ResourcePtr;

		IDXGIResource* dxgiResource;

		if (SUCCEEDED(resource->QueryInterface(__uuidof(IDXGIResource), (void**)&dxgiResource)))
		{
			if (FAILED(dxgiResource->GetSharedHandle(&result)))
			{
				result = NULL;
			}
			dxgiResource->Release();
		}
	}

	return result;
}

static void UNITY_INTERFACE_API OnRenderEvent(int eventId)
{
	switch (eventId)
	{
	case 2:
		if (g_AfxInterop) g_AfxInterop->UpdateDrawingThreadBegin();
		break;
	case 3:
		if (g_AfxInterop) g_AfxInterop->UpdateDrawingThreadContinue();
		break;

	}
}

static void UNITY_INTERFACE_API OnRenderEventAndData(int eventId, void* data)
{
}

// Freely defined function to pass a callback to plugin-specific scripts
extern "C" UnityRenderingEvent UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API AfxInteropGetRenderEventFunc()
{
	return OnRenderEvent;
}

// Freely defined function to pass a callback to plugin-specific scripts
extern "C" UnityRenderingEventAndData UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API AfxInteropGetRenderEventAndDataFunc()
{
	return OnRenderEventAndData;
}


ID3D11Device *pDevice = NULL;
ID3D11DeviceContext * pContext = NULL;
ID3D11Query * pQuery = NULL;

bool g_FbOverride = false;

typedef HRESULT(STDMETHODCALLTYPE * CreateDeferredContext_t)(
	ID3D11Device * This,
	UINT ContextFlags,
	/* [annotation] */
	_COM_Outptr_opt_  ID3D11DeviceContext **ppDeferredContext);

CreateDeferredContext_t True_CreateDeferredContext;

HRESULT STDMETHODCALLTYPE My_CreateDeferredContext(
	ID3D11Device * This,
	UINT ContextFlags,
	/* [annotation] */
	_COM_Outptr_opt_  ID3D11DeviceContext **ppDeferredContext)
{
	HRESULT result = True_CreateDeferredContext(This, ContextFlags, ppDeferredContext);

	if (SUCCEEDED(result) && ppDeferredContext && *ppDeferredContext)
	{
		pContext = *ppDeferredContext;
	}

	return result;
}

typedef HRESULT(STDMETHODCALLTYPE * CreateTexture2D_t)(
	ID3D11Device * This,
	/* [annotation] */
	_In_  const D3D11_TEXTURE2D_DESC *pDesc,
	/* [annotation] */
	_In_reads_opt_(_Inexpressible_(pDesc->MipLevels * pDesc->ArraySize))  const D3D11_SUBRESOURCE_DATA *pInitialData,
	/* [annotation] */
	_COM_Outptr_opt_  ID3D11Texture2D **ppTexture2D);

CreateTexture2D_t True_CreateTexture2D;

HRESULT STDMETHODCALLTYPE My_CreateTexture2D(
	ID3D11Device * This,
	/* [annotation] */
	_In_  const D3D11_TEXTURE2D_DESC *pDesc,
	/* [annotation] */
	_In_reads_opt_(_Inexpressible_(pDesc->MipLevels * pDesc->ArraySize))  const D3D11_SUBRESOURCE_DATA *pInitialData,
	/* [annotation] */
	_COM_Outptr_opt_  ID3D11Texture2D **ppTexture2D)
{
	if (g_FbOverride && pDesc && ppTexture2D)
	{
		switch (pDesc->Format)
		{
		case DXGI_FORMAT_D32_FLOAT_S8X24_UINT:
		case DXGI_FORMAT_D32_FLOAT:
		case DXGI_FORMAT_D24_UNORM_S8_UINT:
		case DXGI_FORMAT_R24_UNORM_X8_TYPELESS:
		case DXGI_FORMAT_D16_UNORM:
			break;
		default:
			if (g_FbOverride)
			{
				g_FbOverride = false;

				D3D11_TEXTURE2D_DESC Desc = *pDesc;

				//Desc.Width = Width;
				//Desc.Height = Height;
				Desc.MipLevels = 1;
				Desc.ArraySize = 1;
				Desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
				Desc.SampleDesc.Count = 1;
				Desc.SampleDesc.Quality = 0;
				Desc.Usage = D3D11_USAGE_DEFAULT;
				Desc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
				Desc.CPUAccessFlags = 0;
				Desc.MiscFlags = D3D11_RESOURCE_MISC_SHARED;;


				HRESULT result = True_CreateTexture2D(This, &Desc, pInitialData, ppTexture2D);

				return result;
			}
			break;
		}
	}

	return True_CreateTexture2D(This, pDesc, pInitialData, ppTexture2D);
}

extern "C" __declspec(dllexport) void __stdcall AfxInteropBeginCreateRenderTexture()
{
	g_FbOverride = true;
}

static IUnityInterfaces* s_UnityInterfaces = NULL;
static IUnityGraphics* s_Graphics = NULL;
static UnityGfxRenderer s_RendererType = kUnityGfxRendererNull;
static IUnityGraphicsD3D11 * s_UnityGraphicsD3D11 = NULL;

bool AfxHookUnityWaitForGPU()
{
	bool bOk = false;
	bool immediateContextUsed = false;

	if (!pContext)
	{
		immediateContextUsed = true;
		pDevice->GetImmediateContext(&pContext);
	}

	if (pDevice && pQuery)
	{
		pContext->Flush();

		pContext->End(pQuery);

		while (S_OK != pContext->GetData(pQuery, NULL, 0, 0))
			;

		bOk = true;
	}

	if (immediateContextUsed)
	{
		pContext->Release();
		pContext = NULL;
	}

	return bOk;
}

static void UNITY_INTERFACE_API
OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
{
	switch (eventType)
	{
	case kUnityGfxDeviceEventInitialize:
	{
		s_RendererType = s_Graphics->GetRenderer();

		if (kUnityGfxRendererD3D11 == s_RendererType)
		{
			s_UnityGraphicsD3D11 = s_UnityInterfaces->Get<IUnityGraphicsD3D11>();

			if (s_UnityGraphicsD3D11)
			{
				if (pDevice = s_UnityGraphicsD3D11->GetDevice())
				{
					DWORD oldProtect;
					VirtualProtect(pDevice, sizeof(void *) * 27, PAGE_EXECUTE_READWRITE, &oldProtect);


					True_CreateTexture2D = (CreateTexture2D_t)*(void **)((*(char **)(pDevice)) + sizeof(void *) * 5);
					True_CreateDeferredContext = (CreateDeferredContext_t)*(void **)((*(char **)(pDevice)) + sizeof(void *) * 27);

					VirtualProtect(pDevice, sizeof(void *) * 27, oldProtect, NULL);

					DetourTransactionBegin();
					DetourUpdateThread(GetCurrentThread());
					DetourAttach(&(PVOID&)True_CreateTexture2D, My_CreateTexture2D);
					//DetourAttach(&(PVOID&)True_CreateDeferredContext, My_CreateDeferredContext);
					DetourTransactionCommit();

					D3D11_QUERY_DESC queryDesc = {
						D3D11_QUERY_EVENT, 0
					};

					if (FAILED((pDevice)->CreateQuery(&queryDesc, &pQuery)))
					{
						pQuery = NULL;
					}
				}
			}
		}
		
		break;
	}
	case kUnityGfxDeviceEventShutdown:
	{
		if (kUnityGfxRendererD3D11 == s_RendererType)
		{
			if (s_UnityGraphicsD3D11)
			{
				if (pQuery)
				{
					pQuery->Release();
					pQuery = NULL;
				}
				if (pDevice)
				{
					DetourTransactionBegin();
					DetourUpdateThread(GetCurrentThread());
					DetourDetach(&(PVOID&)True_CreateTexture2D, My_CreateTexture2D);
					//DetourDetach(&(PVOID&)True_CreateDeferredContext, My_CreateDeferredContext);
					DetourTransactionCommit();

					pDevice->Release();
					pDevice = NULL;
				}

				s_UnityGraphicsD3D11 = NULL;
			}
		}

		s_RendererType = kUnityGfxRendererNull;
		break;
	}
	case kUnityGfxDeviceEventBeforeReset:
	{
		//TODO: user Direct3D 9 code
		break;
	}
	case kUnityGfxDeviceEventAfterReset:
	{
		//TODO: user Direct3D 9 code
		break;
	}
	};
}

// Unity plugin load event
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
UnityPluginLoad(IUnityInterfaces* unityInterfaces)
{
	s_UnityInterfaces = unityInterfaces;
	s_Graphics = unityInterfaces->Get<IUnityGraphics>();

	s_Graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);

	// Run OnGraphicsDeviceEvent(initialize) manually on plugin load
	// to not miss the event in case the graphics device is already initialized
	OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);
}

// Unity plugin unload event
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
UnityPluginUnload()
{
	s_Graphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);
}
