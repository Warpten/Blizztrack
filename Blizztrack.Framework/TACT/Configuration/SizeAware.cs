namespace Blizztrack.Framework.TACT.Configuration
{
    public readonly record struct SizeAware<T>(T Key, long Size);
}
