USE [{DATABASE_NAME}]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER PROCEDURE [dbo].[UpdateFavorite]
	(
	@Id int,
	@Name nvarchar(max),
    @Protocol nvarchar(5),
    @Port nvarchar(max),
    @ServerName nvarchar(max),
    @NewWindow bit,
    @DesktopShare nvarchar(255),
    @Notes nvarchar(max)
	)
AS
	update Favorites
    set
    Name = @Name, Protocol = @Protocol,
    Port = @Port, ServerName = @ServerName, NewWindow = @NewWindow,
    DesktopShare = @DesktopShare, Notes = @Notes
    where Id = @Id
GO
