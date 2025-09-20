using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestDapp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeNotificationsIdToInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert Notifications.ID from bigint to int by recreating the table and preserving IDs
            migrationBuilder.Sql(@"
IF EXISTS(
    SELECT 1 FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'dbo.Notifications') AND name = 'ID' AND system_type_id = 127
)
BEGIN
    -- Create new table with ID as int identity and unique constraint/index names to avoid collisions
    CREATE TABLE dbo.Notifications_new (
        ID int IDENTITY(1,1) NOT NULL,
        UserID int NOT NULL,
        [Type] nvarchar(50) NULL,
        Title nvarchar(255) NULL,
        Message nvarchar(max) NULL,
        Data nvarchar(max) NULL,
        IsRead bit NOT NULL,
        CreatedAt datetime2 NOT NULL,
        CONSTRAINT PK_Notifications_new PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_Notifications_new_Users_UserID FOREIGN KEY (UserID) REFERENCES dbo.Users (ID) ON DELETE CASCADE
    );

    SET IDENTITY_INSERT dbo.Notifications_new ON;

    INSERT INTO dbo.Notifications_new (ID, UserID, [Type], Title, Message, Data, IsRead, CreatedAt)
    SELECT CAST(ID AS int), UserID, [Type], Title, Message, Data, IsRead, CreatedAt FROM dbo.Notifications;

    SET IDENTITY_INSERT dbo.Notifications_new OFF;

    DROP TABLE dbo.Notifications;

    EXEC sp_rename 'dbo.Notifications_new', 'Notifications';

    CREATE INDEX IX_Notifications_new_UserID ON dbo.Notifications (UserID);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert Notifications.ID back to bigint if currently int
            migrationBuilder.Sql(@"
IF EXISTS(
    SELECT 1 FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'dbo.Notifications') AND name = 'ID' AND system_type_id = 56
)
BEGIN
    CREATE TABLE dbo.Notifications_old (
        ID bigint IDENTITY(1,1) NOT NULL,
        UserID int NOT NULL,
        [Type] nvarchar(50) NULL,
        Title nvarchar(255) NULL,
        Message nvarchar(max) NULL,
        Data nvarchar(max) NULL,
        IsRead bit NOT NULL,
        CreatedAt datetime2 NOT NULL,
        CONSTRAINT PK_Notifications_old PRIMARY KEY CLUSTERED (ID),
        CONSTRAINT FK_Notifications_old_Users_UserID FOREIGN KEY (UserID) REFERENCES dbo.Users (ID) ON DELETE CASCADE
    );

    SET IDENTITY_INSERT dbo.Notifications_old ON;

    INSERT INTO dbo.Notifications_old (ID, UserID, [Type], Title, Message, Data, IsRead, CreatedAt)
    SELECT CAST(ID AS bigint), UserID, [Type], Title, Message, Data, IsRead, CreatedAt FROM dbo.Notifications;

    SET IDENTITY_INSERT dbo.Notifications_old OFF;

    DROP TABLE dbo.Notifications;

    EXEC sp_rename 'dbo.Notifications_old', 'Notifications';

    CREATE INDEX IX_Notifications_old_UserID ON dbo.Notifications (UserID);
END
");
        }
    }
}
