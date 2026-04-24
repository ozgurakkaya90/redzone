IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [AppConfigs] (
    [Key] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_AppConfigs] PRIMARY KEY ([Key])
);
GO

CREATE TABLE [LdapConfigurations] (
    [Id] int NOT NULL IDENTITY,
    [Enabled] bit NOT NULL,
    [Server] nvarchar(max) NULL,
    [Port] int NOT NULL,
    [UseSsl] bit NOT NULL,
    [UseTls] bit NOT NULL,
    [Timeout] int NOT NULL,
    [BindDn] nvarchar(max) NULL,
    [BindPassword] nvarchar(max) NULL,
    [UserSearchBase] nvarchar(max) NULL,
    [UserSearchFilter] nvarchar(max) NOT NULL,
    [AttrDisplayName] nvarchar(max) NOT NULL,
    [AttrEmail] nvarchar(max) NOT NULL,
    [AttrDepartment] nvarchar(max) NOT NULL,
    [AutoCreateUsers] bit NOT NULL,
    [DefaultRole] nvarchar(max) NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_LdapConfigurations] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [RolePermissions] (
    [Id] int NOT NULL IDENTITY,
    [Role] nvarchar(450) NOT NULL,
    [Permission] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Users] (
    [Id] int NOT NULL IDENTITY,
    [Username] nvarchar(450) NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [FullName] nvarchar(max) NOT NULL,
    [Department] nvarchar(max) NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    [AuthType] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [EthicsReports] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(max) NOT NULL,
    [Subject] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [ReportCategory] nvarchar(max) NULL,
    [Status] nvarchar(max) NOT NULL,
    [SubmittedAt] datetime2 NOT NULL,
    [AuditDecision] nvarchar(max) NULL,
    [AuditNotes] nvarchar(max) NULL,
    [AuditReviewedById] int NULL,
    [AuditReviewedAt] datetime2 NULL,
    [EthicsDecision] nvarchar(max) NULL,
    [EthicsNotes] nvarchar(max) NULL,
    [EthicsReviewedById] int NULL,
    [EthicsReviewedAt] datetime2 NULL,
    CONSTRAINT [PK_EthicsReports] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EthicsReports_Users_AuditReviewedById] FOREIGN KEY ([AuditReviewedById]) REFERENCES [Users] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_EthicsReports_Users_EthicsReviewedById] FOREIGN KEY ([EthicsReviewedById]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
);
GO

