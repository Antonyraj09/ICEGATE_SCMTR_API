namespace IcegateLegacyClient.Models
{
    /// <summary>
    /// Mirrors the IcegateApiResult&lt;T&gt; envelope returned by every endpoint on the
    /// .NET 8 Icegate.Integration API. Generic types are deserialized separately per
    /// endpoint (see UploadApiResult / AckApiResult below) because JavaScriptSerializer
    /// on .NET Framework 4.0 does not deserialize open generic types directly.
    /// </summary>
    public class IcegateApiResultBase
    {
        public bool success { get; set; }
        public int statusCode { get; set; }
        public string message { get; set; }
        public string errorMessage { get; set; }
        public string correlationId { get; set; }
    }

    public class UploadApiResult : IcegateApiResultBase
    {
        public UploadResultData data { get; set; }
    }

    public class AckApiResult : IcegateApiResultBase
    {
        public AckResultData data { get; set; }
    }
}
