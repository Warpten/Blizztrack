using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blizztrack.Framework.IO
{
    /// <summary>
    /// A lightweight handle around a file mapped in memory.
    /// <para>
    /// This type should always be used withìn a <c>using</c> statement, despite not inheriting from <see cref="IDisposable"/>
    /// due to it being a stack-allocated type.
    /// </para>
    /// </summary>
    public readonly ref struct MappedMemory
    {
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly unsafe byte* _rawData;

        public readonly Span<byte> Span;

        internal MappedMemory(MemoryMappedFile file, long offset, int length, MemoryMappedFileAccess access = MemoryMappedFileAccess.Read)
            : this(file.CreateViewAccessor(offset, length, access), length)
        {
        }

        public void Read<U>(Index offset, out U value)
        {
            var relativeOffset = offset.GetOffset(Span.Length);

            value = Unsafe.ReadUnaligned<U>(ref Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(Span), relativeOffset));
        }

        /// <inheritdoc cref="MappedMemory"/>
        public unsafe MappedMemory(MemoryMappedViewAccessor accessor, int length)
        {
            _accessor = accessor;

            if (OperatingSystem.IsWindows())
                _rawData = (byte*)_accessor.SafeMemoryMappedViewHandle.DangerousGetHandle().ToPointer();
            else
                _rawData = null;
                _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _rawData);

            if (_rawData == null)
                throw new InvalidOperationException("Failed to retrieve a pointer to file data");

            Span = new(_rawData, length);
        }

        public readonly unsafe void Dispose()
        {
            if (_rawData != null && !OperatingSystem.IsWindows())
                _accessor.SafeMemoryMappedViewHandle.ReleasePointer();

            _accessor.Dispose();
        }
    }
}
