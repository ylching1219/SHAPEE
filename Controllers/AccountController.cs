using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using SHAPEE.ViewModels;
using SHAPEE.Models;

namespace SHAPEE.Controllers;

public class AccountController(DB db, Helper hp) : Controller
{
    // GET: Account/Login - Displays the login form
    [HttpGet]
    public IActionResult Login(string? returnURL)
    {
        ViewData["ReturnUrl"] = returnURL;
        ViewData["Title"] = "Login";
        return View();
    }

    // POST: Account/Login - Handles form submission and authentication
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginVM vm, string? returnURL)
    {
        var u = db.Users.Find(vm.Id);

        if (u != null && u.Status == "Locked")
        {
            ModelState.AddModelError("", "Your account is locked due to too many failed attempts. Please contact Administration.");
            return View(vm);
        }

        if (u == null || !hp.VerifyPassword(u.Hash, vm.Password))
        {
            if (u != null)
            {
                u.LoginAttempts++;

                if (u.LoginAttempts >= 3) // Lock after 3 failed attempts
                {
                    u.Status = "Locked";
                    ModelState.AddModelError("", "Too many failed attempts. Your account has been locked.");
                }
                else
                {
                    ModelState.AddModelError("",
                        "Please check your ID or password and try again. (" + (3 - u.LoginAttempts) + " attempt(s) remaining)");
                }

                db.SaveChanges(); // Persist the updated attempts/status
            }
            else
            {
                ModelState.AddModelError("", "Please check your ID or password and try again.");
            }

            return View(vm);
        }

        // 2. Successful Login Logic

        // Reset attempts and ensure active status
        u!.LoginAttempts = 0;
        u.Status = "Active";
        db.SaveChanges();

        hp.SignIn(u.Id, u.Role, vm.RememberMe);

        // Redirection based on role
        if (!string.IsNullOrEmpty(returnURL))
        {
            return LocalRedirect(returnURL);
        }

        return u.Role switch
        {
            "Admin" => RedirectToAction("Admin", "Dashboard"),
            "Staff" => RedirectToAction("Staff", "Dashboard"),
            "Student" => RedirectToAction("Student", "Dashboard"),
            _ => RedirectToAction("Index", "Home"),
        };
    }

    // GET: Account/ForgotPassword - Displays the request form
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        ViewData["Title"] = "Forgot Password";
        return View();
    }

    // POST: Account/ForgotPassword - Handles token generation and email sending (A3)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ForgotPassword(ForgotPasswordVM vm)
    {
        var u = db.Users.Find(vm.Id);

        if (u != null)
        {
            string token = Guid.NewGuid().ToString("N"); // Use a GUID for a unique token

    //        u.ResetToken = token;
    //        u.TokenExpiry = DateTime.UtcNow.AddHours(1);
    //        db.SaveChanges();

    //        string resetLink = Url.Action("ResetPassword", "Account",
    //            new { token = token, userId = u.Id }, Request.Scheme);

    //        TempData["DevResetLink"] = resetLink;
    //    }

    //    ViewData["Title"] = "Password Reset Request Sent";
    //    return View("ForgotPasswordConfirmation");
    //}

    // GET: Account/ResetPassword - Validates token and displays the reset form (A3)
    [HttpGet]
    public IActionResult ResetPassword(string token, string userId)
    {
        var u = db.Users.Find(userId);

    //    if (u == null || u.ResetToken != token || u.TokenExpiry == null || u.TokenExpiry.Value < DateTime.UtcNow)
    //    {
    //        ViewData["Title"] = "Invalid Link";
    //        ViewData["Message"] = "The password reset link is invalid or has expired. Please request a new one.";
    //        return View("Error");
    //    }

        ViewData["Title"] = "Reset Password";
        return View(new ResetPasswordVM { Token = token, UserId = userId });
    }

    //// POST: Account/ResetPassword - Handles form submission for new password (A3)
    //[HttpPost]
    //[ValidateAntiForgeryToken]
    //public IActionResult ResetPassword(ResetPasswordVM vm)
    //{
    //    if (!ModelState.IsValid)
    //    {
    //        ViewData["Title"] = "Reset Password";
    //        return View(vm);
    //    }

    //    var u = db.Users.FirstOrDefault(
    //        x => x.Id == vm.UserId && x.ResetToken == vm.Token);

    //    if (u == null || u.TokenExpiry == null || u.TokenExpiry.Value < DateTime.UtcNow)
    //    {
    //        ModelState.AddModelError("", "The reset operation failed. Please request a new link.");
    //        ViewData["Title"] = "Reset Password Failed";
    //        return View(vm);
    //    }

    //    u.Hash = hp.HashPassword(vm.NewPassword);

    //    u.ResetToken = null;
    //    u.TokenExpiry = null;
    //    u.LoginAttempts = 0;
    //    u.Status = "Active"; // Unlock the account

    //    db.SaveChanges();

    //    TempData["ResetSuccess"] = "Your password has been reset successfully. You can now log in.";
    //    return RedirectToAction("Login");
    //}

    // GET: Account/AccessDenied
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Access Denied";
        return View();
    }

    // GET: Account/Logout
    public IActionResult Logout(string? returnURL)
    {
        hp.SignOut();
        TempData["LogoutSuccess"] = "true";
        return RedirectToAction("Index", "Home", new { logout = "success" });
    }
}