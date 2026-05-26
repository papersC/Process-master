-- =============================================
-- Create Roles and User_Roles Tables
-- Run this script to add role management to your existing database
-- =============================================

-- Create Roles Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[roles]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[roles](
        [role_id] [int] IDENTITY(1,1) NOT NULL,
        [role_name] [nvarchar](100) NOT NULL,
        [role_name_ar] [nvarchar](100) NULL,
        [description] [nvarchar](500) NULL,
        [created_by] [int] NOT NULL,
        [created_date] [datetime] NOT NULL,
        [update_by] [int] NOT NULL,
        [update_date] [datetime] NOT NULL,
     CONSTRAINT [PK_roles] PRIMARY KEY CLUSTERED 
    (
        [role_id] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
     CONSTRAINT [IX_roles_role_name] UNIQUE NONCLUSTERED 
    (
        [role_name] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY]
    
    PRINT 'Table [roles] created successfully.'
END
ELSE
BEGIN
    PRINT 'Table [roles] already exists.'
END
GO

-- Create User_Roles Junction Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[user_roles]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[user_roles](
        [user_role_id] [int] IDENTITY(1,1) NOT NULL,
        [user_id] [int] NOT NULL,
        [role_id] [int] NOT NULL,
        [assigned_by] [int] NOT NULL,
        [assigned_date] [datetime] NOT NULL,
     CONSTRAINT [PK_user_roles] PRIMARY KEY CLUSTERED 
    (
        [user_role_id] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY]
    
    PRINT 'Table [user_roles] created successfully.'
END
ELSE
BEGIN
    PRINT 'Table [user_roles] already exists.'
END
GO

-- Add Foreign Keys
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_user_roles_user]') AND parent_object_id = OBJECT_ID(N'[dbo].[user_roles]'))
BEGIN
    ALTER TABLE [dbo].[user_roles]  WITH CHECK ADD  CONSTRAINT [FK_user_roles_user] FOREIGN KEY([user_id])
    REFERENCES [dbo].[user] ([user_id])
    
    ALTER TABLE [dbo].[user_roles] CHECK CONSTRAINT [FK_user_roles_user]
    
    PRINT 'Foreign key [FK_user_roles_user] created successfully.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_user_roles_roles]') AND parent_object_id = OBJECT_ID(N'[dbo].[user_roles]'))
BEGIN
    ALTER TABLE [dbo].[user_roles]  WITH CHECK ADD  CONSTRAINT [FK_user_roles_roles] FOREIGN KEY([role_id])
    REFERENCES [dbo].[roles] ([role_id])
    
    ALTER TABLE [dbo].[user_roles] CHECK CONSTRAINT [FK_user_roles_roles]
    
    PRINT 'Foreign key [FK_user_roles_roles] created successfully.'
END
GO

-- Insert Default Roles
IF NOT EXISTS (SELECT * FROM [dbo].[roles] WHERE [role_name] = 'Admin')
BEGIN
    INSERT INTO [dbo].[roles] ([role_name], [role_name_ar], [description], [created_by], [created_date], [update_by], [update_date])
    VALUES 
        ('Admin', 'مدير النظام', 'Full system administrator with all permissions', 1, GETDATE(), 1, GETDATE()),
        ('Editor', 'محرر', 'Can create and edit content', 1, GETDATE(), 1, GETDATE()),
        ('Approver', 'معتمد', 'Can approve and reject submissions', 1, GETDATE(), 1, GETDATE()),
        ('Viewer', 'مشاهد', 'Read-only access to the system', 1, GETDATE(), 1, GETDATE())
    
    PRINT 'Default roles inserted successfully.'
END
ELSE
BEGIN
    PRINT 'Default roles already exist.'
END
GO

PRINT 'Role management tables setup completed successfully!'
GO

