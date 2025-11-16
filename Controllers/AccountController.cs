using Microsoft.AspNetCore.Mvc;

namespace SHAPEE.Controllers;

public class AccountController(DB db,Helper hp) : Controller
{
    // GET: Account/Login
    public IActionResult Login(LoginVM vm,string? returnURL)
    {
        var u = db.Users.Find(vm.Id);

        if (u == null || !hp.VerifyPassword(u.Hash, vm.Password))
        {
            ModelState.AddModelError("",
                "Login credentials not matched.");
        }

        if (ModelState.IsValid)
        {
            hp.SignIn(u!.id, u.Role, vm.RememberMe);


            if (!string.IsNullOrEmpty(returnURL))
            {
                return LocalRedirect(returnURL);
            }

            if(u.Role == "Admin")
            {
                return RedirectToAction("Admin", "Dashboard");
            }
            else if(u.Role == "Staff")
            {
                return RedirectToAction("Staff", "Dashboard");
            }
            else if(u.Role == "Student")
            {
                return RedirectToAction("Student", "Dashboard");
            }
        }

        return View(vm);
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

    public IActionResult Logout(string? returnURL)
    {

        hp.SignOut();
        TempData["LogoutSuccess"] = "true";
        return RedirectToAction("Index", "Home", new { logout = "success" });
    }
}
