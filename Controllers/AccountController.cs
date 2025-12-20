using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHAPEE.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace SHAPEE.Controllers;

public class AccountController(DB db, Helper hp) : Controller
{
    //Login
    [HttpGet]
    public IActionResult Login(string? returnURL)
    {
        ViewData["ReturnUrl"] = returnURL;
        ViewData["Title"] = "Login";
        return View();
    }

    //Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginVM vm, string? returnURL)
    {
        var u = db.Users.Find(vm.Id);

        if (u != null)
        {
            if (u.Status == "Locked") 
            { 
                ModelState.AddModelError("", "Your account is locked due to too many failed attempts. Please contact Administration.");
                return View(vm);
            }
           
        }
        else
        {
            ModelState.AddModelError("", "This ID is not registered yet. Please contact Administration.");
            return View(vm);
        }

        if (u == null || !hp.VerifyPassword(u.Hash, vm.Password))
        {
            if (u != null)
            {
                u.LoginAttempts++;

                if (u.LoginAttempts >= 3)
                {
                    u.Status = "Locked";
                    ModelState.AddModelError("", "Too many failed attempts. Your account has been locked.");
                }
                else
                {
                    ModelState.AddModelError("",
                        "Please check your ID or password and try again. (" + (3 - u.LoginAttempts) + " attempt(s) remaining)");
                }

                db.SaveChanges(); 
            }
            else
            {
                ModelState.AddModelError("", "Please check your ID or password and try again.");
            }

            return View(vm);
        }

        u!.LoginAttempts = 0;
        if (u.Status != "LockedPayment")
            u.Status = "Active";
        db.SaveChanges();

        hp.SignIn(u.Id, u.FullName, u.Role, vm.RememberMe);

        //check payment reminders on login
        if (u.Role == "Student")
        {
            var studentId = u.Id; //capture the ID for use in background task
            _ = Task.Run(async () =>
            {
                try
                {
                    //create new scope for background task
                    using var scope = HttpContext.RequestServices.CreateScope();
                    var scopedDb = scope.ServiceProvider.GetRequiredService<DB>();
                    var scopedHelper = scope.ServiceProvider.GetRequiredService<Helper>();
                    
                    //check payment reminders
                    var today = DateTime.Today;
                    var threeDaysFromNow = today.AddDays(3);

                    var invoicesToRemind = scopedDb.Invoices
                        .Include(i => i.Student)
                        .Where(i => i.StudentId == studentId
                            && !i.IsPaid
                            && i.DueDate.Date >= today  //not overdue
                            && i.DueDate.Date <= threeDaysFromNow  //due in 3 days or less
                            && i.Student != null
                            && !string.IsNullOrEmpty(i.Student.Email))
                        .ToList();

                    foreach (var invoice in invoicesToRemind)
                    {
                        //check if reminder notification was already sent today
                        var reminderSentToday = scopedDb.Notification
                            .Any(n => n.UserId == studentId
                                && n.Title == "Payment Reminder"
                                && n.Message.Contains(invoice.Id)
                                && n.CreatedAt.Date == today);

                        if (reminderSentToday)
                            continue; //skip if already sent today

                        var student = invoice.Student;
                        var studentName = student.FullName?.Trim() ?? student.Id;
                        
                        //calculate days until due for the message
                        var daysUntilDue = (invoice.DueDate.Date - today).Days;
                        var daysText = daysUntilDue == 0 ? "today" : 
                                       daysUntilDue == 1 ? "1 day" : 
                                       $"{daysUntilDue} days";

                        //send email
                        await scopedHelper.SendPaymentReminderEmail(
                            student.Email,
                            studentName,
                            invoice.Id,
                            invoice.TotalAmount,
                            invoice.DueDate
                        );

                        //send notification regardless of email success
                        HomeController.CreateNotificationForUser(
                            scopedDb,
                            studentId,
                            "Payment Reminder",
                            $"Your invoice {invoice.Id} (RM {invoice.TotalAmount:N2}) is due in {daysText} on {invoice.DueDate:dd MMM yyyy}. Please make payment to avoid late fees.",
                            "warning",
                            "/Student/CurrentBilling"
                        );
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing payment reminder on login: {ex.Message}");
                }
            });
        }

        //if payment locked student log in force it into Current Billing tab
        if (u.Role == "Student" && u.Status == "LockedPayment")
        {
            TempData["PaymentLockMessage"] = "Your account is locked due to overdue fees. Please complete payment to regain access.";
            return RedirectToAction("CurrentBilling", "Student");
        }

        // Redirection based on role
        if (!string.IsNullOrEmpty(returnURL))
        {
            return LocalRedirect(returnURL);
        }

        return u.Role switch
        {
            "Admin" => RedirectToAction("Dashboard", "Admin"),
            "Staff" => RedirectToAction("Dashboard", "Staff"),
            "Student" => RedirectToAction("Dashboard", "Student"),
            _ => RedirectToAction("Index", "Home"),
        };
    }

    //ForgotPassword
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        ViewData["Title"] = "Forgot Password";
        return View();
    }

