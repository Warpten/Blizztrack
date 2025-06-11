using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.Framework
{
    public unsafe partial class Compression
    {
        // Return codes for the compression/decompression routines. Negative values are errors;
        // positive values are used for special but normal events.
        public const int Z_OK = 0;
        public const int Z_STREAM_END = 1;
        public const int Z_NEED_DICT = 2;
        public const int Z_ERRNO = -1;
        public const int Z_STREAM_ERROR = -2;
        public const int Z_DATA_ERROR = -3;
        public const int Z_MEM_ERROR = -4;
        public const int Z_BUF_ERROR = -5;
        public const int Z_VERSION_ERROR = -6;

        // Allowed flush values
        public const int Z_NO_FLUSH = 0;
        public const int Z_PARTIAL_FLUSH = 1;
        public const int Z_SYNC_FLUSH = 2;
        public const int Z_FULL_FLUSH = 3;
        public const int Z_FINISH = 4;
        public const int Z_BLOCK = 5;
        public const int Z_TREES = 6;

        // Compression strategies
        public const int Z_FILTERED = 1;
        public const int Z_HUFFMAN_ONLY = 2;
        public const int Z_RLE = 3;
        public const int Z_FIXED = 4;
        public const int Z_DEFAULT_STRATEGY = 0;
        
        // Compression levels
        public const int Z_NO_COMPRESSION = 0;
        public const int Z_BEST_SPEED = 1;
        public const int Z_BEST_PERFORMANCE = 9;
        public const int Z_DEFAULT_COMPRESSION = -1;

        public const int Z_DEFLATED = 8;

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

        [LibraryImport(Constants.LibraryName, EntryPoint = "CompressionNative_DeflateInit2_")]
        private static partial int InitializeDeflate(ref Stream stream, int level, int method, int windowBits, int memLevel, int strategy);
        
        [LibraryImport(Constants.LibraryName, EntryPoint = "CompressionNative_Deflate")]
        private static partial int Deflate(ref Stream stream, int flushCode);

        [LibraryImport(Constants.LibraryName, EntryPoint = "CompressionNative_DeflateEnd")]
        private static partial int DeflateEnd(ref Stream stream);

        public static readonly Compression Instance = new Compression();
#else
        static Compression()
        {
            static nint getNativeHandle()
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return NativeLibrary.Load("System.IO.Compression.Native.dll");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return NativeLibrary.Load("libSystem.IO.Compression.Native.dylib");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return NativeLibrary.Load("libSystem.IO.Compression.Native.so");

                return nint.Zero;
            }
            ;

            var nativeHandle = getNativeHandle();
            Instance = new Compression(functionName => NativeLibrary.GetExport(nativeHandle, functionName));
        }

        private readonly delegate*<ref Stream, int, int> InitializeInflate;
        private readonly delegate*<ref Stream, int, int> Inflate;
        private readonly delegate*<ref Stream, int> InflateEnd;

        private readonly delegate*<ref Stream, int, int, int, int, int, int> InitializeDeflate;
        private readonly delegate*<ref Stream, int, int> Deflate;
        private readonly delegate*<ref Stream, int> DeflateEnd;

        Compression(Func<string, nint> loader)
        {
            InitializeInflate = (delegate*<ref Stream, int, int>)loader("CompressionNative_InflateInit2_").ToPointer();
            Inflate = (delegate*<ref Stream, int, int>)loader("CompressionNative_Inflate").ToPointer();
            InflateEnd = (delegate*<ref Stream, int>)loader("CompressionNative_InflateEnd").ToPointer();

            InitializeDeflate = (delegate*<ref Stream, int, int, int, int, int, int>)loader("CompressionNative_DeflateInit2_").ToPointer();
            Deflate = (delegate*<ref Stream, int, int>)loader("CompressionNative_Deflate").ToPointer();
            DeflateEnd = (delegate*<ref Stream, int>)loader("CompressionNative_DeflateEnd").ToPointer();
        }

        public static Compression Instance { get; }
#endif

        public bool Compress(ReadOnlySpan<byte> input, Span<byte> output, int level, int windowBits)
        {
            const int Z_OK = 0;
            const int Z_NO_FLUSH = 0;
            const int Z_STREAM_END = 1;

            Stream stream = new(input, output);
            var returnCode = InitializeDeflate(ref stream,
                level,
                Z_DEFLATED,
                windowBits,
                8 /* DEF_MEM_LEVEL */,
                Z_DEFAULT_STRATEGY);

            if (returnCode != Z_OK)
                return false;

            while (stream.AvailableIn != 0 && returnCode != Z_STREAM_END)
            {
                returnCode = Deflate(ref stream, Z_NO_FLUSH);
                if (returnCode < 0)
                {
                    returnCode = DeflateEnd(ref stream);
                    return false;
                }
            }

            returnCode = DeflateEnd(ref stream);
            return returnCode == Z_OK;
        }

        public bool Decompress(ReadOnlySpan<byte> input, Span<byte> output, int windowBits = 15)
        {
            Stream stream = new (input, output);
            var returnCode = InitializeInflate(ref stream, windowBits);
            
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
        }
    }
}
