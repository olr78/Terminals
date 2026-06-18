using System.Security;
using Terminals.Configuration;
using Terminals.Security;

namespace Terminals.Data.DB
{
    /// <summary>
    /// Distinguish between the application master password and persistence master password.
    /// Used only by SqlPersistence.
    /// </summary>
    internal class SqlPersistenceSecurity : PersistenceSecurity
    {
        private SecureString persistenceKeyMaterial;

        protected override SecureString PersistenceKeyMaterial
        {
            get { return this.persistenceKeyMaterial; }
        }

        internal bool UpdateDatabaseKey()
        {
            var settings = Settings.Instance;
            return this.UpdateDatabaseKey(settings.ConnectionString, settings.DatabaseMasterPassword);
        }

        internal bool UpdateDatabaseKey(string connectionString, string databasePassword)
        {
            try
            {
                string databaseStoredKey = DatabaseConnections.TryGetMasterPasswordHash(connectionString);
                string key = PasswordFunctions2.CalculateMasterPasswordKey(databasePassword, databaseStoredKey);
                if (this.persistenceKeyMaterial != null)
                    this.persistenceKeyMaterial.Dispose();
                this.persistenceKeyMaterial = ToSecureString(key);
                return true;
            }
            catch
            {
                Logging.Error("Unable to obtain database key from database");
                return false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && this.persistenceKeyMaterial != null)
                this.persistenceKeyMaterial.Dispose();
            base.Dispose(disposing);
        }
    }
}
