using System;

namespace Wagenheimer.CloudSave
{
    public enum CloudSyncResult
    {
        NoCloudSave,
        LocalNewer,
        CloudApplied,
        UserChoseLocal,
        Offline,
        Error
    }

    public enum CloudConflictChoice
    {
        UseCloud,
        UseLocal
    }

    public enum CloudConflictReason
    {
        CloudIsNewer,
        AccountSwitched
    }

    public class CloudConflictData
    {
        public long LocalTimestamp { get; }
        public long CloudTimestamp { get; }
        public byte[] CloudBytes   { get; }
        public CloudConflictReason Reason { get; }

        internal CloudConflictData(long localTs, long cloudTs, byte[] cloudBytes,
            CloudConflictReason reason = CloudConflictReason.CloudIsNewer)
        {
            LocalTimestamp = localTs;
            CloudTimestamp = cloudTs;
            CloudBytes     = cloudBytes;
            Reason         = reason;
        }
    }
}
