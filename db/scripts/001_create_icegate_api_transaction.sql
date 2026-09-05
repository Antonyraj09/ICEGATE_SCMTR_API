/*
 ICEGATE_API_TRANSACTION
 -------------------------------------------------------------------------
 Transaction log for all ICEGATE SCMTR Open API interactions performed by
 the Icegate.Integration .NET 8 service.

 IMPORTANT: This table must NEVER store secrets - API Key, Password, JWT,
 Access Token, or Encrypted Authentication Credentials. Only transport /
 business metadata is persisted here.

 Target: Microsoft SQL Server 2016+. Adjust types if targeting another RDBMS.
 -------------------------------------------------------------------------
*/

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ICEGATE_API_TRANSACTION')
BEGIN
    CREATE TABLE dbo.ICEGATE_API_TRANSACTION
    (
        Id                   BIGINT IDENTITY(1,1) NOT NULL,
        ClientId             NVARCHAR(100)   NOT NULL,  -- onboarded client (own ICEGATE identity) - see docs/Multi_Client_Onboarding.md
        LocalReferenceNo     NVARCHAR(100)   NULL,
        FileName             NVARCHAR(500)   NULL,
        FileHash             NVARCHAR(128)   NULL,
        ApiType              NVARCHAR(30)    NOT NULL,   -- AUTHENTICATION | INBOUND_UPLOAD | GET_ACK | GET_ZIP_ACK
        Environment          NVARCHAR(10)    NOT NULL,   -- UAT | PROD
        RequestDateTime      DATETIME2       NOT NULL,
        ResponseDateTime     DATETIME2       NULL,
        IcegateUniqueId      NVARCHAR(100)   NULL,
        MessageId            NVARCHAR(100)   NULL,
        SenderId             NVARCHAR(100)   NULL,
        IcegateId            NVARCHAR(100)   NULL,
        CustodianCode        NVARCHAR(100)   NULL,
        Status               NVARCHAR(30)    NOT NULL,   -- see application TransactionStatus enum
        ValidationStatus     NVARCHAR(50)    NULL,
        ResponseDescription  NVARCHAR(1000)  NULL,
        ErrorMessage         NVARCHAR(2000)  NULL,
        AckFileName          NVARCHAR(500)   NULL,
        AckFilePath          NVARCHAR(1000)  NULL,
        ZipFileName          NVARCHAR(500)   NULL,
        ZipFilePath          NVARCHAR(1000)  NULL,
        CorrelationId        NVARCHAR(50)    NOT NULL,
        CreatedDate          DATETIME2       NOT NULL CONSTRAINT DF_ICEGATE_API_TRANSACTION_CreatedDate DEFAULT (SYSUTCDATETIME()),
        UpdatedDate          DATETIME2       NULL,

        CONSTRAINT PK_ICEGATE_API_TRANSACTION PRIMARY KEY CLUSTERED (Id ASC)
    );

    CREATE NONCLUSTERED INDEX IX_ICEGATE_API_TRANSACTION_ClientId
        ON dbo.ICEGATE_API_TRANSACTION (ClientId);

    CREATE NONCLUSTERED INDEX IX_ICEGATE_API_TRANSACTION_Client_UniqueId
        ON dbo.ICEGATE_API_TRANSACTION (ClientId, IcegateUniqueId);

    CREATE NONCLUSTERED INDEX IX_ICEGATE_API_TRANSACTION_UniqueId
        ON dbo.ICEGATE_API_TRANSACTION (IcegateUniqueId);

    CREATE NONCLUSTERED INDEX IX_ICEGATE_API_TRANSACTION_CorrelationId
        ON dbo.ICEGATE_API_TRANSACTION (CorrelationId);

    CREATE NONCLUSTERED INDEX IX_ICEGATE_API_TRANSACTION_LocalReferenceNo
        ON dbo.ICEGATE_API_TRANSACTION (LocalReferenceNo);

    CREATE NONCLUSTERED INDEX IX_ICEGATE_API_TRANSACTION_Sender_Message
        ON dbo.ICEGATE_API_TRANSACTION (SenderId, MessageId);
END
GO
