namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 8 — вид электронной подписи (соответствует 63-ФЗ «Об ЭП»).
    /// <list type="bullet">
    ///   <item><description><see cref="Simple"/> — ПЭП: логин + пароль + таймстамп
    ///     (HMAC-подпись с PasswordHash как ключом).</description></item>
    ///   <item><description><see cref="Enhanced"/> — НЭП: X.509-сертификат без
    ///     квалификации, проверка по локальному хранилищу.</description></item>
    ///   <item><description><see cref="Qualified"/> — КЭП: квалифицированная
    ///     подпись через CryptoPro CSP / VipNet (заглушка
    ///     <see cref="Services.CryptoProStub"/>).</description></item>
    /// </list>
    /// </summary>
    public enum SignatureKind
    {
        Simple = 0,
        Enhanced = 1,
        Qualified = 2,
    }
}
