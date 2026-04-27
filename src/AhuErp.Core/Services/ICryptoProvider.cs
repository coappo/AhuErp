namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 8 — абстракция криптопровайдера для подписи и проверки.
    /// Конкретные реализации:
    /// <list type="bullet">
    ///   <item><description><see cref="HmacCryptoProvider"/> — для ПЭП/НЭП.
    ///     В качестве «ключа» использует <c>Employee.PasswordHash</c>
    ///     (не сам пароль).</description></item>
    ///   <item><description><see cref="CryptoProStub"/> — задел под реальный
    ///     CryptoPro CSP / VipNet, бросает <see cref="System.NotSupportedException"/>
    ///     до интеграции.</description></item>
    /// </list>
    /// </summary>
    public interface ICryptoProvider
    {
        /// <summary>Подписать содержимое; thumbprint трактуется по реализации (HMAC-key id или X.509 thumbprint).</summary>
        byte[] Sign(byte[] payload, string thumbprint);

        /// <summary>Проверить подпись над содержимым.</summary>
        bool Verify(byte[] payload, byte[] signature, string thumbprint);

        /// <summary>Subject-DN сертификата по thumbprint (для УЦ-подписей) или fallback-описание для HMAC.</summary>
        string GetSubject(string thumbprint);
    }
}
