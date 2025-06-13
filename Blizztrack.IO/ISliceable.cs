namespace Blizztrack.IO
{
    // An interface for self-sliceable types.
    public interface ISliceable<T, U>
        where T : ISliceable<T, U>, allows ref struct
        where U : ISliceable<U, U>, allows ref struct
    {
        public U this[Range range] { get; }

        public U Slice(int offset, int length);
    }

    public interface ISliceable<T> : ISliceable<T, T> where T : ISliceable<T, T>, allows ref struct;
}
