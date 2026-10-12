SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

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
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE TABLE [AdminAuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [AdminUserId] nvarchar(450) NULL,
        [AdminName] nvarchar(100) NOT NULL,
        [Action] nvarchar(60) NOT NULL,
        [TargetType] nvarchar(40) NOT NULL,
        [TargetId] nvarchar(64) NULL,
        [Details] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AdminAuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [DisplayName] nvarchar(100) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [LastLoginAtUtc] datetime2 NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE TABLE [Customers] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(120) NOT NULL,
        [NormalizedPhone] nvarchar(20) NOT NULL,
        [InvoiceReference] nvarchar(60) NULL,
        [LaptopModel] nvarchar(120) NULL,
        [Notes] nvarchar(1000) NULL,
        [AccessCodeHash] nvarchar(200) NOT NULL,
        [AccessCodeVersion] int NOT NULL,
        [SpinCredits] int NOT NULL,
        [IsActive] bit NOT NULL,
        [FailedLoginCount] int NOT NULL,
        [LockoutEndUtc] datetime2 NULL,
        [LastLoginAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Customers_SpinCredits] CHECK ([SpinCredits] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE TABLE [DataProtectionKeys] (
        [Id] int NOT NULL IDENTITY,
        [FriendlyName] nvarchar(max) NULL,
        [Xml] nvarchar(max) NULL,
        CONSTRAINT [PK_DataProtectionKeys] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE TABLE [PrizeImages] (
        [Id] uniqueidentifier NOT NULL,
        [ContentType] nvarchar(40) NOT NULL,
        [Data] varbinary(max) NOT NULL,
        [ByteLength] int NOT NULL,
        [Sha256] nvarchar(64) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PrizeImages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE TABLE [SiteSettings] (
        [Id] int NOT NULL,
        [SiteTitle] nvarchar(120) NOT NULL,
        [PromoSubheadingEn] nvarchar(300) NOT NULL,
        [PromoSubheadingAr] nvarchar(300) NOT NULL,
        [PickupInstructionsEn] nvarchar(500) NOT NULL,
        [PickupInstructionsAr] nvarchar(500) NOT NULL,
        [AnnouncementEn] nvarchar(300) NULL,
        [AnnouncementAr] nvarchar(300) NULL,
        [PausedMessageEn] nvarchar(300) NOT NULL,
        [PausedMessageAr] nvarchar(300) NOT NULL,
        [DefaultLanguage] nvarchar(5) NOT NULL,
        [SoundEnabledByDefault] bit NOT NULL,
        [SpinDurationMinMs] int NOT NULL,
        [SpinDurationMaxMs] int NOT NULL,
        [AnimationIntensity] nvarchar(20) NOT NULL,
        [CampaignEnabled] bit NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SiteSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SiteSettings_Duration] CHECK ([SpinDurationMinMs] >= 1500 AND [SpinDurationMaxMs] <= 12000 AND [SpinDurationMinMs] <= [SpinDurationMaxMs])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE TABLE [WheelItems] (
        [Id] int NOT NULL IDENTITY,
        [NameEn] nvarchar(80) NOT NULL,
        [NameAr] nvarchar(80) NULL,
        [DescriptionEn] nvarchar(300) NULL,
        [DescriptionAr] nvarchar(300) NULL,
        [Kind] nvarchar(20) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [ProbabilityBasisPoints] int NOT NULL,
        [ImageId] uniqueidentifier NULL,
        [IconKey] nvarchar(40) NULL,
        [ColorKey] nvarchar(20) NOT NULL,
        [StockQuantity] int NULL,
        [LowStockThreshold] int NOT NULL,
        [IsActive] bit NOT NULL,
        [IsArchived] bit NOT NULL,
        [CelebrationTier] nvarchar(20) NOT NULL,
        [CollectionNoteEn] nvarchar(300) NULL,
        [CollectionNoteAr] nvarchar(300) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WheelItems] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_WheelItems_Probability] CHECK ([ProbabilityBasisPoints] >= 0 AND [ProbabilityBasisPoints] <= 10000),
        CONSTRAINT [CK_WheelItems_Stock] CHECK ([StockQuantity] IS NULL OR [StockQuantity] >= 0),
        CONSTRAINT [FK_WheelItems_PrizeImages_ImageId] FOREIGN KEY ([ImageId]) REFERENCES [PrizeImages] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE TABLE [SpinAttempts] (
        [Id] uniqueidentifier NOT NULL,
        [CustomerId] int NOT NULL,
        [WheelItemId] int NOT NULL,
        [RequestId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [ItemNameEn] nvarchar(80) NOT NULL,
        [ItemNameAr] nvarchar(80) NULL,
        [ItemKind] nvarchar(20) NOT NULL,
        [ImageId] uniqueidentifier NULL,
        [IconKey] nvarchar(40) NULL,
        [ColorKey] nvarchar(20) NOT NULL,
        [CelebrationTier] nvarchar(20) NOT NULL,
        [ConfiguredBasisPoints] int NOT NULL,
        [EffectiveWeight] int NOT NULL,
        [EffectiveTotalWeight] int NOT NULL,
        [CreditsBefore] int NOT NULL,
        [CreditsAfter] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_SpinAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SpinAttempts_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SpinAttempts_WheelItems_WheelItemId] FOREIGN KEY ([WheelItemId]) REFERENCES [WheelItems] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE TABLE [Awards] (
        [Id] int NOT NULL IDENTITY,
        [SpinAttemptId] uniqueidentifier NOT NULL,
        [CustomerId] int NOT NULL,
        [WheelItemId] int NOT NULL,
        [PrizeNameEn] nvarchar(80) NOT NULL,
        [PrizeNameAr] nvarchar(80) NULL,
        [ImageId] uniqueidentifier NULL,
        [IconKey] nvarchar(40) NULL,
        [ColorKey] nvarchar(20) NOT NULL,
        [CelebrationTier] nvarchar(20) NOT NULL,
        [CollectionNoteEn] nvarchar(300) NULL,
        [CollectionNoteAr] nvarchar(300) NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [DeliveredAtUtc] datetime2 NULL,
        [DeliveredByAdminId] nvarchar(450) NULL,
        [DeliveredByAdminName] nvarchar(100) NULL,
        [AdminNote] nvarchar(500) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Awards] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Awards_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Awards_SpinAttempts_SpinAttemptId] FOREIGN KEY ([SpinAttemptId]) REFERENCES [SpinAttempts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Awards_WheelItems_WheelItemId] FOREIGN KEY ([WheelItemId]) REFERENCES [WheelItems] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE TABLE [CreditLedgerEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [Delta] int NOT NULL,
        [BalanceAfter] int NOT NULL,
        [Reason] nvarchar(30) NOT NULL,
        [SpinAttemptId] uniqueidentifier NULL,
        [AdminUserId] nvarchar(450) NULL,
        [AdminName] nvarchar(100) NULL,
        [Note] nvarchar(300) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_CreditLedgerEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CreditLedgerEntries_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CreditLedgerEntries_SpinAttempts_SpinAttemptId] FOREIGN KEY ([SpinAttemptId]) REFERENCES [SpinAttempts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AdminAuditLogs_CreatedAtUtc] ON [AdminAuditLogs] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Awards_CustomerId] ON [Awards] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Awards_SpinAttemptId] ON [Awards] ([SpinAttemptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Awards_Status_CreatedAtUtc] ON [Awards] ([Status], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Awards_WheelItemId] ON [Awards] ([WheelItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CreditLedgerEntries_CustomerId_CreatedAtUtc] ON [CreditLedgerEntries] ([CustomerId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CreditLedgerEntries_SpinAttemptId] ON [CreditLedgerEntries] ([SpinAttemptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Customers_CreatedAtUtc] ON [Customers] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Customers_InvoiceReference] ON [Customers] ([InvoiceReference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Customers_NormalizedPhone] ON [Customers] ([NormalizedPhone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SpinAttempts_CreatedAtUtc] ON [SpinAttempts] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SpinAttempts_CustomerId_RequestId] ON [SpinAttempts] ([CustomerId], [RequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SpinAttempts_WheelItemId] ON [SpinAttempts] ([WheelItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WheelItems_ImageId] ON [WheelItems] ([ImageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_WheelItems_SingleActiveTryAgain] ON [WheelItems] ([Kind]) WHERE [Kind] = ''TryAgain'' AND [IsActive] = 1 AND [IsArchived] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012005845_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012005845_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

