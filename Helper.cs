using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Security.Claims;
using System.Text;

namespace SHAPEE
{
    public class Helper
    {
        private readonly IHttpContextAccessor ct;
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher<object> ph = new();

        public Helper(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            ct = httpContextAccessor;
            _configuration = configuration;
        }

        public string HashPassword(string password)
        {
            return ph.HashPassword(0, password);
        }

        public bool VerifyPassword(string hash, string password)
        {
            return ph.VerifyHashedPassword(0, hash, password)
                == PasswordVerificationResult.Success;
        }

        public void SignIn(string id, string fullName, string role, bool rememberMe)
        {
            List<Claim> claims = new()
            {
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim(ClaimTypes.Name, fullName),
                new Claim(ClaimTypes.Role, role)
            };
            ClaimsIdentity identity = new(claims, "Cookies");
            ClaimsPrincipal principal = new(identity);
            AuthenticationProperties properties = new()
            {
                IsPersistent = rememberMe
            };
            Console.WriteLine("Signing in user with ID: " + id + " and Role: " + role);
            ct.HttpContext!.SignInAsync(principal, properties);
        }

        public void SignOut()
        {
            ct.HttpContext!.SignOutAsync();
        }

        // Email functionality
        public async Task<bool> SendAccountCreationEmail(string toEmail, string userName, string userId, string password, string userType)
        {
            try
            {
                var smtpUser = _configuration["Smtp:User"];
                var smtpPass = _configuration["Smtp:Pass"];
                var smtpName = _configuration["Smtp:Name"];
                var smtpHost = _configuration["Smtp:Host"];
                var smtpPort = int.Parse(_configuration["Smtp:Port"]);

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(smtpUser, smtpName),
                    Subject = "Your TARUMT Account Has Been Created",
                    Body = GetEmailBody(userName, userId, password, userType),
                    IsBodyHtml = true
                };

                mailMessage.To.Add(new MailAddress(toEmail));

                using var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email sending failed: {ex.Message}");
                return false;
            }
        }

