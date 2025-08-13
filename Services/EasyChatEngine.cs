using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace LoQA.Services
{
    public class EasyChatEngine : IDisposable
    {
#if WINDOWS
            private const string DllName = "easychatengine.dll";
#elif ANDROID
            // FIX: Ensure this points to your new library name for Android
            private const string DllName = "libeasychatengine.so";
#elif IOS || MACCATALYST
        private const string DllName = "__Internal"; // For static linking
#else
            private const string DllName = "easychatengine";
#endif

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int easyChatEngineInvoke([MarshalAs(UnmanagedType.LPStr)] string request);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void easyChatEngineGetLastResult(StringBuilder buffer, int buffer_size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void TokenCallback(string token);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void registerTokenCallback(TokenCallback callback);

        private readonly TokenCallback _managedCallback;

        public EasyChatEngine()
        {
            _managedCallback = (token) => OnTokenReceived?.Invoke(token);
            registerTokenCallback(_managedCallback);
        }

        public event Action<string>? OnTokenReceived;

        public Dictionary<string, JsonElement> InvokeCommand(string command)
        {
            try
            {
                int size = easyChatEngineInvoke(command);
                if (size <= 0)
                {
                    return new Dictionary<string, JsonElement> {
                        { "status", JsonSerializer.SerializeToElement("ERROR") },
                        { "message", JsonSerializer.SerializeToElement($"Invoke failed with code: {size}") }
                    };
                }

                StringBuilder buffer = new StringBuilder(size + 1);
                easyChatEngineGetLastResult(buffer, buffer.Capacity);
                string jsonResponse = buffer.ToString();

                var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonResponse);
                return result ?? new Dictionary<string, JsonElement>();
            }
            catch (Exception ex)
            {
                return new Dictionary<string, JsonElement> {
                    { "status", JsonSerializer.SerializeToElement("ERROR") },
                    { "message", JsonSerializer.SerializeToElement($"C# wrapper exception: {ex.Message}") }
                };
            }
        }

        public void Dispose()
        {
            InvokeCommand("command=free");
            GC.SuppressFinalize(this);
        }
    }
}