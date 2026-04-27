using System;
using System.Collections.Generic;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 9 — заглушка SMTP для демо/тестов: сохраняет отправленные сообщения
    /// в памяти, ничего не делает с сетью. Удобна для проверки в тестах.
    /// </summary>
    public sealed class NoOpEmailGateway : IEmailGateway
    {
        /// <summary>История «отправленных» писем; используется в тестах.</summary>
        public List<SentEmail> Sent { get; } = new List<SentEmail>();

        public void Send(string toEmail, string subject, string body)
        {
            Sent.Add(new SentEmail
            {
                To = toEmail,
                Subject = subject,
                Body = body,
                SentAt = DateTime.Now,
            });
        }

        public sealed class SentEmail
        {
            public string To { get; set; }
            public string Subject { get; set; }
            public string Body { get; set; }
            public DateTime SentAt { get; set; }
        }
    }
}
