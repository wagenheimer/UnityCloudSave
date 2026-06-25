namespace Wagenheimer.CloudSave
{
    public enum CloudLinkStatus
    {
        Linked,
        SignedInExisting,
        AlreadyLinked,
        Failed
    }

    public class CloudLinkResult
    {
        public CloudLinkStatus Status  { get; }
        public string          Message { get; }

        public bool IsSuccess =>
            Status == CloudLinkStatus.Linked || Status == CloudLinkStatus.SignedInExisting;

        CloudLinkResult(CloudLinkStatus status, string message = null)
        {
            Status  = status;
            Message = message;
        }

        internal static CloudLinkResult Ok(CloudLinkStatus status)      => new CloudLinkResult(status);
        internal static CloudLinkResult Fail(string message)            => new CloudLinkResult(CloudLinkStatus.Failed, message);
        internal static CloudLinkResult AlreadyLinkedResult(string msg) => new CloudLinkResult(CloudLinkStatus.AlreadyLinked, msg);
    }
}
