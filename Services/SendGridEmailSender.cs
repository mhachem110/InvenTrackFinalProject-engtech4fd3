using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace InvenTrack.Services
{
    public class SendGridEmailSender : IEmailSender
    {
        private readonly SendGridSettings _settings;
        private readonly ILogger<SendGridEmailSender> _logger;

        public SendGridEmailSender(IOptions<SendGridSettings> settings, ILogger<SendGridEmailSender> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
                throw new InvalidOperationException("SendGrid ApiKey is missing (SendGrid:ApiKey).");

            if (string.IsNullOrWhiteSpace(_settings.FromEmail))
                throw new InvalidOperationException("SendGrid FromEmail is missing (SendGrid:FromEmail).");

            var client = new SendGridClient(_settings.ApiKey);

            var from = new EmailAddress(_settings.FromEmail, _settings.FromName);
            var to = new EmailAddress(email);

            var msg = MailHelper.CreateSingleEmail(
                from,
                to,
                subject,
                plainTextContent: StripHtml(htmlMessage),
                htmlContent: htmlMessage);

            var response = await client.SendEmailAsync(msg);

            if ((int)response.StatusCode >= 200 && (int)response.StatusCode <= 299)
            {
                _logger.LogInformation("SendGrid email sent. To={To} Subject={Subject}", email, subject);
                return;
            }

            var body = await response.Body.ReadAsStringAsync();
            _logger.LogError("SendGrid email failed. Status={Status} To={To} Subject={Subject} Body={Body}",
                (int)response.StatusCode, email, subject, body);

            throw new InvalidOperationException($"SendGrid rejected email. Status={(int)response.StatusCode}. Body={body}");
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";
            var charArray = new char[html.Length];
            int arrayIndex = 0;
            bool inside = false;

            for (int i = 0; i < html.Length; i++)
            {
                char let = html[i];
                if (let == '<') { inside = true; continue; }
                if (let == '>') { inside = false; continue; }
                if (!inside) { charArray[arrayIndex++] = let; }
            }

            return new string(charArray, 0, arrayIndex).Trim();
        }
    }
}