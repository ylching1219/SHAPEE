using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.Security.Claims;
using System.Text;

namespace SHAPEE
{
    public class Helper(IHttpContextAccessor ct)
    {
        private readonly PasswordHasher<object> ph = new();

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
                new Claim(ClaimTypes.Name,fullName),
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

        public static byte[] ExportToCsv<T>(IEnumerable<T> data)
        {
            var sb = new StringBuilder();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // Header
            sb.AppendLine(string.Join(",", props.Select(p => p.Name)));

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

    }
}
