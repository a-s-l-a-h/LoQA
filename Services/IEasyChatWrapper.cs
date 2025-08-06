// LoQA/Services/IEasyChatWrapper.cs

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace LoQA.Services
{
    #region P/Invoke Structs
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ChatModelParams
    {
        public int n_gpu_layers;
        public int main_gpu;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public float[] tensor_split;
        public byte use_mmap;
        public byte use_mlock;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ChatContextParams
    {
        public int n_ctx;
        public int n_batch;
        public int n_threads;
        public int n_threads_batch;
        public float rope_freq_base;
        public float rope_freq_scale;
        public byte offload_kqv;
        public byte flash_attn;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ChatSamplingParams
    {
        public float temperature;
        public float min_p;
        public int seed; // Use -1 for random
    }
    #endregion

    public interface IEasyChatWrapper : IDisposable
    {
        event Action<string>? OnTokenReceived;
        bool IsInitialized { get; }
        Task<bool> InitializeAsync(string modelPath, ChatModelParams modelParams, ChatContextParams ctxParams);

        // The prompt is passed directly to the incremental generation function.
        Task GenerateAsync(string prompt, int maxTokens = 4096);

        // New function to prime the KV cache with past messages.
        // The C++ function is now `prime_kv_cache`.
        bool PrimeKvCache(string role, string content);

        void StopGeneration();
        bool UpdateSamplingParams(ChatSamplingParams newParams);
        ChatSamplingParams GetCurrentSamplingParams();
        ChatModelParams GetDefaultModelParams();
        ChatContextParams GetDefaultContextParams();
        ChatSamplingParams GetDefaultSamplingParams();
        void ClearConversation();
        string GetLastError();
    }
}