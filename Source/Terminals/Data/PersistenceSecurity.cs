using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using Terminals.Configuration;
using Terminals.Security;

namespace Terminals.Data
{
    /// <summary>
    /// Provides persistence authentication and manipulations with master password.
    /// Doesn't distinguish between application and persistence master password.
    /// </summary>
    internal class PersistenceSecurity : IDisposable
    {
        private readonly Settings settings = Settings.Instance;
        private bool isAuthenticated;

        /// <summary>
        /// Master password entered by the user, kept only until the stored key created by outdated
        /// key derivation is replaced, see <see cref="UpgradeKeyDerivationIfRequired"/>.
        /// </summary>
        private SecureString passwordToUpgrade;

        protected SecureString KeyMaterial { get; private set; }

        protected virtual SecureString PersistenceKeyMaterial { get { return this.KeyMaterial; } }

        internal bool IsMasterPasswordDefined
        {
            get
            {
                return AuthenticationSequence.IsMasterPasswordDefined();
            }
        }

        internal event Action<string> OnUpdatePasswordsByNewMasterPassword;

        internal PersistenceSecurity()
        {
            this.KeyMaterial = new SecureString();
        }

        /// <summary>
        /// Creates new instance and initializes internal state from sourceSecurity.
        /// </summary>
        /// <param name="sourceSecurity">Not null current security instance to initialize from.</param>
        internal PersistenceSecurity(PersistenceSecurity sourceSecurity)
        {
            this.KeyMaterial = sourceSecurity.KeyMaterial.Copy();
        }

        internal bool Authenticate(Func<bool, AuthenticationPrompt> knowsUserPassword)
        {
            // don't prompt user third time for password when upgrading passwords from v2
            if (this.isAuthenticated)
                return true;

            var authentication = new AuthenticationSequence(this.IsMasterPasswordValid, knowsUserPassword);
            this.isAuthenticated = authentication.AuthenticateIfRequired();
            return this.isAuthenticated;
        }

        private Boolean IsMasterPasswordValid(string passwordToCheck)
        {
            string storedMasterPassword = this.settings.MasterPasswordHash;
            string key;
            if (PasswordFunctions2.TryCalculateMasterPasswordKey(passwordToCheck, storedMasterPassword, out key))
            {
                this.SetKeyMaterial(key);
                if (PasswordFunctions2.IsKeyDerivationOutdated(storedMasterPassword))
                    this.passwordToUpgrade = ToSecureString(passwordToCheck);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Replaces the master password key created by outdated (too fast) key derivation by new one,
        /// using the same master password. All passwords encrypted by the master password key are
        /// re-encrypted, same as when the master password is changed. Call only after the persistence
        /// was initialized, because the file persistence handles the update event only after that.
        /// </summary>
        internal void UpgradeKeyDerivationIfRequired()
        {
            if (this.passwordToUpgrade == null)
                return;

            try
            {
                Logging.Info("Upgrading the master password key derivation.");
                BackupFilesBeforeKeyUpgrade();
                this.UpdateMasterPassword(ToUnsecureString(this.passwordToUpgrade));
                Logging.Info("Master password key derivation upgraded.");
            }
            catch (Exception exception)
            {
                Logging.Error("Unable to upgrade the master password key derivation", exception);
            }
            finally
            {
                this.passwordToUpgrade.Dispose();
                this.passwordToUpgrade = null;
            }
        }

        private void BackupFilesBeforeKeyUpgrade()
        {
            FileLocations locations = this.settings.FileLocations;
            BackupFile(locations.Configuration);
            BackupFile(locations.Favorites);
            BackupFile(locations.Credentials);
        }

        private static void BackupFile(string fileName)
        {
            string backup = fileName + ".before-key-upgrade.bak";
            // keep the first backup, it is the last one encrypted by the old key
            if (File.Exists(fileName) && !File.Exists(backup))
                File.Copy(fileName, backup);
        }

        private static string ToUnsecureString(SecureString value)
        {
            IntPtr pointer = Marshal.SecureStringToGlobalAllocUnicode(value);
            try
            {
                return Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(pointer);
            }
        }

        internal void UpdateMasterPassword(string newPassword)
        {
            string newMasterKey;
            string storedMasterPassword = PasswordFunctions2.CalculateStoredMasterPasswordKey(newPassword, out newMasterKey);

            // start of not secured transaction. Old key is still present,
            // but passwords are already encrypted by new Key
            if (this.OnUpdatePasswordsByNewMasterPassword != null)
                this.OnUpdatePasswordsByNewMasterPassword(newMasterKey);

            this.settings.UpdateConfigurationPasswords(newMasterKey, storedMasterPassword);
            // finish transaction, the passwords now reflect the new key
            this.SetKeyMaterial(newMasterKey);
        }

        private void SetKeyMaterial(string key)
        {
            if (this.KeyMaterial != null)
                this.KeyMaterial.Dispose();
            this.KeyMaterial = ToSecureString(key);
        }

        /// <summary>
        /// Use only to resolve settings file passwords
        /// </summary>
        internal string DecryptPassword(string encryptedPassword)
        {
            return PasswordFunctions2.DecryptPassword(encryptedPassword, this.KeyMaterial);
        }

        /// <summary>
        /// Use only to store settings file passwords
        /// </summary>
        internal string EncryptPassword(string decryptedPassword)
        {
            return PasswordFunctions2.EncryptPassword(decryptedPassword, this.KeyMaterial);
        }

        /// <summary>
        /// Use only to resolve all other passwords except settings file passwords
        /// </summary>
        internal string DecryptPersistencePassword(string encryptedPassword)
        {
            return PasswordFunctions2.DecryptPassword(encryptedPassword, this.PersistenceKeyMaterial);
        }

        /// <summary>
        /// Use only to store all other passwords except settings file passwords
        /// </summary>
        internal string EncryptPersistencePassword(string decryptedPassword)
        {
            return PasswordFunctions2.EncryptPassword(decryptedPassword, this.PersistenceKeyMaterial);
        }

        protected static SecureString ToSecureString(string value)
        {
            var secure = new SecureString();
            if (!string.IsNullOrEmpty(value))
            {
                foreach (char c in value)
                    secure.AppendChar(c);
            }
            secure.MakeReadOnly();
            return secure;
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && this.KeyMaterial != null)
                this.KeyMaterial.Dispose();

            if (disposing && this.passwordToUpgrade != null)
                this.passwordToUpgrade.Dispose();
        }
    }
}
