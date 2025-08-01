using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LoQA.Services
{
    public class EasyChatService : IDisposable
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
        private GCHandle _callbackHandle; // Keep the delegate alive

        public event Action<string>? OnTokenReceived;
        public bool IsInitialized { get; private set; } = false;

        #region P/Invoke Structs (Correct)
        [StructLayout(LayoutKind.Sequential)]
        public struct ChatModelParams
        {
            public int n_gpu_layers;
            public int main_gpu;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public float[] tensor_split;
            [MarshalAs(UnmanagedType.I1)] public bool use_mmap;
            [MarshalAs(UnmanagedType.I1)] public bool use_mlock;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ChatContextParams
        {
            public int n_ctx;
            public int n_batch;
            public int n_threads;
            public int n_threads_batch;
            public float rope_freq_base;
            public float rope_freq_scale;
            [MarshalAs(UnmanagedType.I1)] public bool offload_kqv;
            [MarshalAs(UnmanagedType.I1)] public bool flash_attn;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ChatSamplingParams
        {
            public float temperature;
            public float min_p;
            public int seed; // Use -1 for random
        }
        #endregion

        #region P/Invoke Method Signatures (Corrected)
        // --- Core Lifecycle & Config ---
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "get_default_model_params")]
        public static extern ChatModelParams GetDefaultModelParams();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "get_default_context_params")]
        public static extern ChatContextParams GetDefaultContextParams();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "initializeLlama")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool initializeLlama(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string model_path,
            ChatModelParams model_params,
            ChatContextParams ctx_params);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "freeLlama")]
        private static extern void freeLlama();

        // --- Token Generation & Control ---
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "registerTokenCallback")]
        private static extern void registerTokenCallback(TokenCallback callback);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "generateResponse")]
        private static extern void generateResponse(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string prompt,
            int max_tokens);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "stopGeneration")]
        private static extern void stopGeneration_native();

        // --- Sampling Parameters ---
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "get_default_sampling_params")]
        public static extern ChatSamplingParams GetDefaultSamplingParams();

        // UPDATED: Signature now matches the C++ function exactly.
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "update_sampling_params")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool update_sampling_params_native(float temperature, float min_p, int seed);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "get_current_sampling_params")]
        private static extern ChatSamplingParams get_current_sampling_params_native();

        // --- Conversation History & State ---
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "add_history_message")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool add_history_message_native([MarshalAs(UnmanagedType.LPUTF8Str)] string role, [MarshalAs(UnmanagedType.LPUTF8Str)] string content);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "clear_internal_messages")]
        private static extern void clear_internal_messages_native();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "clearConversation")]
        private static extern void clearConversation_native();

        // UPDATED: Signature now correctly includes the token buffer and count.
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "importSessionState")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool importSessionState_native(
            byte[] state_buffer,
            nuint state_size,
            int[] token_buffer, // llama_token is int32_t
            nuint token_count);

        // UPDATED: Signature now matches C++ for clarity and safety.
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "exportSessionState")]
        private static extern nuint exportSessionState_native(byte[] dest_buffer, nuint buffer_size);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "getSessionStateSizeBytes")]
        private static extern nuint getSessionStateSizeBytes_native();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "getTokenHistoryCount")]
        private static extern nuint getTokenHistoryCount_native(); // Return type is size_t -> nuint

        // UPDATED: Signature now correctly uses an int array (IntPtr) for llama_token*.
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "exportTokenHistory")]
        private static extern nuint exportTokenHistory_native(
            [Out] int[] dest_buffer, // Out attribute for clarity
            nuint token_count);

        // --- Diagnostics & Error Handling ---
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "get_last_error")]
        private static extern IntPtr get_last_error_native();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "get_memory_info")]
        private static extern void get_memory_info_native(out nuint n_ctx_out, out nuint n_past_out);

        #endregion

        #region Public Wrapper Methods (Instance-based)

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
                    // Pin the delegate to prevent the garbage collector from moving it
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

        public void Generate(string prompt, int maxTokens = 4096)
        {
            if (!IsInitialized)
            {
                OnTokenReceived?.Invoke("ERROR: Service is not initialized.\n");
                return;
            }
            generateResponse(prompt, maxTokens);
        }

        public void StopGeneration()
        {
            if (!IsInitialized) return;
            stopGeneration_native();
        }

        // UPDATED: Public method now passes the struct fields as separate arguments.
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

        public bool AddHistoryMessage(string author, string text)
        {
            if (!IsInitialized) return false;
            // The C++ function is named add_history_message, ensure you are calling the correct native method.
            return add_history_message_native(author, text);
        }

        public void ClearInternalMessages()
        {
            if (!IsInitialized) return;
            clear_internal_messages_native();
        }

        public void ClearConversation()
        {
            if (!IsInitialized) return;
            clearConversation_native();
        }

        // UPDATED: Logic is now safer and more direct.
        public byte[]? ExportSessionState()
        {
            if (!IsInitialized) return null;
            nuint size = getSessionStateSizeBytes_native();
            if (size == 0) return Array.Empty<byte>();

            byte[] buffer = new byte[(int)size];
            nuint bytesWritten = exportSessionState_native(buffer, size);

            if (bytesWritten == size)
            {
                return buffer;
            }

            // Optional: Handle partial writes if that's a possibility in your C++ code.
            if (bytesWritten > 0 && bytesWritten < size)
            {
                Array.Resize(ref buffer, (int)bytesWritten);
                return buffer;
            }

            return null; // An error occurred
        }

        // UPDATED: Now takes both state and token data.
        public bool ImportSessionState(byte[] stateData, int[] tokenData)
        {
            if (!IsInitialized || stateData == null || tokenData == null) return false;
            return importSessionState_native(stateData, (nuint)stateData.Length, tokenData, (nuint)tokenData.Length);
        }


        public int GetTokenHistoryCount()
        {
            if (!IsInitialized) return 0;
            return (int)getTokenHistoryCount_native();
        }

        // UPDATED: Now correctly exports an array of integers (tokens).
        public int[]? ExportTokenHistory()
        {
            if (!IsInitialized) return null;
            nuint count = getTokenHistoryCount_native();
            if (count == 0) return Array.Empty<int>();

            int[] buffer = new int[(int)count];
            nuint tokensExported = exportTokenHistory_native(buffer, count);

            if (tokensExported == count)
            {
                return buffer;
            }
            return null; // Error occurred
        }


        public string GetLastError()
        {
            IntPtr errorPtr = get_last_error_native();
            return Marshal.PtrToStringUTF8(errorPtr) ?? "Unknown error";
        }

        public (int context, int past) GetInfo()
        {
            if (!IsInitialized) return (0, 0);
            get_memory_info_native(out nuint ctx, out nuint past);
            return ((int)ctx, (int)past);
        }

        public void Dispose()
        {
            if (IsInitialized)
            {
                freeLlama();
                IsInitialized = false;
            }
            // Free the GCHandle when the service is disposed
            if (_callbackHandle.IsAllocated)
            {
                _callbackHandle.Free();
            }
            GC.SuppressFinalize(this);
        }

        ~EasyChatService()
        {
            Dispose();
        }
        #endregion
    }
}