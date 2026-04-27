using System;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 8 — заглушка под реальный CryptoPro CSP / VipNet.
    /// До интеграции с УЦ бросает <see cref="NotSupportedException"/> —
    /// чтобы случайные вызовы Sign/Verify в обход настройки не молча
    /// возвращали успех.
    /// </summary>
    public sealed class CryptoProStub : ICryptoProvider
    {
        public byte[] Sign(byte[] payload, string thumbprint)
            => throw new NotSupportedException(
                "CryptoPro CSP не настроен. Подпись КЭП недоступна в текущей сборке.");

        public bool Verify(byte[] payload, byte[] signature, string thumbprint)
            => throw new NotSupportedException(
                "CryptoPro CSP не настроен. Проверка КЭП недоступна в текущей сборке.");

        public string GetSubject(string thumbprint) => $"CN=CryptoPro/Stub; Thumbprint={thumbprint}";
    }
}
