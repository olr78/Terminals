USE [{DATABASE_NAME}]
GO
-- Notes is now stored encrypted (AES, same scheme as passwords), and ciphertext
-- is roughly 2.5-3x longer than the original plaintext, so nvarchar(500) is too small.
ALTER TABLE [dbo].[Favorites] ALTER COLUMN [Notes] [nvarchar](max) NULL
GO
