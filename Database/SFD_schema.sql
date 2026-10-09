-- Tables for the Leader Dashboard (SFD). Run once on the database that DBHelper's "NotificationDb" connection points to.
-- Requires SQL Server 2016+ (the repositories use OPENJSON for bulk inserts).
-- OwnerId is the SoloFleet UserId of the leader; there is no local users table.

IF OBJECT_ID('dbo.SFD_Members', 'U') IS NULL
CREATE TABLE dbo.SFD_Members (
    Id         uniqueidentifier NOT NULL CONSTRAINT PK_SFD_Members PRIMARY KEY,
    OwnerId    nvarchar(64)     NOT NULL,
    Name       nvarchar(100)    NOT NULL,
    Area       nvarchar(100)    NOT NULL,
    Color      nvarchar(20)     NOT NULL,
    Phone      nvarchar(30)     NULL,
    CreatedUtc datetime2        NOT NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SFD_Members_OwnerId')
    CREATE INDEX IX_SFD_Members_OwnerId ON dbo.SFD_Members (OwnerId);

IF OBJECT_ID('dbo.SFD_GpsPoints', 'U') IS NULL
CREATE TABLE dbo.SFD_GpsPoints (
    Id            bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SFD_GpsPoints PRIMARY KEY,
    OwnerId       nvarchar(64)     NOT NULL,
    MemberId      uniqueidentifier NOT NULL CONSTRAINT FK_SFD_GpsPoints_Members REFERENCES dbo.SFD_Members (Id) ON DELETE CASCADE,
    Latitude      float            NOT NULL,
    Longitude     float            NOT NULL,
    Speed         float            NOT NULL,
    Bearing       float            NOT NULL,
    Accuracy      float            NOT NULL,
    RecordedAtUtc datetime2        NOT NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SFD_GpsPoints_Owner_Time')
    CREATE INDEX IX_SFD_GpsPoints_Owner_Time ON dbo.SFD_GpsPoints (OwnerId, RecordedAtUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SFD_GpsPoints_MemberId')
    CREATE INDEX IX_SFD_GpsPoints_MemberId ON dbo.SFD_GpsPoints (MemberId);

IF OBJECT_ID('dbo.SFD_Routes', 'U') IS NULL
CREATE TABLE dbo.SFD_Routes (
    Id              uniqueidentifier NOT NULL CONSTRAINT PK_SFD_Routes PRIMARY KEY,
    OwnerId         nvarchar(64)     NOT NULL,
    Name            nvarchar(100)    NOT NULL,
    Area            nvarchar(100)    NOT NULL,
    CoordinatesJson nvarchar(max)    NOT NULL,
    CreatedUtc      datetime2        NOT NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SFD_Routes_OwnerId')
    CREATE INDEX IX_SFD_Routes_OwnerId ON dbo.SFD_Routes (OwnerId);
