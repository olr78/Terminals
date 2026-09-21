using System.Collections.Generic;

namespace Terminals.Integration.Export
{
    /// <summary>
    /// Export parameters container
    /// </summary>
    internal class ExportOptions
    {
        internal string ProviderFilter { get; set; }

        /// <summary>
        /// Full path and name of the destination file including extension.
        /// </summary>
        internal string FileName { get; set; }

        /// <summary>
        /// Not null collection of favorites to export.
        /// </summary>
        internal List<FavoriteConfigurationElement> Favorites { get; set; }

        /// <summary>
        /// if set to <c>true</c> includes paswords in not encrypted form into the destination file.
        /// </summary>
        internal bool IncludePasswords { get; set; }

        /// <summary>
        /// If set to <c>true</c>, the whole destination file content is encrypted with
        /// <see cref="EncryptionPassword"/> instead of written as plain text.
        /// </summary>
        internal bool EncryptFile { get; set; }

        /// <summary>
        /// Password used to encrypt the destination file when <see cref="EncryptFile"/> is set.
        /// Not the persistence master password - chosen at export time so the file stays
        /// portable to another machine/install.
        /// </summary>
        internal string EncryptionPassword { get; set; }
    }
}
