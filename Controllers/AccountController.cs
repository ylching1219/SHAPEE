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
}
