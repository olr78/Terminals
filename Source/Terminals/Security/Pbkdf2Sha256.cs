using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Terminals.Security
{
    /// <summary>
    /// PBKDF2 with HMAC-SHA256 (RFC 8018). .NET Framework 4.0 offers only HMAC-SHA1 in Rfc2898DeriveBytes,
    /// so the native Windows implementation (bcrypt.dll, Windows 7 and newer) is used, which is about
    /// five times faster than managed code. The managed implementation is used only as fallback.
    /// The password is encoded as UTF-8, same as Rfc2898DeriveBytes does.
    /// </summary>
    internal static class Pbkdf2Sha256
    {
        private const int BCRYPT_ALG_HANDLE_HMAC_FLAG = 0x00000008;

        private static bool nativeAvailable = true;

        internal static byte[] DeriveKey(string password, byte[] salt, int iterations, int keyLength)
        {
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password ?? string.Empty);
            try
            {
                if (nativeAvailable)
                {
                    try
                    {
                        return DeriveNative(passwordBytes, salt, iterations, keyLength);
                    }
                    catch (Exception exception)
                    {
                        nativeAvailable = false;
                        Logging.Warn("Native PBKDF2 isn't available, using managed implementation: " + exception.Message);
                    }
                }

                return DeriveManaged(passwordBytes, salt, iterations, keyLength);
            }
            finally
            {
                Array.Clear(passwordBytes, 0, passwordBytes.Length);
            }
        }

        private static byte[] DeriveNative(byte[] password, byte[] salt, int iterations, int keyLength)
        {
            IntPtr algorithm;
            int status = BCryptOpenAlgorithmProvider(out algorithm, "SHA256", null, BCRYPT_ALG_HANDLE_HMAC_FLAG);
            if (status != 0)
                throw new CryptographicException(string.Format("BCryptOpenAlgorithmProvider failed: 0x{0:X8}", status));

            try
            {
                var key = new byte[keyLength];
                status = BCryptDeriveKeyPBKDF2(algorithm, password, password.Length, salt, salt.Length,
                                               (ulong)iterations, key, key.Length, 0);
                if (status != 0)
                    throw new CryptographicException(string.Format("BCryptDeriveKeyPBKDF2 failed: 0x{0:X8}", status));

                return key;
            }
            finally
            {
                BCryptCloseAlgorithmProvider(algorithm, 0);
            }
        }

        internal static byte[] DeriveManaged(byte[] password, byte[] salt, int iterations, int keyLength)
        {
            using (var hmac = new HMACSHA256(password))
            {
                int hashLength = hmac.HashSize / 8;
                var result = new byte[keyLength];
                var saltWithIndex = new byte[salt.Length + 4];
                salt.CopyTo(saltWithIndex, 0);

                for (int block = 1, offset = 0; offset < keyLength; block++, offset += hashLength)
                {
                    saltWithIndex[salt.Length] = (byte)(block >> 24);
                    saltWithIndex[salt.Length + 1] = (byte)(block >> 16);
                    saltWithIndex[salt.Length + 2] = (byte)(block >> 8);
                    saltWithIndex[salt.Length + 3] = (byte)block;

                    byte[] u = hmac.ComputeHash(saltWithIndex);
                    var blockResult = (byte[])u.Clone();
                    for (int iteration = 1; iteration < iterations; iteration++)
                    {
                        u = hmac.ComputeHash(u);
                        for (int index = 0; index < blockResult.Length; index++)
                            blockResult[index] ^= u[index];
                    }

                    Array.Copy(blockResult, 0, result, offset, Math.Min(hashLength, keyLength - offset));
                }

                return result;
            }
        }

        [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)]
        private static extern int BCryptOpenAlgorithmProvider(out IntPtr algorithm, string algorithmId, string implementation, int flags);

        [DllImport("bcrypt.dll")]
        private static extern int BCryptCloseAlgorithmProvider(IntPtr algorithm, int flags);

        [DllImport("bcrypt.dll")]
        private static extern int BCryptDeriveKeyPBKDF2(IntPtr algorithm, byte[] password, int passwordLength,
            byte[] salt, int saltLength, ulong iterations, byte[] derivedKey, int derivedKeyLength, int flags);
    }
}
