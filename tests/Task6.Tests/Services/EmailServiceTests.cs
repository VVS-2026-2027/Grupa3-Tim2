using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using Moq;
using Task6.Services;

namespace Task6.Tests.Services
{
    public class EmailServiceTests
    {
        private readonly Mock<ISmtpClient> _smtp = new();
        private readonly EmailService _service;

        public EmailServiceTests()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["EmailSettings:SenderName"] = "The Last Key",
                    ["EmailSettings:SenderEmail"] = "sender@example.test",
                    ["EmailSettings:SmtpServer"] = "smtp.example.test",
                    ["EmailSettings:Port"] = "587",
                    ["EmailSettings:Password"] = "test-password"
                }).Build();
            _service = new EmailService(configuration, () => _smtp.Object);
        }

        [Fact]
        public async Task SendEmailAsync_IspravniPodaci_SaljeHtmlPorukuPrekoStartTls()
        {
            MimeMessage? poruka = null;
            _smtp.Setup(c => c.SendAsync(It.IsAny<MimeMessage>(), default, null))
                .Callback<MimeMessage, CancellationToken, MailKit.ITransferProgress?>((m, _, _) => poruka = m)
                .ReturnsAsync("OK");

            await _service.SendEmailAsync("recipient@example.test", "Potvrda", "<p>Termin potvrđen.</p>");

            Assert.NotNull(poruka);
            Assert.Equal("sender@example.test", Assert.Single(poruka.From.Mailboxes).Address);
            Assert.Equal("recipient@example.test", Assert.Single(poruka.To.Mailboxes).Address);
            Assert.Equal("Potvrda", poruka.Subject);
            Assert.Equal("<p>Termin potvrđen.</p>", poruka.HtmlBody);
            _smtp.Verify(c => c.ConnectAsync("smtp.example.test", 587, SecureSocketOptions.StartTls, default), Times.Once);
            _smtp.Verify(c => c.AuthenticateAsync("sender@example.test", "test-password", default), Times.Once);
            _smtp.Verify(c => c.DisconnectAsync(true, default), Times.Once);
            _smtp.Verify(c => c.Dispose(), Times.Once);
        }

        [Fact]
        public async Task SendEmailAsync_NeuspjelaAutentikacija_PropagiraGreskuIOslobadjaKlijent()
        {
            _smtp.Setup(c => c.AuthenticateAsync("sender@example.test", "test-password", default))
                .ThrowsAsync(new InvalidOperationException("Autentikacija nije uspjela."));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.SendEmailAsync("recipient@example.test", "Potvrda", "<p>Termin potvrđen.</p>"));

            _smtp.Verify(c => c.SendAsync(It.IsAny<MimeMessage>(), default, null), Times.Never);
            _smtp.Verify(c => c.Dispose(), Times.Once);
        }
    }
}
