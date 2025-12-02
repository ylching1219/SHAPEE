using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

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

        public void SignIn(string id,string fullName, string role, bool rememberMe) { 
            List<Claim> claims = new()
            {
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim(ClaimTypes.Name,fullName),
                new Claim(ClaimTypes.Role, role)
            };

            ClaimsIdentity identity = new(claims,"Cookies");
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

    }
}
