namespace Blizztrack.IO
{
    // Types that can be wrapped in cursor-able handlers.
    public interface ICursorable<T, U> : IReadable
        where T : ICursorable<T, U>
        where U : ICursorReadable
    {
        public U WithCursor();
    }
}
