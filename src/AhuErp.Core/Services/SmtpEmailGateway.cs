using System;
using System.Net;
using System.Net.Mail;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 9 — реальный SMTP-шлюз. Параметры задаются через <see cref="SmtpEmailGatewayOptions"/>.
    /// При выключенном сервере в App.config регистрируется <see cref="NoOpEmailGateway"/>.
    /// </summary>
    public sealed class SmtpEmailGateway : IEmailGateway, IDisposable
    {
        private readonly SmtpEmailGatewayOptions _options;
        private readonly SmtpClient _client;

        public SmtpEmailGateway(SmtpEmailGatewayOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _client = new SmtpClient(options.Host, options.Port)
            {
                EnableSsl = options.UseSsl,
                Timeout = (int)TimeSpan.FromSeconds(15).TotalMilliseconds,
            };
            if (!string.IsNullOrEmpty(options.UserName))
            {
                _client.Credentials = new NetworkCredential(options.UserName, options.Password);
            }
        }

        public void Send(string toEmail, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return;
            using (var msg = new MailMessage(_options.FromAddress, toEmail)
            {
                Subject = subject ?? string.Empty,
                Body = body ?? string.Empty,
                IsBodyHtml = false,
            })
            {
                _client.Send(msg);
            }
        }

        public void Dispose() => _client?.Dispose();
    }

    public sealed class SmtpEmailGatewayOptions
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 25;
        public bool UseSsl { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string FromAddress { get; set; } = "noreply@ahu.bmr";
    }
}
