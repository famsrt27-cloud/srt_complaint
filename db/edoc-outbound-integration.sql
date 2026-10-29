BEGIN TRANSACTION;
CREATE TABLE [dbo].[EdocDocumentLinks] (
    [Id] int NOT NULL IDENTITY,
    [ComplaintId] int NOT NULL,
    [CreatedById] int NOT NULL,
    [WID] nvarchar(100) NOT NULL,
    [BasketId] nvarchar(13) NOT NULL,
    [BasketName] nvarchar(200) NULL,
    [RegisterNumber] nvarchar(20) NULL,
    [RemoteDocumentDate] nvarchar(50) NULL,
    [SyncStatus] nvarchar(40) NOT NULL,
    [RemoteStatusCode] nvarchar(20) NULL,
    [RemoteStatusDescription] nvarchar(2000) NULL,
    [AttemptCount] int NOT NULL,
    [LastAttemptAt] datetime2 NULL,
    [SentAt] datetime2 NULL,
    [LastTrackedAt] datetime2 NULL,
    [NextTrackingAt] datetime2 NULL,
    [LeaseExpiresAt] datetime2 NULL,
    [LastError] nvarchar(2000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_EdocDocumentLinks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EdocDocumentLinks_Complaints_ComplaintId] FOREIGN KEY ([ComplaintId]) REFERENCES [dbo].[Complaints] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EdocDocumentLinks_StaffUsers_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [dbo].[StaffUsers] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [dbo].[EdocAttachmentDeliveries] (
    [Id] int NOT NULL IDENTITY,
    [EdocDocumentLinkId] int NOT NULL,
    [ComplaintAttachmentId] int NOT NULL,
    [Status] nvarchar(30) NOT NULL,
    [AttemptCount] int NOT NULL,
    [LastAttemptAt] datetime2 NULL,
    [UploadedAt] datetime2 NULL,
    [LastError] nvarchar(2000) NULL,
    CONSTRAINT [PK_EdocAttachmentDeliveries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EdocAttachmentDeliveries_ComplaintAttachments_ComplaintAttachmentId] FOREIGN KEY ([ComplaintAttachmentId]) REFERENCES [dbo].[ComplaintAttachments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EdocAttachmentDeliveries_EdocDocumentLinks_EdocDocumentLinkId] FOREIGN KEY ([EdocDocumentLinkId]) REFERENCES [dbo].[EdocDocumentLinks] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_EdocAttachmentDeliveries_ComplaintAttachmentId] ON [dbo].[EdocAttachmentDeliveries] ([ComplaintAttachmentId]);

CREATE UNIQUE INDEX [IX_EdocAttachmentDeliveries_EdocDocumentLinkId_ComplaintAttachmentId] ON [dbo].[EdocAttachmentDeliveries] ([EdocDocumentLinkId], [ComplaintAttachmentId]);

CREATE UNIQUE INDEX [IX_EdocDocumentLinks_ComplaintId] ON [dbo].[EdocDocumentLinks] ([ComplaintId]);

CREATE INDEX [IX_EdocDocumentLinks_CreatedById] ON [dbo].[EdocDocumentLinks] ([CreatedById]);

CREATE INDEX [IX_EdocDocumentLinks_SyncStatus_NextTrackingAt] ON [dbo].[EdocDocumentLinks] ([SyncStatus], [NextTrackingAt]);

CREATE UNIQUE INDEX [IX_EdocDocumentLinks_WID] ON [dbo].[EdocDocumentLinks] ([WID]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261027084737_AddEdocOutboundIntegration', N'9.0.22');

COMMIT;
GO

