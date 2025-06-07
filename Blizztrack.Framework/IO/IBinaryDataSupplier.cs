using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Blizztrack.Framework.IO
{
    /// <summary>
    /// This interface is used to access binary data to be interpreted by various TACT file format implementations.
    /// </summary>
    public interface IBinaryDataSupplier
    {
        public ReadOnlySpan<byte> this[Range range] { get; }
        public byte this[int offset] { get; }
        public byte this[Index index] { get; }

        public int Length { get; }
        public ReadOnlySpan<byte> Slice(int offset, int count) => this[offset..(offset + count)];
    }
}

