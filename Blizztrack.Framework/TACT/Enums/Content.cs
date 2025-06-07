namespace Blizztrack.Framework.TACT.Enums
{
    [Flags]
    public enum Content : uint
    {
        LoadOnWindows = 0x00000008,
        LoadOnMacOS   = 0x00000010,
        LowViolence   = 0x00000080,
        DoNotLoad     = 0x00000100,
        UpdatePlugin  = 0x00000800,
        Encrypted     = 0x08000000,
        NoNames       = 0x10000000,
        UncommonRes   = 0x20000000,
        Bundle        = 0x40000000,
        NoCompression = 0x80000000,
    }
}
