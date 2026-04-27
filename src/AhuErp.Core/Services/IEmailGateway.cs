namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 9 — абстракция отправки e-mail. В демо-режиме привязывается к
    /// <see cref="NoOpEmailGateway"/>; в проде — к <see cref="SmtpEmailGateway"/>.
    /// </summary>
    public interface IEmailGateway
    {
        /// <summary>Отправить письмо. Реализация может бросать при сетевой ошибке.</summary>
        void Send(string toEmail, string subject, string body);
    }
}
