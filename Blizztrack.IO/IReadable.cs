using Blizztrack.IO.Extensions;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.IO
{
    public interface IReadable
    {
        public U ReadBE<U>(nint byteOffset) where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(byteOffset, BitConverter.IsLittleEndian);
        public U ReadLE<U>(nint byteOffset) where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(byteOffset, !BitConverter.IsLittleEndian);

        public U ReadNative<U>(nint byteOffset) where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(byteOffset, false);

        public ReadOnlySpan<U> ReadBE<U>(nint byteOffset, int count) where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(byteOffset, count, BitConverter.IsLittleEndian);
        public ReadOnlySpan<U> ReadLE<U>(nint byteOffset, int count) where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(byteOffset, count, BitConverter.IsLittleEndian);
        public ReadOnlySpan<U> ReadNativE<U>(nint byteOffset, int count) where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(byteOffset, count, BitConverter.IsLittleEndian);

        protected U ReadCore<U>(nint byteOffset, bool reverseEndianness) where U : unmanaged, IBinaryInteger<U>;
        protected ReadOnlySpan<U> ReadCore<U>(nint byteOffset, int count, bool reverseEndianness) where U : unmanaged, IBinaryInteger<U>;
    }
}
