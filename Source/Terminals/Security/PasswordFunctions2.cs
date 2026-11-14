using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace Terminals.Security
{
    /// <summary>
    /// Security encryption/decryption logic for stored passwords after version 2.0.
    /// This version resolves previous version problems.
    /// Used Algorithm and length of key and salt are identical with previous version.
    /// Passwords are stored as text combined of initialization vector + encrypted password.
    /// Master password key and stored password keys are based on random initialization vector.
    /// There is no stored master password key hash.
    /// To create new master password key:
    /// 1. call <see cref="CalculateStoredMasterPasswordKey"/> method
    /// 2. call <see cref="CalculateMasterPasswordKey(string,string)"/> to get new master password key
    /// 
    /// Links:
    /// <seealso cref="http://msdn.microsoft.com/en-us/magazine/cc163913.aspx"/>
    /// <seealso cref="http://blogs.msdn.com/b/shawnfa/archive/2004/04/14/generating-a-key-from-a-password.aspx?Redirected=true"/>
    /// <seealso cref="http://msdn.microsoft.com/en-us/library/system.security.cryptography.rfc2898derivebytes.aspx"/>
    /// </summary>
    internal class PasswordFunctions2
    {
        /// <summary>
        /// PBKDF2-HMAC-SHA1 iterations of the legacy stored master password key format (64 bytes:
        /// master key salt, validation key salt, validation key). Too weak against offline guessing,
        /// kept only to read the old stored keys.
        /// </summary>
        private const int LEGACY_ITERATION_COUNT = 2121;

        /// <summary>
        /// PBKDF2-HMAC-SHA256 iterations used for newly created stored keys (OWASP recommendation).
        /// The value is part of the stored key, so it can be increased later without breaking existing keys.
        /// </summary>
        internal const int ITERATION_COUNT = 600000;

        /// <summary>
        /// Current stored key format: master key salt (16B), validation hash (32B), format id (1B),
        /// iteration count (4B, little endian). The validation hash is HMAC-SHA256 of a constant label
        /// keyed by the master key, so validating a password costs the same as deriving the key.
        /// </summary>
        private const byte FORMAT_PBKDF2_SHA256 = 2;

        private const int STORED_KEY_LENGTH = IV_LENGTH + KEY_LENGTH + 1 + 4;

        private const int MAX_ITERATION_COUNT = 100000000;

        private static readonly byte[] validationLabel = Encoding.ASCII.GetBytes("Terminals master password validation");

        /// <summary>
        /// Stronger, than in previous version
        /// </summary>
        private const int KEY_LENGTH = 32;
        
        /// <summary>
        /// Identical with previous version (16B).
        /// </summary>
        private const int IV_LENGTH = PasswordFunctions.IV_LENGTH;

        private static readonly RandomNumberGenerator saltGenerator = RandomNumberGenerator.Create();

        /// <summary>
        /// Ensures if, password entered by user is valid against storedPassword.
        /// </summary>
        /// <param name="password">Password entered by user</param>
        /// <param name="storedMasterPassword">Validation key, encrypted by masterPassword key,
        /// which should be identical to key, which we are able to generate from entered password</param>
        internal static bool MasterPasswordIsValid(string password, string storedMasterPassword)
        {
            string masterKey;
            return TryCalculateMasterPasswordKey(password, storedMasterPassword, out masterKey);
        }

        /// <summary>
        /// Validates the password against the stored key and returns the master password key,
        /// if the password is valid. Faster than <see cref="MasterPasswordIsValid"/> followed by
        /// <see cref="CalculateMasterPasswordKey(string,string)"/>, because the key is derived only once.
        /// </summary>
        internal static bool TryCalculateMasterPasswordKey(string password, string storedMasterPassword, out string masterKey)
        {
            masterKey = string.Empty;
            int iterations;
            byte[] stored = ParseStoredKey(storedMasterPassword, out iterations);
            if (stored != null)
            {
                if (string.IsNullOrEmpty(password) || iterations <= 0)
                    return false;

                byte[] key = DeriveKey(password, stored.Take(IV_LENGTH).ToArray(), iterations);
                byte[] expected = stored.Skip(IV_LENGTH).Take(KEY_LENGTH).ToArray();
                if (!FixedTimeEquals(CalculateValidationHash(key), expected))
                    return false;

                masterKey = Convert.ToBase64String(key);
                return true;
            }

            if (!LegacyMasterPasswordIsValid(password, storedMasterPassword))
                return false;

            masterKey = CalculateMasterPasswordKey(password, storedMasterPassword);
            return true;
        }

        /// <summary>
        /// Returns true, if the stored key was created by older or weaker key derivation than the current one.
        /// The master password key should be replaced by new one created by <see cref="CalculateStoredMasterPasswordKey(string)"/>.
        /// </summary>
        internal static bool IsKeyDerivationOutdated(string storedMasterPassword)
        {
            if (string.IsNullOrEmpty(storedMasterPassword))
                return false;

            int iterations;
            byte[] stored = ParseStoredKey(storedMasterPassword, out iterations);
            return stored == null || iterations < ITERATION_COUNT;
        }

        /// <summary>
        /// Returns not null decoded stored key in current format with its iteration count.
        /// Returns null for legacy stored keys.
        /// </summary>
        private static byte[] ParseStoredKey(string storedMasterPassword, out int iterations)
        {
            iterations = 0;
            if (string.IsNullOrEmpty(storedMasterPassword))
                return null;

            byte[] stored = Convert.FromBase64String(storedMasterPassword);
            if (stored.Length != STORED_KEY_LENGTH || stored[IV_LENGTH + KEY_LENGTH] != FORMAT_PBKDF2_SHA256)
                return null;

            int iterationsIndex = IV_LENGTH + KEY_LENGTH + 1;
            iterations = stored[iterationsIndex] | stored[iterationsIndex + 1] << 8 |
                         stored[iterationsIndex + 2] << 16 | stored[iterationsIndex + 3] << 24;
            if (iterations > MAX_ITERATION_COUNT)
                iterations = 0;

            return stored;
        }

        private static byte[] DeriveKey(string password, byte[] keySalt, int iterations)
        {
            return Pbkdf2Sha256.DeriveKey(password, keySalt, iterations, KEY_LENGTH);
        }

        private static byte[] CalculateValidationHash(byte[] masterKey)
        {
            using (var hmac = new HMACSHA256(masterKey))
                return hmac.ComputeHash(validationLabel);
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
                return false;

            int difference = 0;
            for (int index = 0; index < left.Length; index++)
                difference |= left[index] ^ right[index];

            return difference == 0;
        }

        private static bool LegacyMasterPasswordIsValid(string password, string storedMasterPassword)
        {
            // first part is master password key
            Tuple<byte[], byte[]> keyParts = SplitEncryptedPassword(storedMasterPassword);
            // from the second part again first part is salt, second is not encrypted validation key
            Tuple<byte[], byte[]> validationParts = SplitEncryptedPassword(keyParts.Item2);
            
            // empty password check
            if (string.IsNullOrEmpty(password) && validationParts.Item1.Length == 0)
                return true;

            byte[] validationKey = CalculateMasterPasswordKey(password, validationParts.Item1);
            return validationKey.SequenceEqual(validationParts.Item2);
        }

        /// <summary>
        /// Replacement of v1 passwords <see cref="PasswordFunctions.ComputeMasterPasswordHash"/>
        /// </summary>
        internal static string CalculateStoredMasterPasswordKey(string password)
        {
            string masterKey;
            return CalculateStoredMasterPasswordKey(password, out masterKey);
        }

        /// <summary>
        /// Creates new stored key for the password in current format and returns also the master password key,
        /// which is identical to <see cref="CalculateMasterPasswordKey(string,string)"/> result for the stored key.
        /// </summary>
        internal static string CalculateStoredMasterPasswordKey(string password, out string masterKey)
        {
            masterKey = string.Empty;
            if (string.IsNullOrEmpty(password))
                return string.Empty;

            byte[] masterKeySalt = CreateRandomKeySalt();
            byte[] key = DeriveKey(password, masterKeySalt, ITERATION_COUNT);
            byte[] validationHash = CalculateValidationHash(key);
            var stored = new byte[STORED_KEY_LENGTH];
            masterKeySalt.CopyTo(stored, 0);
            validationHash.CopyTo(stored, IV_LENGTH);
            int index = IV_LENGTH + KEY_LENGTH;
            stored[index] = FORMAT_PBKDF2_SHA256;
            int iterations = ITERATION_COUNT;
            stored[index + 1] = (byte)iterations;
            stored[index + 2] = (byte)(iterations >> 8);
            stored[index + 3] = (byte)(iterations >> 16);
            stored[index + 4] = (byte)(iterations >> 24);
            masterKey = Convert.ToBase64String(key);
            return Convert.ToBase64String(stored);
        }

        /// <summary>
        /// Calculates new master password key using key salt used for stored master password.
        /// Doesn't validate, if the stored master password is valid. Use only this overload on real data.
        /// For newly entered passwords, should be used always after <see cref="CalculateStoredMasterPasswordKey"/> method.
        /// </summary>
        /// <param name="password">master password for which the key has to be generated</param>
        /// <param name="storedMasterPassword">Not empty stored random key derived from master password,
        /// contains two key salt</param>
        internal static string CalculateMasterPasswordKey(string password, string storedMasterPassword)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;

            int iterations;
            byte[] stored = ParseStoredKey(storedMasterPassword, out iterations);
            if (stored != null)
            {
                if (iterations <= 0)
                    throw new FormatException("Stored master password key has invalid iteration count.");

                byte[] key = DeriveKey(password, stored.Take(IV_LENGTH).ToArray(), iterations);
                return Convert.ToBase64String(key);
            }

            byte[] keySalt = SplitEncryptedPassword(storedMasterPassword).Item1;
            return CalculateMasterPasswordKeyText(password, keySalt);
        }

        private static string CalculateMasterPasswordKeyText(string password, byte[] keySalt)
        {
            byte[] key = CalculateMasterPasswordKey(password, keySalt);
            return Convert.ToBase64String(key);
        }

        private static byte[] CalculateMasterPasswordKey(string password, byte[] keySalt)
        {
            var rfbk2 = new Rfc2898DeriveBytes(password, keySalt, LEGACY_ITERATION_COUNT);
            return rfbk2.GetBytes(KEY_LENGTH);            
        }

        internal static string EncryptPassword(string password, string keyMaterial)
        {
            try
            {
                return TryEncryptPassword(password, keyMaterial);
            }
            catch (Exception exception)
            {
                Logging.Error("Error Encrypting Password", exception);
                return String.Empty;
            }
        }

        internal static string EncryptPassword(string password, SecureString keyMaterial)
        {
            try
            {
                if (string.IsNullOrEmpty(password))
                    return string.Empty;

                if (keyMaterial == null || keyMaterial.Length == 0)
                    return EncryptByEmptyKeyMaterial(password);

                byte[] keyBytes = SecureStringToKeyBytes(keyMaterial);
                try
                {
                    byte[] initializationVector = CreateRandomKeySalt();
                    return EncryptPassword(password, keyBytes, initializationVector);
                }
                finally
                {
                    Array.Clear(keyBytes, 0, keyBytes.Length);
                }
            }
            catch (Exception exception)
            {
                Logging.Error("Error Encrypting Password", exception);
                return String.Empty;
            }
        }

        private static string TryEncryptPassword(string password, string keyMaterial)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;

            if (string.IsNullOrEmpty(keyMaterial))
                return EncryptByEmptyKeyMaterial(password);

            return EncryptPasswordByRandomSalt(password, keyMaterial);
        }

        private static string EncryptPasswordByRandomSalt(string password, string keyMaterial)
        {
            byte[] initializationVector = CreateRandomKeySalt();
            byte[] passwordKey = Convert.FromBase64String(keyMaterial);
            return EncryptPassword(password, passwordKey, initializationVector);
        }

        private static string EncryptByEmptyKeyMaterial(string password)
        {
            byte[] initializationVector = CreateRandomKeySalt();
            byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
            byte[] protectedPassword = ProtectedData.Protect(passwordBytes, initializationVector, DataProtectionScope.CurrentUser);
            return ConcatenatePasswordPartsToText(initializationVector, protectedPassword);
        }

        private static string EncryptPassword(string password, byte[] passwordKey, byte[] initializationVector)
        {
            byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
            byte[] encryptedPassword = PasswordFunctions.EncryptByKey(passwordBytes, initializationVector, passwordKey);
            return ConcatenatePasswordPartsToText(initializationVector, encryptedPassword);
        }

        internal static string DecryptPassword(string encryptedPassword, string keyMaterial)
        {
            try
            {
                return TryDecryptPassword(encryptedPassword, keyMaterial);
            }
            catch (Exception exception)
            {
                Logging.Error("Error Decrypting Password", exception);
                return String.Empty;
            }
        }

        internal static string DecryptPassword(string encryptedPassword, SecureString keyMaterial)
        {
            try
            {
                if (string.IsNullOrEmpty(encryptedPassword) || encryptedPassword.Length < IV_LENGTH)
                    return string.Empty;

                if (keyMaterial == null || keyMaterial.Length == 0)
                    return DecryptByEmptyKeyMaterial(encryptedPassword);

                byte[] keyBytes = SecureStringToKeyBytes(keyMaterial);
                try
                {
                    return DecryptPassword(encryptedPassword, keyBytes);
                }
                finally
                {
                    Array.Clear(keyBytes, 0, keyBytes.Length);
                }
            }
            catch (Exception exception)
            {
                Logging.Error("Error Decrypting Password", exception);
                return String.Empty;
            }
        }

        private static string TryDecryptPassword(string encryptedPassword, string keyMaterial)
        {
            if (string.IsNullOrEmpty(encryptedPassword) || encryptedPassword.Length < IV_LENGTH)
                return string.Empty;

            if (string.IsNullOrEmpty(keyMaterial))
                return DecryptByEmptyKeyMaterial(encryptedPassword);

            byte[] passwordKey = Convert.FromBase64String(keyMaterial);
            return DecryptPassword(encryptedPassword, passwordKey);
        }

        private static string DecryptByEmptyKeyMaterial(string encryptedPassword)
        {
            Tuple<byte[], byte[]> passwordParts = SplitEncryptedPassword(encryptedPassword);
            byte[] plaintext = ProtectedData.Unprotect(passwordParts.Item2, passwordParts.Item1, DataProtectionScope.CurrentUser);
            return Encoding.Unicode.GetString(plaintext);
        }

        private static string DecryptPassword(string encryptedPassword, byte[] passwordKey)
        {
            Tuple<byte[], byte[]> passwordParts = SplitEncryptedPassword(encryptedPassword);
            byte[] decrypted = PasswordFunctions.DecryptByKey(passwordParts.Item2, passwordParts.Item1, passwordKey);
            return Encoding.Unicode.GetString(decrypted);
        }

        private static string ConcatenatePasswordPartsToText(byte[] initializationVector, byte[] encryptedPassword)
        {
            byte[] encryptedResult = ConcatenatePasswordParts(initializationVector, encryptedPassword);
            return Convert.ToBase64String(encryptedResult);
        }

        private static byte[] ConcatenatePasswordParts(byte[] initializationVector, byte[] encryptedPassword)
        {
            byte[] encryptedResult = new byte[initializationVector.Length + encryptedPassword.Length];
            initializationVector.CopyTo(encryptedResult, 0);
            encryptedPassword.CopyTo(encryptedResult, initializationVector.Length);
            return encryptedResult;
        }

        /// <summary>
        /// Extracts initialization vector as Item1 and password bytes as Item2 from encryptedPassword.
        /// </summary>
        private static Tuple<byte[], byte[]> SplitEncryptedPassword(string encryptedPassword)
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedPassword);
            return SplitEncryptedPassword(encryptedBytes);
        }

        private static Tuple<byte[], byte[]> SplitEncryptedPassword(byte[] encryptedBytes)
        {
            byte[] initializationVector = encryptedBytes.Take(IV_LENGTH).ToArray();
            byte[] passwordPart = encryptedBytes.Skip(IV_LENGTH).ToArray();
            return new Tuple<byte[], byte[]>(initializationVector, passwordPart);
        }

        /// <summary>
        /// Extracts key bytes from a SecureString containing a Base64-encoded key.
        /// Caller must zero the returned array after use.
        /// </summary>
        private static byte[] SecureStringToKeyBytes(SecureString keyMaterial)
        {
            IntPtr ptr = Marshal.SecureStringToGlobalAllocUnicode(keyMaterial);
            try
            {
                string base64 = Marshal.PtrToStringUni(ptr);
                return Convert.FromBase64String(base64);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(ptr);
            }
        }

        private static byte[] CreateRandomKeySalt()
        {
            var initializationVector = new byte[IV_LENGTH];
            saltGenerator.GetBytes(initializationVector);
            return initializationVector;
        }
    }
}