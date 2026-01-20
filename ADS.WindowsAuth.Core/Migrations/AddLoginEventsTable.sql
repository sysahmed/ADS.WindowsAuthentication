-- Add LoginEvents table to existing database
-- This script should be run manually on the database

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LoginEvents]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[LoginEvents](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [Username] [nvarchar](255) NOT NULL,
        [Domain] [nvarchar](255) NOT NULL,
        [MachineName] [nvarchar](255) NOT NULL,
        [LoginTime] [datetime2](7) NOT NULL,
        [LoginMethod] [nvarchar](50) NOT NULL,
        [SessionId] [nvarchar](100) NULL,
        [Success] [bit] NOT NULL,
        [IpAddress] [nvarchar](50) NULL,
        [AdUserId] [int] NULL,
        [LogonType] [int] NULL,
     CONSTRAINT [PK_LoginEvents] PRIMARY KEY CLUSTERED 
    (
        [Id] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    ) ON [PRIMARY]

    -- Create indexes
    CREATE NONCLUSTERED INDEX [IX_LoginEvents_Username_MachineName_LoginTime] ON [dbo].[LoginEvents]
    (
        [Username] ASC,
        [MachineName] ASC,
        [LoginTime] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

    CREATE NONCLUSTERED INDEX [IX_LoginEvents_LoginMethod] ON [dbo].[LoginEvents]
    (
        [LoginMethod] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

    PRINT 'LoginEvents table created successfully'
END
ELSE
BEGIN
    PRINT 'LoginEvents table already exists'
END
GO
