using Blizztrack.Framework.TACT.Structures;

namespace Blizztrack.Framework.TACT.Implementation
{
    public readonly struct RootRecord(ContentKey contentKey, ulong nameHash, int fileDataID)
    {
        public readonly ContentKey ContentKey = contentKey;
        public readonly ulong NameHash = nameHash;
        public readonly int FileDataID = fileDataID;
    }
}
