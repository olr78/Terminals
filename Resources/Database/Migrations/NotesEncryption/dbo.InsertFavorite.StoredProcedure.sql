USE [{DATABASE_NAME}]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER PROCEDURE [dbo].[InsertFavorite]
	(
	@Name nvarchar(255),
    @Protocol nvarchar(5),
    @Port int,
    @ServerName nvarchar(255),
    @NewWindow bit,
    @DesktopShare nvarchar(255),
    @Notes nvarchar(max)
	)
AS
	insert into Favorites
    (Name, Protocol, Port, ServerName, NewWindow,
    DesktopShare, Notes)

    values (@Name, @Protocol, @Port, @ServerName, @NewWindow,
    @DesktopShare, @Notes)

select SCOPE_IDENTITY() as Id
GO
