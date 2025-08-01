using LoQA.Services;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public class EasyChatEngine : LoQA.Services.IEasyChatWrapper
{
    // Platform-specific library name resolution
#if WINDOWS
    private const string DllName = "chat.dll";
#elif ANDROID
    private const string DllName = "libchat.so";
#elif IOS || MACCATALYST
    private const string DllName = "__Internal";
#else
    private const string DllName = "chat"; // Fallback
#endif

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void TokenCallback([MarshalAs(UnmanagedType.LPUTF8Str)] string token);

    private TokenCallback? _managedCallback;
    private GCHandle _callbackHandle;

    public event Action<string>? OnTokenReceived;
    public bool IsInitialized { get; private set; } = false;

    #region P/Invoke Method Signatures
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "get_default_model_params")]
    private static extern ChatModelParams GetDefaultModelParams_Native();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "get_default_context_params")]
    private static extern ChatContextParams GetDefaultContextParams_Native();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "get_default_sampling_params")]
    private static extern ChatSamplingParams GetDefaultSamplingParams_Native();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "initializeLlama")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool initializeLlama(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string model_path,
        ChatModelParams model_params,
        ChatContextParams ctx_params);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "freeLlama")]
    private static extern void freeLlama();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "registerTokenCallback")]
    private static extern void registerTokenCallback(TokenCallback callback);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "generateResponse")]
    private static extern void generateResponse(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string prompt,
        int max_tokens);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "stopGeneration")]
    private static extern void stopGeneration_native();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "update_sampling_params")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool update_sampling_params_native(float temperature, float min_p, int seed);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "get_current_sampling_params")]
    private static extern ChatSamplingParams get_current_sampling_params_native();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "add_history_message")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool add_history_message_native([MarshalAs(UnmanagedType.LPUTF8Str)] string role, [MarshalAs(UnmanagedType.LPUTF8Str)] string content);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "clearConversation")]
    private static extern void clearConversation_native();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "get_last_error")]
    private static extern IntPtr get_last_error_native();
    #endregion

    #region Public Wrapper Methods

    public ChatModelParams GetDefaultModelParams() => GetDefaultModelParams_Native();
    public ChatContextParams GetDefaultContextParams() => GetDefaultContextParams_Native();
    public ChatSamplingParams GetDefaultSamplingParams() => GetDefaultSamplingParams_Native();

    public async Task<bool> InitializeAsync(string modelPath, ChatModelParams modelParams, ChatContextParams ctxParams)
    {
        if (IsInitialized)
        {
            Dispose();
        }

        try
        {
            bool success = await Task.Run(() => initializeLlama(modelPath, modelParams, ctxParams));

            if (success)
            {
                _managedCallback = (token) => OnTokenReceived?.Invoke(token);
                _callbackHandle = GCHandle.Alloc(_managedCallback);
                registerTokenCallback(_managedCallback);
                IsInitialized = true;
            }
            else
            {
                OnTokenReceived?.Invoke($"ERROR: Engine initialization failed: {GetLastError()}\n");
            }
            return success;
        }
        catch (Exception ex)
        {
            OnTokenReceived?.Invoke($"FATAL: An exception occurred during initialization: {ex.Message}\n");
            return false;
        }
    }

    public async Task GenerateAsync(string prompt, int maxTokens = 4096)
    {
        if (!IsInitialized)
        {
            OnTokenReceived?.Invoke("ERROR: Service is not initialized.\n");
            return;
        }
        await Task.Run(() => generateResponse(prompt, maxTokens));
    }

    public void StopGeneration()
    {
        if (!IsInitialized) return;
        stopGeneration_native();
    }

    public bool UpdateSamplingParams(ChatSamplingParams newParams)
    {
        if (!IsInitialized) return false;
        return update_sampling_params_native(newParams.temperature, newParams.min_p, newParams.seed);
    }

    public ChatSamplingParams GetCurrentSamplingParams()
    {
        if (!IsInitialized) return GetDefaultSamplingParams();
        return get_current_sampling_params_native();
    }

    public bool AddHistoryMessage(string role, string content)
    {
        if (!IsInitialized) return false;
        return add_history_message_native(role, content);
    }

    public void ClearConversation()
    {
        if (!IsInitialized) return;
        clearConversation_native();
    }

    public string GetLastError()
    {
        IntPtr errorPtr = get_last_error_native();
        return Marshal.PtrToStringUTF8(errorPtr) ?? "Unknown error";
    }

    public void Dispose()
    {
        if (IsInitialized)
        {
            freeLlama();
            IsInitialized = false;
        }
        if (_callbackHandle.IsAllocated)
        {
            _callbackHandle.Free();
        }
        GC.SuppressFinalize(this);
    }

    ~EasyChatEngine()
    {
        Dispose();
    }
    #endregion
}