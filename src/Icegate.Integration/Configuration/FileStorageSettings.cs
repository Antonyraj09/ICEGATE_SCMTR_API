namespace Icegate.Integration.Configuration;

/// <summary>Configurable, safe file storage locations for SCMTR inbound files, ACKs and ZIP ACKs.</summary>
public class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    public string InboundPath { get; set; } = "Files/ICEGATE/Inbound";

    public string AckPath { get; set; } = "Files/ICEGATE/Acknowledgements";

    public string ZipAckPath { get; set; } = "Files/ICEGATE/ZipAcknowledgements";

    /// <summary>Maximum accepted inbound SCMTR file size, in bytes. Default 10 MB.</summary>
    public long MaxInboundFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Extensions allowed for inbound SCMTR file uploads.</summary>
    public string[] AllowedInboundExtensions { get; set; } = { ".json" };
}
