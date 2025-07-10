using Blizztrack.Framework.TACT.Resources;
using Blizztrack.Options;
using Blizztrack.Shared.IO;

using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using ZiggyCreatures.Caching.Fusion;

namespace Blizztrack.Services
{
    public class MemoryMappedResourceCache(IServiceProvider serviceProvider)
    {
        private IOptionsMonitor<Settings> _settings = serviceProvider.GetRequiredService<IOptionsMonitor<Settings>>();
        private IResourceLocator _resourceLocator = serviceProvider.GetRequiredService<IResourceLocator>();

        private FusionCache _resourceCache = new(new FusionCacheOptions() {
            CacheName = $"cache:mmio",
        });

        public MappedDataSource Obtain(ResourceHandle resourceHandle, TimeSpan lifetime)
        {
            return _resourceCache.GetOrSet($"{resourceHandle.Name}/{resourceHandle.Offset}:{resourceHandle.Length}",
                        _ => resourceHandle.ToMappedDataSource(),
                        new FusionCacheEntryOptions(lifetime), default);
        }
    }

    public class LazyMappedDataSource(ResourceHandle resourceHandle) : IDataSource
    {
        private readonly ResourceHandle _resourceHandle = resourceHandle;

        // private record struct Key(long Offset, int Length);
        // private readonly Dictionary<Key, (MemoryMappedViewAccessor Accessor, nint Pointer)> _mappedBlocks = [];

        private MemoryMappedFile? _memoryMappedFile = null;
        private MemoryMappedViewAccessor? _accessor = null;
        private unsafe byte* _fileData = null;

        public ReadOnlySpan<byte> this[Range range]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var (offset, length) = range.GetOffsetAndLength(_resourceHandle.Length);
                return MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref MemoryBlock, offset), length);
            }
        }

        public byte this[Index index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.AddByteOffset(ref MemoryBlock, index.GetOffset(_resourceHandle.Length));
        }

        public byte this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.AddByteOffset(ref MemoryBlock, index);
        }

        public unsafe void Dispose()
        {
            if (_fileData == null)
                return;

            _accessor!.SafeMemoryMappedViewHandle.ReleasePointer();
            _accessor.Dispose();
            _memoryMappedFile!.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> Slice(int offset, int length)
            => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AddByteOffset(ref MemoryBlock, offset), length);

        private unsafe ref byte MemoryBlock
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_fileData == null)
                {
                    _memoryMappedFile = MemoryMappedFile.CreateFromFile(_resourceHandle.Path, FileMode.Open, null, _resourceHandle.Length, MemoryMappedFileAccess.Read);
                    _accessor = _memoryMappedFile.CreateViewAccessor(_resourceHandle.Offset, _resourceHandle.Length);

                    try
                    {
                        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _fileData);
                    }
                    catch (InvalidOperationException _)
                    {
                        return ref Unsafe.NullRef<byte>();
                    }
                }

                return ref Unsafe.AsRef<byte>(_fileData);
            }
        }
    }
}
