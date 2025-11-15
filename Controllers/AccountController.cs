using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers;

public class AccountController : Controller
{
    // GET: Account/Login
    public IActionResult Login()
    {
        ViewData["Title"] = "Login";
        return View();
    }

    // GET: Account/AccessDenied
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Access Denied";
        return View();
    }
    
    public IActionResult ForgotPassword()
    {
        ViewData["Title"] = "Forgot Password";
        return View();
    }

    // GET: Account/ResetPassword
    public IActionResult ResetPassword()
    {
        ViewData["Title"] = "Reset Password";
        return View();
    }
}
