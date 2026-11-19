using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Task6.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly Func<ISmtpClient> _createClient;

        public EmailService(IConfiguration configuration)
            : this(configuration, () => new SmtpClient())
        {
        }

        internal EmailService(IConfiguration configuration, Func<ISmtpClient> createClient)
        {
            _configuration = configuration;
            _createClient = createClient;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string message)
        {
            var settings = _configuration.GetSection("EmailSettings");

            var email = new MimeMessage();

            email.From.Add(new MailboxAddress(
                settings["SenderName"],
                settings["SenderEmail"]));

            email.To.Add(MailboxAddress.Parse(toEmail));

            email.Subject = subject;

            email.Body = new TextPart("html")
            {
                Text = message
            };

            using var smtp = _createClient();

            await smtp.ConnectAsync(
                settings["SmtpServer"],
                int.Parse(settings["Port"]),
                SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(
                settings["SenderEmail"],
                settings["Password"]);

            await smtp.SendAsync(email);

            await smtp.DisconnectAsync(true);
        }
    }
}
