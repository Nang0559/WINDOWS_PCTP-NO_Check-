using System;
using System.Security.Cryptography;
using System.Text;

namespace PCTP.Security
{
    /// <summary>PBKDF2-HMAC-SHA256 password storage for .NET Framework 4.7.2.</summary>
    public static class PasswordHasher
    {
        public const int DefaultIterations = 210000;
        public const int SaltSize = 16;
        public const int HashSize = 32;

        public static byte[] CreateSalt()
        {
            var salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            return salt;
        }

        public static byte[] Hash(string password, byte[] salt, int iterations = DefaultIterations)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("Mật khẩu không được để trống.", nameof(password));
            if (salt == null || salt.Length < SaltSize) throw new ArgumentException("Salt không hợp lệ.", nameof(salt));
            if (iterations < 100000 || iterations > 1000000) throw new ArgumentOutOfRangeException(nameof(iterations));

            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] block = new byte[salt.Length + 4];
            Buffer.BlockCopy(salt, 0, block, 0, salt.Length);
            block[block.Length - 1] = 1; // INT_32_BE(1)
            byte[] u;
            byte[] result;
            using (var hmac = new HMACSHA256(passwordBytes))
            {
                u = hmac.ComputeHash(block);
                result = (byte[])u.Clone();
                for (int i = 1; i < iterations; i++)
                {
                    u = hmac.ComputeHash(u);
                    for (int j = 0; j < result.Length; j++) result[j] ^= u[j];
                }
            }
            Array.Clear(passwordBytes, 0, passwordBytes.Length);
            return result;
        }

        public static bool Verify(string password, byte[] salt, byte[] expectedHash, int iterations)
        {
            if (expectedHash == null || expectedHash.Length != HashSize) return false;
            byte[] actual = Hash(password, salt, iterations);
            int diff = actual.Length ^ expectedHash.Length;
            for (int i = 0; i < Math.Min(actual.Length, expectedHash.Length); i++) diff |= actual[i] ^ expectedHash[i];
            Array.Clear(actual, 0, actual.Length);
            return diff == 0;
        }

        public static void ValidatePolicy(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 12 || password.Length > 256)
                throw new InvalidOperationException("Mật khẩu phải có từ 12 đến 256 ký tự.");
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("Mật khẩu không được chỉ chứa khoảng trắng.");
        }
    }
}