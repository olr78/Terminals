using System;
using System.Xml;
using System.Xml.Linq;
using Terminals.Security;

namespace Terminals.Integration
{
    /// <summary>
    /// Wraps/unwraps the Terminals native export XML in a password-protected encrypted
    /// envelope, so sensitive fields (passwords, server names, notes, etc.) never appear
    /// in plain text in an exported file. Uses the same AES scheme as the persistence
    /// master password, but keyed by a password chosen at export time - not the app's own
    /// master password, so the exported file stays portable to another machine/install.
    /// </summary>
    internal static class EncryptedExportFile
    {
        private const string ROOT_ELEMENT = "encryptedFavorites";
        private const string STORED_KEY_ELEMENT = "storedKey";
        private const string PAYLOAD_ELEMENT = "payload";

        internal static void WriteEncrypted(string plainXml, string password, string fileName)
        {
            string storedKey = PasswordFunctions2.CalculateStoredMasterPasswordKey(password);
            string keyMaterial = PasswordFunctions2.CalculateMasterPasswordKey(password, storedKey);
            string cipherText = PasswordFunctions2.EncryptPassword(plainXml, keyMaterial);

            var document = new XDocument(
                new XElement(ROOT_ELEMENT,
                    new XElement(STORED_KEY_ELEMENT, storedKey),
                    new XElement(PAYLOAD_ELEMENT, cipherText)));
            document.Save(fileName);
        }

        /// <summary>
        /// Peeks at the file's root element to identify it as an encrypted export,
        /// without fully loading or parsing its content.
        /// </summary>
        internal static bool IsEncrypted(string fileName)
        {
            try
            {
                using (XmlReader reader = XmlReader.Create(fileName))
                {
                    if (reader.MoveToContent() == XmlNodeType.Element)
                        return reader.LocalName == ROOT_ELEMENT;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        /// <summary>
        /// Returns the decrypted plain text XML, or null if the password was wrong
        /// or the content could not be decrypted.
        /// </summary>
        internal static string TryReadDecrypted(string fileName, string password)
        {
            XDocument document = XDocument.Load(fileName);
            string storedKey = (string)document.Root.Element(STORED_KEY_ELEMENT);
            string cipherText = (string)document.Root.Element(PAYLOAD_ELEMENT);

            if (string.IsNullOrEmpty(storedKey) || string.IsNullOrEmpty(cipherText))
                return null;

            if (!PasswordFunctions2.MasterPasswordIsValid(password, storedKey))
                return null;

            string keyMaterial = PasswordFunctions2.CalculateMasterPasswordKey(password, storedKey);
            string plainXml = PasswordFunctions2.DecryptPassword(cipherText, keyMaterial);
            return string.IsNullOrEmpty(plainXml) ? null : plainXml;
        }
    }
}
