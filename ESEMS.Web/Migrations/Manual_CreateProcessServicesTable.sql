-- Manual SQL script to create ProcessServices table
-- This is equivalent to running the AddProcessServiceManyToMany migration

-- Create ProcessServices table
CREATE TABLE [dbo].[ProcessServices] (
    [ProcessId] NVARCHAR(450) NOT NULL,
    [ServiceId] NVARCHAR(450) NOT NULL,
    [Criticality] INT NOT NULL,
    [IsMandatory] BIT NOT NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NOT NULL,
    [CreatedById] NVARCHAR(MAX) NULL,
    [UpdatedById] NVARCHAR(MAX) NULL,
    [IsActive] BIT NOT NULL,
    
    -- Primary Key (Composite)
    CONSTRAINT [PK_ProcessServices] PRIMARY KEY ([ProcessId], [ServiceId]),
    
    -- Foreign Keys
    CONSTRAINT [FK_ProcessServices_Processes_ProcessId] 
        FOREIGN KEY ([ProcessId]) REFERENCES [dbo].[Processes] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProcessServices_Services_ServiceId] 
        FOREIGN KEY ([ServiceId]) REFERENCES [dbo].[Services] ([Id]) ON DELETE CASCADE
);

-- Create Index
CREATE INDEX [IX_ProcessServices_ServiceId] ON [dbo].[ProcessServices] ([ServiceId]);

-- Insert migration history record (so EF Core knows this migration has been applied)
INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260214120000_AddProcessServiceManyToMany', N'8.0.0');

GO

