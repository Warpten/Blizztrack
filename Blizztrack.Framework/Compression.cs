using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.Framework
{
    public unsafe partial class Compression
    {
#if NATIVE_AOT
        private interface Constants {
#if PLATFORM_WINDOWS
            const string LibraryName = "System.IO.Compression.Native.dll";
#elif PLATFORM_LINUX
            const string LibraryName = "libSystem.IO.Compression.Native.so";
#elif PLATFORM_OSX
            const string LibraryName = "libSystem.IO.Compression.Native.dylib";
#endif
        }

        [LibraryImport(Constants.LibraryName, EntryPoint = "CompressionNative_InflateInit2_")]
        private static partial int InitializeInflate(ref Stream stream, int flags);

        [LibraryImport(Constants.LibraryName, EntryPoint = "CompressionNative_Inflate")]
        private static partial int Inflate(ref Stream stream, int flushCode);

        [LibraryImport(Constants.LibraryName, EntryPoint = "CompressionNative_InflateEnd")]
        private static partial int InflateEnd(ref Stream stream);

        public static readonly Compression Instance = new Compression();
#else
        private readonly delegate*<ref Stream, int, int> InitializeInflate;
        private readonly delegate*<ref Stream, int, int> Inflate;
        private readonly delegate*<ref Stream, int> InflateEnd;

        Compression(Func<string, nint> loader)
        {
            InitializeInflate = (delegate*<ref Stream, int, int>)loader("CompressionNative_InflateInit2_").ToPointer();
            Inflate = (delegate*<ref Stream, int, int>)loader("CompressionNative_Inflate").ToPointer();
            InflateEnd = (delegate*<ref Stream, int>)loader("CompressionNative_InflateEnd").ToPointer();
        }

        public static Compression Instance => InstanceSupplier.Value;

        private static readonly Lazy<Compression> InstanceSupplier = new (() =>
        {
            static nint getNativeHandle() {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return NativeLibrary.Load("System.IO.Compression.Native.dll");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return NativeLibrary.Load("libSystem.IO.Compression.Native.dylib");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return NativeLibrary.Load("libSystem.IO.Compression.Native.so");

                return nint.Zero;
            };

            var nativeHandle = getNativeHandle();

            return new Compression(functionName => NativeLibrary.GetExport(nativeHandle, functionName));
        });
#endif

        public bool Execute(ReadOnlySpan<byte> input, Span<byte> output)
        {
            const int Z_OK = 0;
            const int Z_NO_FLUSH = 0;
            const int Z_STREAM_END = 1;

            Stream stream = new (input, output);
            var returnCode = InitializeInflate(ref stream, 15);
            
            if (returnCode != Z_OK)
                return false;

            while (stream.AvailableIn != 0 && returnCode != Z_STREAM_END)
            {
                returnCode = Inflate(ref stream, Z_NO_FLUSH);
                if (returnCode < 0)
                {
                    returnCode = InflateEnd(ref stream);
                    return false;
                }
            }

            returnCode = InflateEnd(ref stream);
            return returnCode == Z_OK;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private ref struct Stream(ReadOnlySpan<byte> input, Span<byte> output)
        {
            private byte* NextIn = (byte*) Unsafe.AsPointer(ref MemoryMarshal.GetReference(input));
            private byte* NextOut = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(output));
            private nint Msg;
            private nint InternalState;
            public uint AvailableIn = (uint) input.Length;
            private uint AvailableOut = (uint) output.Length;
            
            public void SetInput(ReadOnlySpan<byte> input)
            {
                NextIn = (byte*) Unsafe.AsPointer(ref MemoryMarshal.GetReference(input));
                AvailableIn = (uint) input.Length;
            }

            public void SetOutput(Span<byte> output)
            {
                NextOut = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(output));
                AvailableOut = (uint) output.Length;
            }
        }
    }
}
