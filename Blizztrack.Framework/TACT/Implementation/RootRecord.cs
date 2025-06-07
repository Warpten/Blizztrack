using Blizztrack.Framework.TACT.Structures;

namespace Blizztrack.Framework.TACT.Implementation
{
    public readonly struct RootRecord(MD5 contentKey, ulong nameHash, int fileDataID)
    {
        public readonly MD5 ContentKey = contentKey;
        public readonly ulong NameHash = nameHash;
        public readonly int FileDataID = fileDataID;
    }
}