CREATE TABLE [InternalAudits] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(max) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Scope] nvarchar(max) NULL,
    [Period] nvarchar(max) NOT NULL,
    [AuditType] nvarchar(max) NULL,
    [StartDate] date NULL,
    [EndDate] date NULL,
    [Status] nvarchar(max) NOT NULL,
    [LeadAuditorId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_InternalAudits] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InternalAudits_Users_LeadAuditorId] FOREIGN KEY ([LeadAuditorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [PasswordResetTokens] (
    [Id] int NOT NULL IDENTITY,
    [Token] nvarchar(max) NOT NULL,
    [UserId] int NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [Used] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PasswordResetTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PasswordResetTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Risks] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(max) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [Category] nvarchar(max) NULL,
    [ResponsibleUnit] nvarchar(max) NULL,
    [RiskStrategy] nvarchar(max) NULL,
    [ProposedById] int NULL,
    [ProposerName] nvarchar(max) NULL,
    [ProposedAt] datetime2 NOT NULL,
    [OwnerId] int NULL,
    [Status] nvarchar(max) NOT NULL,
    [RejectionReason] nvarchar(max) NULL,
    CONSTRAINT [PK_Risks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Risks_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_Risks_Users_ProposedById] FOREIGN KEY ([ProposedById]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
);
GO

CREATE TABLE [EthicsAttachments] (
    [Id] int NOT NULL IDENTITY,
    [ReportId] int NOT NULL,
    [OriginalFilename] nvarchar(max) NOT NULL,
    [StoredFilename] nvarchar(max) NOT NULL,
    [FileSize] bigint NOT NULL,
    [UploadedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_EthicsAttachments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EthicsAttachments_EthicsReports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [EthicsReports] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AuditFindings] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(max) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [Category] nvarchar(max) NULL,
    [Severity] nvarchar(max) NULL,
    [AuditPeriod] nvarchar(max) NULL,
    [AuditorId] int NOT NULL,
    [OwnerId] int NULL,
    [InternalAuditId] int NULL,
    [Status] nvarchar(max) NOT NULL,
    [DueDate] date NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ClosedAt] datetime2 NULL,
    CONSTRAINT [PK_AuditFindings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AuditFindings_InternalAudits_InternalAuditId] FOREIGN KEY ([InternalAuditId]) REFERENCES [InternalAudits] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_AuditFindings_Users_AuditorId] FOREIGN KEY ([AuditorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AuditFindings_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
);
GO

CREATE TABLE [ActionPlans] (
    [Id] int NOT NULL IDENTITY,
    [RiskId] int NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Responsible] nvarchar(max) NOT NULL,
    [DueDate] date NULL,
    [Status] nvarchar(max) NOT NULL,
    [CreatedById] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CompletedAt] datetime2 NULL,
    CONSTRAINT [PK_ActionPlans] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ActionPlans_Risks_RiskId] FOREIGN KEY ([RiskId]) REFERENCES [Risks] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ActionPlans_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Controls] (
    [Id] int NOT NULL IDENTITY,
    [RiskId] int NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [ControlType] nvarchar(max) NOT NULL,
    [Effectiveness] nvarchar(max) NULL,
    [Frequency] nvarchar(max) NULL,
    [EnteredById] int NOT NULL,
    [EnteredAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Controls] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Controls_Risks_RiskId] FOREIGN KEY ([RiskId]) REFERENCES [Risks] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Controls_Users_EnteredById] FOREIGN KEY ([EnteredById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Evaluations] (
    [Id] int NOT NULL IDENTITY,
    [RiskId] int NOT NULL,
    [EvalType] nvarchar(max) NOT NULL,
    [Probability] float NOT NULL,
    [Exposure] float NOT NULL,
    [Consequence] float NOT NULL,
    [Score] float NOT NULL,
    [RiskLevel] nvarchar(max) NOT NULL,
    [EvaluatedById] int NOT NULL,
    [EvaluatedAt] datetime2 NOT NULL,
    [Notes] nvarchar(max) NULL,
    CONSTRAINT [PK_Evaluations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Evaluations_Risks_RiskId] FOREIGN KEY ([RiskId]) REFERENCES [Risks] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Evaluations_Users_EvaluatedById] FOREIGN KEY ([EvaluatedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ClosureRequests] (
    [Id] int NOT NULL IDENTITY,
    [FindingId] int NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Evidence] nvarchar(max) NULL,
    [RequestedById] int NOT NULL,
    [RequestedAt] datetime2 NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [ReviewedById] int NULL,
    [ReviewedAt] datetime2 NULL,
    [ReviewNotes] nvarchar(max) NULL,
    CONSTRAINT [PK_ClosureRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ClosureRequests_AuditFindings_FindingId] FOREIGN KEY ([FindingId]) REFERENCES [AuditFindings] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ClosureRequests_Users_RequestedById] FOREIGN KEY ([RequestedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ClosureRequests_Users_ReviewedById] FOREIGN KEY ([ReviewedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_ActionPlans_CreatedById] ON [ActionPlans] ([CreatedById]);
GO

CREATE INDEX [IX_ActionPlans_RiskId] ON [ActionPlans] ([RiskId]);
GO

CREATE INDEX [IX_AuditFindings_AuditorId] ON [AuditFindings] ([AuditorId]);
GO

CREATE INDEX [IX_AuditFindings_InternalAuditId] ON [AuditFindings] ([InternalAuditId]);
GO

CREATE INDEX [IX_AuditFindings_OwnerId] ON [AuditFindings] ([OwnerId]);
GO

CREATE INDEX [IX_ClosureRequests_FindingId] ON [ClosureRequests] ([FindingId]);
GO

CREATE INDEX [IX_ClosureRequests_RequestedById] ON [ClosureRequests] ([RequestedById]);
GO

CREATE INDEX [IX_ClosureRequests_ReviewedById] ON [ClosureRequests] ([ReviewedById]);
GO

CREATE INDEX [IX_Controls_EnteredById] ON [Controls] ([EnteredById]);
GO

CREATE INDEX [IX_Controls_RiskId] ON [Controls] ([RiskId]);
GO

CREATE INDEX [IX_EthicsAttachments_ReportId] ON [EthicsAttachments] ([ReportId]);
GO

CREATE INDEX [IX_EthicsReports_AuditReviewedById] ON [EthicsReports] ([AuditReviewedById]);
GO

CREATE INDEX [IX_EthicsReports_EthicsReviewedById] ON [EthicsReports] ([EthicsReviewedById]);
GO

CREATE INDEX [IX_Evaluations_EvaluatedById] ON [Evaluations] ([EvaluatedById]);
GO

CREATE INDEX [IX_Evaluations_RiskId] ON [Evaluations] ([RiskId]);
GO

CREATE INDEX [IX_InternalAudits_LeadAuditorId] ON [InternalAudits] ([LeadAuditorId]);
GO

CREATE INDEX [IX_PasswordResetTokens_UserId] ON [PasswordResetTokens] ([UserId]);
GO

CREATE INDEX [IX_Risks_OwnerId] ON [Risks] ([OwnerId]);
GO

CREATE INDEX [IX_Risks_ProposedById] ON [Risks] ([ProposedById]);
GO

CREATE UNIQUE INDEX [IX_RolePermissions_Role_Permission] ON [RolePermissions] ([Role], [Permission]);
GO

CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260424182109_InitialCreate', N'8.0.0');
GO

COMMIT;
GO

