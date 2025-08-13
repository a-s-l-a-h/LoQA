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
        [MarshalAs(UnmanagedType.I1)]
        public bool use_mmap;
        [MarshalAs(UnmanagedType.I1)]
        public bool use_mlock;
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
        [MarshalAs(UnmanagedType.I1)]
        public bool offload_kqv;
        [MarshalAs(UnmanagedType.I1)]
        public bool flash_attn;
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

        Task<bool> GenerateAsync(string prompt, int maxTokens = 4096);

        // This function now returns bool for better error checking.
        bool LoadFullHistory(string historyJson);

        // New function to set the fallback template
        bool SetFallbackChatTemplate(string template);

        void StopGeneration();
        bool UpdateSamplingParams(ChatSamplingParams newParams);
        ChatSamplingParams GetCurrentSamplingParams();
        ChatModelParams GetDefaultModelParams();
        ChatContextParams GetDefaultContextParams();
        ChatSamplingParams GetDefaultSamplingParams();
        bool ClearConversation();
        string GetLastError();
    }
}