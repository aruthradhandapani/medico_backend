using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace medico_backend.Services
{
    // ── Settings model ──────────────────────────────────────────────────────────
    public class SmtpSettings
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public bool EnableSsl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string FromEmail { get; set; }
        public string FromName { get; set; }
    }

    // ── Interface ───────────────────────────────────────────────────────────────
    public interface IEmailService
    {
        Task SendOtpEmailAsync(string toEmail, string otp, DateTime otpExpiry);
    }

    // ── Implementation ──────────────────────────────────────────────────────────
    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _smtp;

        public EmailService(IOptions<SmtpSettings> smtpSettings)
        {
            _smtp = smtpSettings.Value;
        }

        public async Task SendOtpEmailAsync(string toEmail, string otp, DateTime otpExpiry)
        {
            var message = new MailMessage
            {
                From = new MailAddress(_smtp.FromEmail, _smtp.FromName),
                Subject = "Password Reset OTP",
                IsBodyHtml = true,
                Body = $@"
                    <div style='font-family:Arial,sans-serif;max-width:480px;margin:auto;
                                border:1px solid #e5e7eb;border-radius:8px;padding:32px;'>
                        <h2 style='color:#1f2937;margin-bottom:8px;'>Password Reset Request</h2>
                        <p style='color:#6b7280;'>Use the OTP below to reset your password.
                           It expires in <strong>10 minutes</strong>.</p>

                        <div style='text-align:center;margin:28px 0;'>
                            <span style='font-size:36px;font-weight:700;letter-spacing:12px;
                                         color:#4F46E5;background:#eef2ff;padding:16px 24px;
                                         border-radius:8px;display:inline-block;'>
                                {otp}
                            </span>
                        </div>

                        <p style='color:#6b7280;font-size:13px;'>
                            Valid until <strong>{otpExpiry:yyyy-MM-dd HH:mm:ss} UTC</strong>.
                        </p>
                        <p style='color:#9ca3af;font-size:12px;margin-top:24px;'>
                            If you did not request a password reset, please ignore this email.
                        </p>
                    </div>"
            };

            message.To.Add(new MailAddress(toEmail));

            using var smtpClient = new SmtpClient(_smtp.Host, _smtp.Port)
            {
                Credentials = new NetworkCredential(_smtp.Username, _smtp.Password),
                EnableSsl = _smtp.EnableSsl
            };

            await smtpClient.SendMailAsync(message);
        }
    }
}