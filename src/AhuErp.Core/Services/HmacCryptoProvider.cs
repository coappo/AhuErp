using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 8 — реализация для ПЭП/НЭП через HMAC-SHA256.
    /// «Ключом» выступает строка <c>thumbprint</c> (на практике — PasswordHash
    /// сотрудника). Это не криптографически стойкая схема в смысле 63-ФЗ
    /// (это эмуляция), но достаточна для simple/enhanced-подписи в локальной
    /// СЭД с журналом аудита.
    /// </summary>
    public sealed class HmacCryptoProvider : ICryptoProvider
    {
        public byte[] Sign(byte[] payload, string thumbprint)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (string.IsNullOrEmpty(thumbprint)) throw new ArgumentException("thumbprint обязателен", nameof(thumbprint));
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(thumbprint)))
            {
                return hmac.ComputeHash(payload);
            }
        }

        public bool Verify(byte[] payload, byte[] signature, string thumbprint)
        {
            if (payload == null || signature == null || string.IsNullOrEmpty(thumbprint)) return false;
            var expected = Sign(payload, thumbprint);
            return CryptographicEquals(expected, signature);
        }

        public string GetSubject(string thumbprint)
            => string.IsNullOrEmpty(thumbprint)
                ? "CN=AhuErp/HMAC"
                : $"CN=AhuErp/HMAC; KeyId={Truncate(thumbprint, 16)}";

        private static bool CryptographicEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static string Truncate(string s, int max)
            => s.Length <= max ? s : new string(s.Take(max).ToArray());
    }
}