        private string GetEmailBody(string userName, string userId, string password, string userType)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
        }}
        .container {{
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            border: 1px solid #ddd;
            border-radius: 5px;
        }}
        .header {{
            background-color: #4e73df;
            color: white;
            padding: 20px;
            text-align: center;
            border-radius: 5px 5px 0 0;
        }}
        .content {{
            padding: 20px;
            background-color: #f8f9fc;
        }}
        .credentials {{
            background-color: #fff;
            padding: 15px;
            border-left: 4px solid #4e73df;
            margin: 20px 0;
        }}
        .credentials strong {{
            color: #4e73df;
        }}
        .warning {{
            background-color: #fff3cd;
            border: 1px solid #ffc107;
            padding: 15px;
            margin: 20px 0;
            border-radius: 5px;
        }}
        .footer {{
            text-align: center;
            padding: 20px;
            color: #666;
            font-size: 12px;
        }}
        .button {{
            display: inline-block;
            padding: 12px 30px;
            background-color: #4e73df;
            color: white;
            text-decoration: none;
            border-radius: 5px;
            margin: 20px 0;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>Welcome to TARUMT!</h2>
        </div>
        <div class='content'>
            <p>Dear <strong>{userName}</strong>,</p>
            
            <p>Your {userType} account has been successfully created. Below are your login credentials:</p>
            
            <div class='credentials'>
                <p><strong>User ID:</strong> {userId}</p>
                <p><strong>Password:</strong> {password}</p>
            </div>
            
            <div class='warning'>
                <strong>⚠️ Important Security Notice:</strong>
                <ul>
                    <li>Please change your password after your first login</li>
                    <li>Do not share your credentials with anyone</li>
                    <li>Keep this email secure or delete it after changing your password</li>
                </ul>
            </div>
            
            <p>If you have any questions or need assistance, please contact the system administrator.</p>
            
            <p>Best regards,<br>
            <strong>TARUMT Administration</strong></p>
        </div>
        <div class='footer'>
            <p>This is an automated email. Please do not reply to this message.</p>
            <p>&copy; 2025 TARUMT. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
        }

        public string GenerateOTP()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        public async Task<bool> SendOTPEmail(string toEmail, string userName, string otp)
        {
            try
            {
                var smtpUser = _configuration["Smtp:User"];
                var smtpPass = _configuration["Smtp:Pass"];
                var smtpName = _configuration["Smtp:Name"];
                var smtpHost = _configuration["Smtp:Host"];
                var smtpPort = int.Parse(_configuration["Smtp:Port"]);

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(smtpUser, smtpName),
                    Subject = "Password Reset - Verification Code",
                    Body = GetOTPEmailBody(userName, otp),
                    IsBodyHtml = true
                };

                mailMessage.To.Add(new MailAddress(toEmail));

                using var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OTP email sending failed: {ex.Message}");
                return false;
            }
        }

        private string GetOTPEmailBody(string userName, string otp)
        {
            return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{
                        font-family: Arial, sans-serif;
                        line-height: 1.6;
                        color: #333;
                    }}
                    .container {{
                        max-width: 600px;
                        margin: 0 auto;
                        padding: 20px;
                        border: 1px solid #ddd;
                        border-radius: 5px;
                    }}
                    .header {{
                        background-color: #4e73df;
                        color: white;
                        padding: 20px;
                        text-align: center;
                        border-radius: 5px 5px 0 0;
                    }}
                    .content {{
                        padding: 20px;
                        background-color: #f8f9fc;
                    }}
                    .otp-box {{
                        background-color: #fff;
                        padding: 20px;
                        text-align: center;
                        margin: 20px 0;
                        border: 2px dashed #4e73df;
                        border-radius: 5px;
                    }}
                    .otp-code {{
                        font-size: 32px;
                        font-weight: bold;
                        color: #4e73df;
                        letter-spacing: 8px;
                    }}
                    .warning {{
                        background-color: #fff3cd;
                        border: 1px solid #ffc107;
                        padding: 15px;
                        margin: 20px 0;
                        border-radius: 5px;
                    }}
                    .footer {{
                        text-align: center;
                        padding: 20px;
                        color: #666;
                        font-size: 12px;
                    }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h2>Password Reset Request</h2>
                    </div>
                    <div class='content'>
                        <p>Dear <strong>{userName}</strong>,</p>
            
                        <p>We received a request to reset your password. Use the verification code below to proceed:</p>
            
                        <div class='otp-box'>
                            <p style='margin: 0; color: #666; font-size: 14px;'>Your Verification Code</p>
                            <div class='otp-code'>{otp}</div>
                            <p style='margin: 10px 0 0 0; color: #999; font-size: 12px;'>Valid for 10 minutes</p>
                        </div>
            
                        <div class='warning'>
                            <strong>⚠️ Security Notice:</strong>
                            <ul>
                                <li>This code will expire in 10 minutes</li>
                                <li>Do not share this code with anyone</li>
                                <li>If you didn't request this, please ignore this email</li>
                            </ul>
                        </div>
            
                        <p>Best regards,<br>
                        <strong>TARUMT Administration</strong></p>
                    </div>
                    <div class='footer'>
                        <p>This is an automated email. Please do not reply to this message.</p>
                        <p>&copy; 2025 TARUMT. All rights reserved.</p>
                    </div>
                </div>
            </body>
            </html>";
        }

        public static byte[] ExportToCsv<T>(IEnumerable<T> data)
        {
            var sb = new StringBuilder();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            // Header: use DisplayAttribute if available
            var headers = props.Select(p =>
            {
                var displayAttr = p.GetCustomAttribute<DisplayAttribute>();
                return displayAttr != null ? displayAttr.Name : p.Name;
            });
            // Header
            sb.AppendLine(string.Join(",", headers));
            // Rows
            foreach (var item in data)
            {
                var values = props.Select(p =>
                {
                    var value = p.GetValue(item, null);
                    return Escape(value?.ToString());
                });
                sb.AppendLine(string.Join(",", values));
            }
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            value = value.Replace("\"", "\"\"");
            return value.Contains(',') || value.Contains('\n')
                ? $"\"{value}\""
                : value;
        }

        public async Task<bool> SendPaymentReceiptEmail(
            string toEmail,
            string studentName,
            string invoiceId,
            string description,
            decimal amountPaid,
            string receiptId,
            string receiptLink)
        {
            try
            {
                var smtpUser = _configuration["Smtp:User"];
                var smtpPass = _configuration["Smtp:Pass"];
                var smtpName = _configuration["Smtp:Name"];
                var smtpHost = _configuration["Smtp:Host"];
                var smtpPort = int.Parse(_configuration["Smtp:Port"]);

                var body = $@"
        <!DOCTYPE html>
        <html>
        <body style='font-family: Arial, sans-serif; color:#333;'>
        <div style='max-width: 640px; margin: 0 auto; border:1px solid #e5e5e5; border-radius:8px; overflow:hidden;'>
            <div style='background:#4e73df; color:#fff; padding:16px 20px;'>
            <h2 style='margin:0;'>Payment Successful</h2>
            </div>
            <div style='padding:20px; background:#f8f9fc;'>
            <p>Dear <b>{studentName}</b>,</p>
            <p>Your payment has been received successfully.</p>

            <table style='width:100%; border-collapse:collapse; background:#fff;'>
                <tr><td style='padding:10px; border-bottom:1px solid #eee;'><b>Receipt</b></td><td style='padding:10px; border-bottom:1px solid #eee;'>{receiptId}</td></tr>
                <tr><td style='padding:10px; border-bottom:1px solid #eee;'><b>Invoice</b></td><td style='padding:10px; border-bottom:1px solid #eee;'>{invoiceId}</td></tr>
                <tr><td style='padding:10px; border-bottom:1px solid #eee;'><b>Description</b></td><td style='padding:10px; border-bottom:1px solid #eee;'>{description}</td></tr>
                <tr><td style='padding:10px;'><b>Amount Paid</b></td><td style='padding:10px;'>RM {amountPaid:N2}</td></tr>
            </table>

            <p style='margin-top:16px;'>
                Download your receipt (PDF):
                <a href='{receiptLink}'>{receiptLink}</a>
            </p>

            <p style='margin-top:16px;'>Regards,<br/><b>TARUMT Finance</b></p>
            </div>
            <div style='padding:12px 20px; font-size:12px; color:#666;'>
            This is an automated email. Please do not reply.
            </div>
        </div>
        </body>
        </html>";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(smtpUser, smtpName),
                    Subject = $"Payment Successful - {invoiceId}",
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(new MailAddress(toEmail));

                using var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Payment email sending failed: {ex.Message}");
                return false;
            }
        }
    }
}