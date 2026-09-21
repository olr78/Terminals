USE [{DATABASE_NAME}]
GO
-- Name, ServerName and Port are now stored encrypted (AES, same scheme as Notes/passwords),
-- and ciphertext is longer than the original plaintext, so the fixed nvarchar(255)/int
-- columns are too small. Existing plaintext values are implicitly converted to their
-- string form by SQL Server for the Port column; the one-off re-encryption pass
-- (run once via the app, see project notes) then encrypts all three columns in place.
ALTER TABLE [dbo].[Favorites] ALTER COLUMN [Name] [nvarchar](max) NOT NULL
GO
ALTER TABLE [dbo].[Favorites] ALTER COLUMN [ServerName] [nvarchar](max) NOT NULL
GO
ALTER TABLE [dbo].[Favorites] ALTER COLUMN [Port] [nvarchar](max) NOT NULL
GO
