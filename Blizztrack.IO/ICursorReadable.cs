using System.Numerics;

namespace Blizztrack.IO
{
    public interface ICursorReadable : IReadable
    {
        public U ReadBE<U>() where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(BitConverter.IsLittleEndian);

        public U ReadLE<U>() where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(!BitConverter.IsLittleEndian);

        public U ReadNative<U>() where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(false);

        public ReadOnlySpan<U> ReadBE<U>(int count) where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(count, BitConverter.IsLittleEndian);
        public ReadOnlySpan<U> ReadLE<U>(int count) where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(count, BitConverter.IsLittleEndian);
        public ReadOnlySpan<U> ReadNativE<U>(int count) where U : unmanaged, IBinaryInteger<U> => ReadCore<U>(count, BitConverter.IsLittleEndian);


        protected U ReadCore<U>(bool reverseEndianness) where U : unmanaged, IBinaryInteger<U>;
        protected ReadOnlySpan<U> ReadCore<U>(int count, bool reverseEndianness) where U : unmanaged, IBinaryInteger<U>;
    }
}
