using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Security;

namespace Tests.Passwords
{
    /// <summary>
    /// Master password key derivation: PBKDF2-HMAC-SHA256 stored keys and compatibility with legacy stored keys.
    /// </summary>
    [TestClass]
    public class KeyDerivationTest
    {
        private const string PASSWORD = "Mäster password";

        // RFC 7914, chapter 11, PBKDF2-HMAC-SHA256 test vectors (64 bytes = two blocks)
        private const string VECTOR_1 = "55ac046e56e3089fec1691c22544b605f94185216dde0465e68b9d57c20dacbc" +
                                        "49ca9cccf179b645991664b39d77ef317c71b845b1e30bd509112041d3a19783";

        private const string VECTOR_80000 = "4ddcd8f60b98be21830cee5ef22701f9641a4418d04c0414aeff08876b34ab56" +
                                            "a1d425a1225833549adb841b51c9b3176a272bdebba1d078478f62b397f33c8d";

        [TestMethod]
        public void RfcVectors_DeriveKey_MatchesNativeAndManaged()
        {
            Assert.AreEqual(VECTOR_1, Hex(Pbkdf2Sha256.DeriveKey("passwd", Ascii("salt"), 1, 64)));
            Assert.AreEqual(VECTOR_80000, Hex(Pbkdf2Sha256.DeriveKey("Password", Ascii("NaCl"), 80000, 64)));
            Assert.AreEqual(VECTOR_1, Hex(Pbkdf2Sha256.DeriveManaged(Ascii("passwd"), Ascii("salt"), 1, 64)));
            Assert.AreEqual(VECTOR_80000, Hex(Pbkdf2Sha256.DeriveManaged(Ascii("Password"), Ascii("NaCl"), 80000, 64)));
        }

        [TestMethod]
        public void NewStoredKey_Validate_AcceptsOnlyThePassword()
        {
            string masterKey;
            string stored = PasswordFunctions2.CalculateStoredMasterPasswordKey(PASSWORD, out masterKey);

            Assert.IsTrue(PasswordFunctions2.MasterPasswordIsValid(PASSWORD, stored));
            Assert.IsFalse(PasswordFunctions2.MasterPasswordIsValid(PASSWORD + "x", stored));
            Assert.IsFalse(PasswordFunctions2.MasterPasswordIsValid(string.Empty, stored));
            Assert.IsFalse(PasswordFunctions2.IsKeyDerivationOutdated(stored));
        }

        [TestMethod]
        public void NewStoredKey_CalculateKey_ReturnsTheSameKeyEveryTime()
        {
            string createdKey;
            string stored = PasswordFunctions2.CalculateStoredMasterPasswordKey(PASSWORD, out createdKey);
            string validatedKey;
            bool isValid = PasswordFunctions2.TryCalculateMasterPasswordKey(PASSWORD, stored, out validatedKey);

            Assert.IsTrue(isValid);
            Assert.AreEqual(createdKey, validatedKey);
            Assert.AreEqual(createdKey, PasswordFunctions2.CalculateMasterPasswordKey(PASSWORD, stored));
            Assert.AreEqual(32, Convert.FromBase64String(createdKey).Length);

            string encrypted = PasswordFunctions2.EncryptPassword("secret", createdKey);
            Assert.AreEqual("secret", PasswordFunctions2.DecryptPassword(encrypted, validatedKey));
        }

        [TestMethod]
        public void LegacyStoredKey_Validate_IsStillSupportedButOutdated()
        {
            byte[] masterSalt = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
            byte[] validationSalt = Enumerable.Range(100, 16).Select(i => (byte)i).ToArray();
            byte[] validationKey = LegacyDerive(PASSWORD, validationSalt);
            string stored = Convert.ToBase64String(masterSalt.Concat(validationSalt).Concat(validationKey).ToArray());
            string expectedKey = Convert.ToBase64String(LegacyDerive(PASSWORD, masterSalt));

            string key;
            Assert.IsTrue(PasswordFunctions2.TryCalculateMasterPasswordKey(PASSWORD, stored, out key));
            Assert.AreEqual(expectedKey, key);
            Assert.AreEqual(expectedKey, PasswordFunctions2.CalculateMasterPasswordKey(PASSWORD, stored));
            Assert.IsFalse(PasswordFunctions2.MasterPasswordIsValid("wrong", stored));
            Assert.IsTrue(PasswordFunctions2.IsKeyDerivationOutdated(stored));
        }

        [TestMethod]
        public void EmptyPassword_StoredKey_IsEmptyAndValid()
        {
            string key;
            string stored = PasswordFunctions2.CalculateStoredMasterPasswordKey(string.Empty, out key);

            Assert.AreEqual(string.Empty, stored);
            Assert.AreEqual(string.Empty, key);
            Assert.IsTrue(PasswordFunctions2.MasterPasswordIsValid(string.Empty, stored));
            Assert.IsFalse(PasswordFunctions2.IsKeyDerivationOutdated(stored));
        }

        private static byte[] LegacyDerive(string password, byte[] salt)
        {
            return new Rfc2898DeriveBytes(password, salt, 2121).GetBytes(32);
        }

        private static byte[] Ascii(string text)
        {
            return Encoding.ASCII.GetBytes(text);
        }

        private static string Hex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
