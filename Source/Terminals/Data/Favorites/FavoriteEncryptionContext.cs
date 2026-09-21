using System;

namespace Terminals.Data
{
    /// <summary>
    /// Ambient encryption context used only while a <see cref="FavoritesFile"/> is being
    /// serialized or deserialized by <see cref="XmlSerializer"/>, which invokes property
    /// accessors directly and cannot be given extra constructor parameters.
    /// Must be set immediately before, and cleared immediately after, a single (de)serialize call.
    /// Shared by all encrypted-at-rest favorite fields (Notes, Name, ServerName, Port).
    /// </summary>
    internal static class FavoriteEncryptionContext
    {
        internal static Func<string, string> Encryptor { get; set; }

        internal static Func<string, string> Decryptor { get; set; }

        internal static string Encrypt(string plainText)
        {
            if (Encryptor == null)
                return plainText;

            return Encryptor(plainText);
        }

        internal static string Decrypt(string cipherText)
        {
            if (Decryptor == null)
                return cipherText;

            return Decryptor(cipherText);
        }
    }
}
