using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.UI.Services;
namespace WebMTB.Service
{
    public class EmailService : IEmailSender
    {
        private readonly IConfiguration _config;
        public EmailService(IConfiguration config)
        {
            _config = config;
        }
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var emailSettings = _config.GetSection("EmailSettings");
            string mailFrom = emailSettings["Mail"];
            string mailPw = emailSettings["Password"];
            string mailHost = emailSettings["Host"];
            int mailPort = int.Parse(emailSettings["Port"]);

            var client = new SmtpClient(mailHost, mailPort)
            {
                Credentials = new NetworkCredential(mailFrom, mailPw),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(mailFrom, emailSettings["DisplayName"]),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };

            mailMessage.To.Add(email);

            await client.SendMailAsync(mailMessage);
        }

    }
}