    //Verify User ID and Email
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyUserEmail(string userId, string email)
    {
        try
        {
            userId = userId?.Trim();
            email = email?.Trim().ToLower();

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "User ID and Email are required.";
                return RedirectToAction("ForgotPassword");
            }

            // Find user by ID
            var user = await db.Users.FindAsync(userId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "User ID not found.";
                return RedirectToAction("ForgotPassword");
            }

            // Verify email matches
            if (user.Email.ToLower() != email)
            {
                TempData["ErrorMessage"] = "Email does not match the User ID.";
                return RedirectToAction("ForgotPassword");
            }

            if (user.Status == "Suspended")
            {
                TempData["ErrorMessage"] = "Your account is suspended. Please contact administrator.";
                return RedirectToAction("ForgotPassword");
            }

            // Generate 6-digit OTP
            var helper = new Helper(HttpContext.RequestServices.GetRequiredService<IHttpContextAccessor>(),
                                   HttpContext.RequestServices.GetRequiredService<IConfiguration>());
            var otp = helper.GenerateOTP();

            var resetToken = new ResetPassToken
            {
                Id = otp,
                UserId = userId,
                CreatedAt = DateTime.Now,
                ExpiryAt = DateTime.Now.AddMinutes(10),
                Used = false
            };

            db.ResetPassTokens.Add(resetToken);
            await db.SaveChangesAsync();

            var emailSent = await helper.SendOTPEmail(user.Email, user.FullName, otp);

            if (!emailSent)
            {
                TempData["ErrorMessage"] = "Failed to send verification code. Please try again.";
                return RedirectToAction("ForgotPassword");
            }

            TempData["SuccessMessage"] = "Verification code sent to your email!";
            TempData["UserId"] = userId;
            return RedirectToAction("VerifyOTP");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
            return RedirectToAction("ForgotPassword");
        }
    }

    //VerifyOTP
    public IActionResult VerifyOTP()
    {
        if (TempData["UserId"] == null)
        {
            return RedirectToAction("ForgotPassword");
        }

        TempData.Keep("UserId");
        return View();
    }

    //Verify OTP
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOTP(string otp)
    {
        try
        {
            var userId = TempData["UserId"]?.ToString();
            otp = otp?.Trim();

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(otp))
            {
                TempData["ErrorMessage"] = "Invalid request.";
                return RedirectToAction("ForgotPassword");
            }

            // Find token by Id (which is the OTP) and UserId
            var resetToken = await db.ResetPassTokens
                .FirstOrDefaultAsync(t => t.Id == otp && t.UserId == userId && !t.Used);

            if (resetToken == null)
            {
                TempData["ErrorMessage"] = "Invalid verification code.";
                TempData["UserId"] = userId;
                return RedirectToAction("VerifyOTP");
            }

            if (DateTime.Now > resetToken.ExpiryAt)
            {
                TempData["ErrorMessage"] = "Verification code has expired. Please request a new one.";
                return RedirectToAction("ForgotPassword");
            }

            resetToken.Used = true;
            await db.SaveChangesAsync();

            return RedirectToAction("ResetPassword", new { token = resetToken.Id });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
            TempData.Keep("UserId");
            return RedirectToAction("VerifyOTP");
        }
    }

    //ResetPassword
    public async Task<IActionResult> ResetPassword(string token)
    {
        var resetToken = await db.ResetPassTokens
            .FirstOrDefaultAsync(t => t.Id == token && t.Used);

        if (resetToken == null || DateTime.Now > resetToken.ExpiryAt.AddMinutes(5))
        {
            TempData["ErrorMessage"] = "Invalid or expired reset link.";
            return RedirectToAction("ForgotPassword");
        }

        ViewBag.TokenId = token;
        ViewBag.UserId = resetToken.UserId;
        return View();
    }

    //Reset Password
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string tokenId, string newPassword, string confirmPassword)
    {
        try
        {
            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                TempData["ErrorMessage"] = "Password must be at least 6 characters long.";
                return RedirectToAction("ResetPassword", new { token = tokenId });
            }

            if (newPassword != confirmPassword)
            {
                TempData["ErrorMessage"] = "Passwords do not match.";
                return RedirectToAction("ResetPassword", new { token = tokenId });
            }

            var resetToken = await db.ResetPassTokens.FindAsync(tokenId);

            if (resetToken == null || !resetToken.Used)
            {
                TempData["ErrorMessage"] = "Invalid reset token.";
                return RedirectToAction("ForgotPassword");
            }

            // Find user
            var user = await db.Users.FindAsync(resetToken.UserId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("ForgotPassword");
            }

            // Hash new password
            var passwordHasher = new PasswordHasher<User>();
            user.Hash = passwordHasher.HashPassword(user, newPassword);

            // Reset login attempts
            user.LoginAttempts = 0;

            await db.SaveChangesAsync();

            //create notification for password reset
            HomeController.CreateNotificationForUser(
                db,
                user.Id,
                "Password Reset Successful",
                "Your account password has been reset successfully. If you didn't make this change, please contact administration immediately.",
                "warning",
                "/Account/ChangePassword"
            );

            TempData["ResetSuccess"] = "Password reset successfully! You can now login with your new password.";
            return RedirectToAction("Login");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
            return RedirectToAction("ResetPassword", new { token = tokenId });
        }
    }

    //ChangePassword
    [Authorize]
    public IActionResult ChangePassword()
    {
        ViewData["Title"] = "Change Password";
        return View();
    }

    //ChangePassword
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        // Validation
        if (string.IsNullOrEmpty(currentPassword))
        {
            ModelState.AddModelError("currentPassword", "Current password is required.");
        }

        if (string.IsNullOrEmpty(newPassword))
        {
            ModelState.AddModelError("newPassword", "New password is required.");
        }
        else if (newPassword.Length < 6)
        {
            ModelState.AddModelError("newPassword", "Password must be at least 6 characters long.");
        }

        if (string.IsNullOrEmpty(confirmPassword))
        {
            ModelState.AddModelError("confirmPassword", "Please confirm your password.");
        }
        else if (newPassword != confirmPassword)
        {
            ModelState.AddModelError("confirmPassword", "New password and confirmation password do not match.");
        }

        if (!ModelState.IsValid)
        {
            return View();
        }

        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await db.Users.FindAsync(userId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("ChangePassword");
            }

            // Verify current password
            var passwordHasher = new PasswordHasher<User>();
            var verificationResult = passwordHasher.VerifyHashedPassword(user, user.Hash, currentPassword);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError("currentPassword", "Current password is incorrect.");
                return View();
            }

            // Hash new password
            var newPasswordHash = passwordHasher.HashPassword(user, newPassword);
            user.Hash = newPasswordHash;

            await db.SaveChangesAsync();

            //create notification for password change
            HomeController.CreateNotificationForUser(
                db,
                userId,
                "Password Changed",
                "Your account password was updated successfully. If you didn't make this change, please contact administration immediately.",
                "warning",
                "/Account/ChangePassword"
            );

            TempData["SuccessMessage"] = "Password changed successfully!";

            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Profile", "Admin");
            }
            else if (User.IsInRole("Staff"))
            {
                return RedirectToAction("Profile", "Staff");
            }
            else if (User.IsInRole("Student"))
            {
                return RedirectToAction("Profile", "Student");
            }

            return RedirectToAction("Login", "Account");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"An error occurred while changing password: {ex.Message}";
            return RedirectToAction("ChangePassword");
        }
    }

    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Access Denied";
        return View();
    }

    //Logout
    public IActionResult Logout(string? returnURL)
    {
        hp.SignOut();
        TempData["LogoutSuccess"] = "true";
        return RedirectToAction("Index", "Home", new { logout = "success" });
    }
}