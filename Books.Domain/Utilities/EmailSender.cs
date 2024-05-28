using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace Books.Domain.Utilities
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress("Your Name", _configuration["EmailSenderSettings:From"]));
            emailMessage.To.Add(new MailboxAddress("", email));
            emailMessage.Subject = subject;
            emailMessage.Body = new TextPart("html") { Text = htmlMessage };

            using (var client = new SmtpClient())
            {
                try
                {
                    client.Connect(_configuration["EmailSenderSettings:SmtpServer"],
                                   int.Parse(_configuration["EmailSenderSettings:Port"]), true);
                    client.Authenticate(_configuration["EmailSenderSettings:Username"],
                                        _configuration["EmailSenderSettings:Password"]);

                    client.Send(emailMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to send email: {ex.Message}");
                    throw;
                }
                finally
                {
                    client.Disconnect(true);
                }
            }

            return Task.CompletedTask;
        }
    }
}
