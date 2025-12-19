using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SHAPEE.Models;
using SHAPEE.Services;
using SHAPEE.Services;
using Stripe;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static SHAPEE.Models.DB;

namespace SHAPEE.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController(DB db, Helper hp, LeaveService _leaveService) : Controller
    {
        // Dashboard
        public IActionResult Dashboard()
        {
            ViewData["Title"] = "Admin Dashboard";
            return View();
        }

        public IActionResult StudentManagement(string? name, string? filter1, string? filter2, string? filter3)
        {
            ViewData["Title"] = "Student Management";

            var faculties = db.Faculties
                .Include(f => f.Programs)
                .OrderBy(f => f.Name)
                .ToList();
            ViewBag.Faculties = faculties;

            var programs = db.Programs
                .OrderBy(p => p.Id)
                .ToList();
            ViewBag.Programs = programs;

            var filteredStudent = db.Students
                .Include(s => s.Program)
                .AsQueryable();

            name = name?.Trim() ?? "";
            if (!string.IsNullOrEmpty(name))
            {
                filteredStudent = filteredStudent
                    .Where(s => (s.FirstName + " " + s.LastName).Contains(name));
            }

            filter1 = filter1?.Trim() ?? "";
            if (!string.IsNullOrEmpty(filter1))
            {
                filteredStudent = filteredStudent.Where(s => s.Status == filter1);
            }

            filter2 = filter2?.Trim() ?? "";
            if (!string.IsNullOrEmpty(filter2))
            {
                filteredStudent = filteredStudent.Where(s => s.Gender == filter2);
            }

            filter3 = filter3?.Trim() ?? "";
            if (!string.IsNullOrEmpty(filter3))
            {
                filteredStudent = filteredStudent.Where(s => s.ProgramId == filter3);
            }

            var result = filteredStudent
                .OrderBy(s => s.Id)
                .ToList();

            if (Request.IsAjax())
            {
                return PartialView("_Student", result);
            }

            return View(result);
        }

        public IActionResult StaffManagement(string? name, string? filter1, string? filter2, string? filter3)
        {
            ViewData["Title"] = "Staff Management";
            var filteredStaff = db.Staffs
                .Include(s => s.Faculty)
                .AsQueryable();

            name = name?.Trim() ?? "";
            if (!string.IsNullOrEmpty(name))
            {
                filteredStaff = filteredStaff
                    .Where(s => (s.FirstName + " " + s.LastName).Contains(name));
            }

            filter1 = filter1?.Trim() ?? "";
            if (!string.IsNullOrEmpty(filter1))
            {
                filteredStaff = filteredStaff.Where(s => s.Status == filter1);
            }

            filter2 = filter2?.Trim() ?? "";
            if (!string.IsNullOrEmpty(filter2))
            {
                filteredStaff = filteredStaff.Where(s => s.Gender == filter2);
            }

            filter3 = filter3?.Trim() ?? "";
            if (!string.IsNullOrEmpty(filter3))
            {
                filteredStaff = filteredStaff.Where(s => s.FacultyId == filter3);
            }

            var result = filteredStaff
                .OrderBy(s => s.Id)
                .ToList();

            if (Request.IsAjax())
            {
                return PartialView("_Staff", result);
            }

            return View(result);
        }

        public IActionResult AdminManagement(string? name)
        {
            ViewData["Title"] = "Admin Management";

            var filteredAdmin = db.Admins
                .AsQueryable();

            name = name?.Trim() ?? "";
            if (!string.IsNullOrEmpty(name))
            {
                filteredAdmin = filteredAdmin
                    .Where(s => (s.FirstName + " " + s.LastName).Contains(name));
            }

            var result = filteredAdmin
                .OrderBy(s => s.Id)
                .ToList();

            if (Request.IsAjax())
            {
                return PartialView("_Admin", result);
            }

            return View(result);
        }

        private string GenerateRandomPassword(int length = 8)
        {
            const string valid = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            var random = new Random();
            return new string(Enumerable
                .Range(1, length)
                .Select(_ => valid[random.Next(valid.Length)])
                .ToArray());
        }

        //Add Student
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStudent(string firstName, string lastName, string gender,
           string email, string contact, string facultyId, string programName,
           int yearLevel, int semester)
        {
            try
            {
                if (await db.Students.AnyAsync(s => s.Email == email.Trim().ToLower()))
                {
                    TempData["ErrorMessage"] = "Email already exists.";
                    return RedirectToAction(nameof(StudentManagement));
                }

                var lastStudent = await db.Students
                    .OrderByDescending(s => s.Id)
                    .FirstOrDefaultAsync();

                var yearPrefix = DateTime.Now.Year.ToString().Substring(2);
                string newId;

                if (lastStudent != null && lastStudent.Id.StartsWith(yearPrefix))
                {
                    int lastNumber = int.Parse(lastStudent.Id.Substring(2));
                    newId = $"{yearPrefix}{(lastNumber + 1):D5}";
                }
                else
                {
                    newId = $"{yearPrefix}00001";
                }

                var program = await db.Programs
                    .FirstOrDefaultAsync(p => p.Name == programName);

                if (program == null)
                {
                    TempData["ErrorMessage"] = "Selected program not found.";
                    return RedirectToAction(nameof(StudentManagement));
                }

                string rawPassword = GenerateRandomPassword(8);
                var passwordHasher = new PasswordHasher<User>();
                string passwordHash = passwordHasher.HashPassword(null, rawPassword);

                var student = new Student
                {
                    Id = newId,
                    FirstName = firstName.Trim(),
                    LastName = lastName.Trim(),
                    Gender = gender.Trim(),
                    Email = email.Trim().ToLower(),
                    Contact = contact?.Trim(),
                    ProgramId = program.Id,
                    YearLevel = yearLevel,
                    Semester = semester,
                    DateJoin = DateOnly.FromDateTime(DateTime.Now),
                    Hash = passwordHash,
                    LoginAttempts = 0,
                    Status = "Active"
                };

                db.Students.Add(student);
                await db.SaveChangesAsync();

                // Send email
                var emailSent = await hp.SendAccountCreationEmail(
                    student.Email,
                    student.FullName,
                    student.Id,
                    rawPassword,
                    "Student"
                );

                if (emailSent)
                {
                    TempData["SuccessMessage"] =
                        $"Student '{student.FirstName} {student.LastName}' created successfully! " +
                        $"Login credentials have been sent to {student.Email}";
                }
                else
                {
                    TempData["SuccessMessage"] =
                        $"Student '{student.FirstName} {student.LastName}' created successfully! " +
                        $"Generated password: {rawPassword} (Email sending failed)";
                }

                return RedirectToAction(nameof(StudentManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] =
                    $"Error creating student: {ex.InnerException?.Message ?? ex.Message}";
                return RedirectToAction(nameof(StudentManagement));
            }
        }



        //Edit Student
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStudent(string id, string firstName, string lastName, string gender,
            string email, string contact, string programName, int yearLevel, string status)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(firstName))
                {
                    TempData["ErrorMessage"] = "First name is required.";
                    return RedirectToAction(nameof(StudentManagement));
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    TempData["ErrorMessage"] = "Email is required.";
                    return RedirectToAction(nameof(StudentManagement));
                }

                if (string.IsNullOrWhiteSpace(gender))
                {
                    TempData["ErrorMessage"] = "Gender is required.";
                    return RedirectToAction(nameof(StudentManagement));
                }

                var existingStudent = await db.Students.FindAsync(id);
                if (existingStudent == null)
                {
                    TempData["ErrorMessage"] = "Student not found.";
                    return RedirectToAction(nameof(StudentManagement));
                }

                // Check duplicate email
                var duplicateEmail = await db.Students
                    .AnyAsync(s => s.Email == email && s.Id != id);

                if (duplicateEmail)
                {
                    TempData["ErrorMessage"] = "Email already exists for another student.";
                    return RedirectToAction(nameof(StudentManagement));
                }

                existingStudent.FirstName = firstName.Trim();
                existingStudent.LastName = lastName.Trim();
                existingStudent.Gender = gender.Trim();
                existingStudent.Email = email.Trim().ToLower();
                existingStudent.Contact = contact?.Trim();
                existingStudent.YearLevel = yearLevel;
                existingStudent.Status = status?.Trim() ?? "Active";

                if (!string.IsNullOrWhiteSpace(programName))
                {
                    var program = await db.Programs
                        .FirstOrDefaultAsync(p => p.Name == programName);

                    if (program != null)
                    {
                        existingStudent.ProgramId = program.Id;
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Selected program not found.";
                        return RedirectToAction(nameof(StudentManagement));
                    }
                }

                // Save changes to database
                db.Students.Update(existingStudent);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Student '{existingStudent.FullName}' updated successfully!";
                return RedirectToAction(nameof(StudentManagement));
            }
            catch (DbUpdateConcurrencyException)
            {
                // Check if student still exists
                if (!await db.Students.AnyAsync(s => s.Id == id))
                {
                    TempData["ErrorMessage"] = "Student no longer exists.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Concurrency error. The student was updated by another user. Please try again.";
                }
                return RedirectToAction(nameof(StudentManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating student: {ex.Message}";
                return RedirectToAction(nameof(StudentManagement));
            }
        }

        //Delete Student
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStudent(string id)
        {
            try
            {
                var student = await db.Students.FindAsync(id);

                if (student == null)
                {
                    TempData["ErrorMessage"] = "Student not found.";
                    return RedirectToAction(nameof(StudentManagement));
                }

                var studentName = student.FullName;

                db.Students.Remove(student);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Student '{studentName}' deleted successfully!";
                return RedirectToAction(nameof(StudentManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting student: {ex.Message}";
                return RedirectToAction(nameof(StudentManagement));
            }
        }

        //Suspend Student
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspendStudent(string id)
        {
            try
            {
                var student = await db.Students.FindAsync(id);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found." });
                }

                // Toggle status between Active and Suspended
                if (student.Status?.Trim().Equals("Suspended", StringComparison.OrdinalIgnoreCase) == true)
                {
                    student.Status = "Active";
                }
                else
                {
                    student.Status = "Suspended";
                }

                db.Students.Update(student);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = $"Student status updated to {student.Status}." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        //Add Staff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(string firstName, string lastName, string gender,
           string email, string contact, string facultyId)
        {
            try
            {
                if (await db.Staffs.AnyAsync(s => s.Email == email.Trim().ToLower()))
                {
                    TempData["ErrorMessage"] = "Email already exists.";
                    return RedirectToAction(nameof(StaffManagement));
                }

                var lastStaff = await db.Staffs
                    .OrderByDescending(s => s.Id)
                    .FirstOrDefaultAsync();

                string newId = (lastStaff != null && lastStaff.Id.StartsWith("STF"))
                    ? $"STF{(int.Parse(lastStaff.Id.Substring(3)) + 1):D4}"
                    : "STF0001";

                string rawPassword = GenerateRandomPassword(8);
                var passwordHasher = new PasswordHasher<User>();
                string passwordHash = passwordHasher.HashPassword(null, rawPassword);

                var staff = new Staff
                {
                    Id = newId,
                    FirstName = firstName.Trim(),
                    LastName = lastName.Trim(),
                    Gender = gender.Trim(),
                    Email = email.Trim().ToLower(),
                    Contact = contact?.Trim(),
                    FacultyId = facultyId.Trim(),
                    DateJoin = DateOnly.FromDateTime(DateTime.Now),
                    Hash = passwordHash,
                    LoginAttempts = 0,
                    Status = "Active"
                };

                db.Staffs.Add(staff);
                await db.SaveChangesAsync();

                // Send email
                var emailSent = await hp.SendAccountCreationEmail(
                    staff.Email,
                    staff.FullName,
                    staff.Id,
                    rawPassword,
                    "Staff"
                );

                if (emailSent)
                {
                    TempData["SuccessMessage"] =
                        $"Staff '{staff.FirstName} {staff.LastName}' created successfully! " +
                        $"Login credentials have been sent to {staff.Email}";
                }
                else
                {
                    TempData["SuccessMessage"] =
                        $"Staff '{staff.FirstName} {staff.LastName}' created successfully! " +
                        $"Generated password: {rawPassword} (Email sending failed)";
                }

                return RedirectToAction(nameof(StaffManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] =
                    $"Error creating staff: {ex.InnerException?.Message ?? ex.Message}";
                return RedirectToAction(nameof(StaffManagement));
            }
        }

        //Edit Staff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStaff(string id, string firstName, string lastName, string gender,
            string email, string contact, string facultyId)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(firstName))
                {
                    TempData["ErrorMessage"] = "First name is required.";
                    return RedirectToAction(nameof(StaffManagement));
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    TempData["ErrorMessage"] = "Email is required.";
                    return RedirectToAction(nameof(StaffManagement));
                }

                if (string.IsNullOrWhiteSpace(gender))
                {
                    TempData["ErrorMessage"] = "Gender is required.";
                    return RedirectToAction(nameof(StaffManagement));
                }

                // Check if staff exists
                var existingStaff = await db.Staffs.FindAsync(id);
                if (existingStaff == null)
                {
                    TempData["ErrorMessage"] = "Staff not found.";
                    return RedirectToAction(nameof(StaffManagement));
                }

                // Check for duplicate email (excluding current staff)
                var duplicateEmail = await db.Staffs
                    .AnyAsync(s => s.Email == email && s.Id != id);

                if (duplicateEmail)
                {
                    TempData["ErrorMessage"] = "Email already exists for another staff.";
                    return RedirectToAction(nameof(StaffManagement));
                }

                // Update staff properties
                existingStaff.FirstName = firstName.Trim();
                existingStaff.LastName = lastName.Trim();
                existingStaff.Gender = gender.Trim();
                existingStaff.Email = email.Trim().ToLower();
                existingStaff.Contact = contact?.Trim();
                existingStaff.FacultyId = facultyId.Trim();

                // Save changes to database
                db.Staffs.Update(existingStaff);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Staff '{existingStaff.FullName}' updated successfully!";
                return RedirectToAction(nameof(StaffManagement));
            }
            catch (DbUpdateConcurrencyException)
            {
                // Check if staff still exists
                if (!await db.Staffs.AnyAsync(s => s.Id == id))
                {
                    TempData["ErrorMessage"] = "Staff no longer exists.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Concurrency error. The staff was updated by another user. Please try again.";
                }
                return RedirectToAction(nameof(StaffManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating student: {ex.Message}";
                return RedirectToAction(nameof(StaffManagement));
            }
        }

        //Delete Staff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStaff(string id)
        {
            try
            {
                var staff = await db.Staffs.FindAsync(id);

                if (staff == null)
                {
                    TempData["ErrorMessage"] = "Staff not found.";
                    return RedirectToAction(nameof(StaffManagement));
                }

                var staffName = staff.FullName;

                db.Staffs.Remove(staff);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Staff '{staffName}' deleted successfully!";
                return RedirectToAction(nameof(StaffManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting staff: {ex.Message}";
                return RedirectToAction(nameof(StaffManagement));
            }
        }

        //Suspend Staff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspendStaff(string id)
        {
            try
            {
                var staff = await db.Staffs.FindAsync(id);

                if (staff == null)
                {
                    return Json(new { success = false, message = "Staff not found." });
                }

                // Toggle status between Active and Suspended
                if (staff.Status?.Trim().Equals("Suspended", StringComparison.OrdinalIgnoreCase) == true)
                {
                    staff.Status = "Active";
                }
                else
                {
                    staff.Status = "Suspended";
                }

                db.Staffs.Update(staff);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = $"Staff status updated to {staff.Status}." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        //Add Admin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAdmin(string firstName, string lastName,
           string email, string contact)
        {
            try
            {
                if (await db.Admins.AnyAsync(s => s.Email == email.Trim().ToLower()))
                {
                    TempData["ErrorMessage"] = "Email already exists.";
                    return RedirectToAction(nameof(AdminManagement));
                }

                var lastAdmin = await db.Admins
                    .OrderByDescending(s => s.Id)
                    .FirstOrDefaultAsync();

                string newId = (lastAdmin != null && lastAdmin.Id.StartsWith("Admin"))
                    ? $"Admin{(int.Parse(lastAdmin.Id.Substring(5)) + 1):D2}"
                    : "Admin01";

                string rawPassword = GenerateRandomPassword(8);
                var passwordHasher = new PasswordHasher<User>();
                string passwordHash = passwordHasher.HashPassword(null, rawPassword);

                var admin = new Admin
                {
                    Id = newId,
                    FirstName = firstName.Trim(),
                    LastName = lastName.Trim(),
                    Email = email.Trim().ToLower(),
                    Contact = contact?.Trim(),
                    Hash = passwordHash,
                    LoginAttempts = 0,
                    Status = "Active"
                };

                db.Admins.Add(admin);
                await db.SaveChangesAsync();

                // Send email
                var emailSent = await hp.SendAccountCreationEmail(
                    admin.Email,
                    admin.FullName,
                    admin.Id,
                    rawPassword,
                    "Admin"
                );

                if (emailSent)
                {
                    TempData["SuccessMessage"] =
                        $"Admin '{admin.FirstName} {admin.LastName}' created successfully! " +
                        $"Login credentials have been sent to {admin.Email}";
                }
                else
                {
                    TempData["SuccessMessage"] =
                        $"Admin '{admin.FirstName} {admin.LastName}' created successfully! " +
                        $"Generated password: {rawPassword} (Email sending failed)";
                }

                return RedirectToAction(nameof(AdminManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] =
                    $"Error creating admin: {ex.InnerException?.Message ?? ex.Message}";
                return RedirectToAction(nameof(AdminManagement));
            }
        }

        //Edit Admin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAdmin(string id, string firstName, string lastName,
            string email, string contact)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(firstName))
                {
                    TempData["ErrorMessage"] = "First name is required.";
                    return RedirectToAction(nameof(AdminManagement));
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    TempData["ErrorMessage"] = "Email is required.";
                    return RedirectToAction(nameof(AdminManagement));
                }

                // Check if admin exists
                var existingAdmin = await db.Admins.FindAsync(id);
                if (existingAdmin == null)
                {
                    TempData["ErrorMessage"] = "Staff not found.";
                    return RedirectToAction(nameof(AdminManagement));
                }

                // Check for duplicate email (excluding current staff)
                var duplicateEmail = await db.Admins
                    .AnyAsync(s => s.Email == email && s.Id != id);

                if (duplicateEmail)
                {
                    TempData["ErrorMessage"] = "Email already exists for another staff.";
                    return RedirectToAction(nameof(AdminManagement));
                }

                // Update admin properties
                existingAdmin.FirstName = firstName.Trim();
                existingAdmin.LastName = lastName.Trim();
                existingAdmin.Email = email.Trim().ToLower();
                existingAdmin.Contact = contact?.Trim();

                // Save changes to database
                db.Admins.Update(existingAdmin);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Admin '{existingAdmin.FullName}' updated successfully!";
                return RedirectToAction(nameof(AdminManagement));
            }
            catch (DbUpdateConcurrencyException)
            {
                // Check if admin still exists
                if (!await db.Admins.AnyAsync(s => s.Id == id))
                {
                    TempData["ErrorMessage"] = "Admin no longer exists.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Concurrency error. The admin was updated by another user. Please try again.";
                }
                return RedirectToAction(nameof(AdminManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating student: {ex.Message}";
                return RedirectToAction(nameof(AdminManagement));
            }
        }

        //Delete Admin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAdmin(string id)
        {
            try
            {
                var admin = await db.Admins.FindAsync(id);

                if (admin == null)
                {
                    TempData["ErrorMessage"] = "Staff not found.";
                    return RedirectToAction(nameof(AdminManagement));
                }

                var adminName = admin.FullName;

                db.Admins.Remove(admin);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Admin '{adminName}' deleted successfully!";
                return RedirectToAction(nameof(AdminManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting staff: {ex.Message}";
                return RedirectToAction(nameof(AdminManagement));
            }
        }

        //Suspend Admin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspendAdmin(string id)
        {
            try
            {
                var admin = await db.Admins.FindAsync(id);

                if (admin == null)
                {
                    return Json(new { success = false, message = "Admin not found." });
                }

                // Toggle status between Active and Suspended
                if (admin.Status?.Trim().Equals("Suspended", StringComparison.OrdinalIgnoreCase) == true)
                {
                    admin.Status = "Active";
                }
                else
                {
                    admin.Status = "Suspended";
                }

                db.Admins.Update(admin);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = $"Admin status updated to {admin.Status}." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        //Admin Profile
        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var admin = await db.Users
                .OfType<Admin>()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (admin == null)
                return NotFound();

            return View("~/Views/Shared/Profile.cshtml", admin);
        }

        // Update Admin Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string firstName, string lastName,
            string email, string contact)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var admin = await db.Admins.FindAsync(userId);

                if (admin == null)
                    return NotFound();

                var emailExists = await db.Users
                    .AnyAsync(u => u.Email == email.Trim().ToLower() && u.Id != userId);

                if (emailExists)
                {
                    TempData["ErrorMessage"] = "Email already exists.";
                    return RedirectToAction("Profile");
                }

                admin.FirstName = firstName?.Trim();
                admin.LastName = lastName?.Trim();
                admin.Email = email?.Trim().ToLower();
                admin.Contact = contact?.Trim();

                await db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Profile updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while updating profile: {ex.Message}";
            }

            return RedirectToAction("Profile");
        }



        public IActionResult FacultyManagement(string? sort, string? name)
        {
            ViewData["Title"] = "Faculty Management";

            name = name?.Trim() ?? "";
            var filteredFaculties = db.Faculties
                .Where(f => f.Name.Contains(name))
                .AsQueryable();

            var facultiesQuery =
                (sort == "1") ? filteredFaculties.OrderByDescending(f => f.Id) :
                (sort == "2") ? filteredFaculties.OrderBy(f => f.Name) :
                (sort == "3") ? filteredFaculties.OrderByDescending(f => f.Name) :
                (sort == "4") ? filteredFaculties.OrderBy(f => f.CostPerCreditHour) :
                (sort == "5") ? filteredFaculties.OrderByDescending(f => f.CostPerCreditHour) :
                                     filteredFaculties.OrderBy(f => f.Id);

            var faculties = facultiesQuery.ToList();

            var vm = new FacultyManageVM
            {
                Faculties = faculties
            };

            if (Request.IsAjax())
            {
                return PartialView("_Faculty", vm);
            }

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> AddFaculty(string facultyId, string facultyName, decimal costPerCreditHour)
        {
            try
            {
                if (costPerCreditHour <= 0)
                {
                    TempData["ErrorMessage"] = "Cost Per Credit Hour must be greater than 0.";
                    return RedirectToAction(nameof(FacultyManagement));
                }

                facultyId = facultyId.Trim().ToUpper();
                facultyName = facultyName.Trim();

                bool exists = await db.Faculties.AnyAsync(f =>
                    f.Id == facultyId || f.Name == facultyName);

                if (exists)
                {
                    TempData["ErrorMessage"] = "Faculty ID or Faculty Name already exists.";
                    return RedirectToAction(nameof(FacultyManagement));
                }

                var faculty = new Faculty
                {
                    Id = facultyId,
                    Name = facultyName,
                    CostPerCreditHour = costPerCreditHour
                };

                db.Faculties.Add(faculty);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Faculty '{facultyName}' added successfully!";
                return RedirectToAction(nameof(FacultyManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] =
                    $"Error adding faculty: {ex.InnerException?.Message ?? ex.Message}";
                return RedirectToAction(nameof(FacultyManagement));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFaculty(string facultyId, string facultyName, decimal costPerCreditHour)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(facultyName))
                {
                    TempData["ErrorMessage"] = "Faculty Name is required.";
                    return RedirectToAction(nameof(FacultyManagement));
                }

                if (costPerCreditHour <= 0)
                {
                    TempData["ErrorMessage"] = "Cost Per Credit Hour must be greater than 0.";
                    return RedirectToAction(nameof(FacultyManagement));
                }

                // Fetch faculty
                var faculty = await db.Faculties.FindAsync(facultyId);

                if (faculty == null)
                {
                    TempData["ErrorMessage"] = "Faculty not found.";
                    return RedirectToAction(nameof(FacultyManagement));
                }

                // DUPLICATE NAME CHECK (exclude current faculty)
                var duplicateName = await db.Faculties
                    .AnyAsync(f => f.Name == facultyName && f.Id != facultyId);

                if (duplicateName)
                {
                    TempData["ErrorMessage"] = "Faculty Name already exists.";
                    return RedirectToAction(nameof(FacultyManagement));
                }

                // Update
                faculty.Name = facultyName.Trim();
                faculty.CostPerCreditHour = costPerCreditHour;

                db.Faculties.Update(faculty);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = "Faculty updated successfully!";
                return RedirectToAction(nameof(FacultyManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating faculty: " + ex.Message;
                return RedirectToAction(nameof(FacultyManagement));
            }
        }

        public IActionResult GetEditFacultyForm(string facultyId)
        {
            var faculty = db.Faculties.FirstOrDefault(f => f.Id == facultyId);

            if (faculty == null)
            {
                return Content("<div class='alert alert-danger m-3'>Faculty not found</div>");
            }

            var vm = new FacultyManageVM
            {
                EditFaculty = faculty
            };

            return PartialView("_FacultyEditForm", vm);
        }

        //Delete Faculty
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFaculty(string id)
        {
            try
            {
                var faculty = await db.Faculties.FindAsync(id);

                if (faculty == null)
                {
                    TempData["ErrorMessage"] = "Faculty not found.";
                    return RedirectToAction(nameof(FacultyManagement));
                }

                var facultyName = faculty.Name;

                db.Faculties.Remove(faculty);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Faculty '{facultyName}' deleted successfully!";
                return RedirectToAction(nameof(FacultyManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting faculty: {ex.Message}";
                return RedirectToAction(nameof(FacultyManagement));
            }
        }

        //YL
        //Program Management
        public IActionResult ProgramManagement(string? filter, string? name, string? editId)
        {
            ViewData["Title"] = "Program Management";
            SHAPEE.Models.Program? editProgram = null;

            if (!string.IsNullOrEmpty(editId))
            {
                editProgram = db.Programs.FirstOrDefault(p => p.Id == editId);
            }
            name = name?.Trim() ?? "";

            var programQuery = db.Programs
                .Include(p => p.Faculty)
                .Where(p => p.Name.Contains(name))
                .AsQueryable();

            if (!string.IsNullOrEmpty(filter))
            {
                programQuery = programQuery.
                    Where(p => p.FacultyId == filter)
                    .AsQueryable();
            }
            var faculty = db.Faculties.OrderBy(f => f.Id).ToList();
            ViewBag.Faculty = faculty;
            ViewBag.FacultyList = new SelectList(faculty, "Id", "Name", editProgram?.FacultyId);

            var programList = programQuery.ToList();
            var vm = new ProgramManageVM
            {
                Program = programList,
                EditProgram = editProgram,

            };

            if (!string.IsNullOrEmpty(editId))
            {
                return PartialView("_ProgramForm", vm);
            }
            if (Request.IsAjax())
            {
                return PartialView("_Program", vm);
            }
            return View(vm);
        }

        [HttpPost]
        public IActionResult AddProgram(ProgramManageVM vm)
        {
            var s = db.Programs.FirstOrDefault(p => p.Id == vm.ProgramId);

            if (s == null)
            {
                var p = new SHAPEE.Models.Program
                {
                    Id = vm.ProgramId!,
                    Name = vm.ProgramName!,
                    FacultyId = vm.FacultyId!
                };

                db.Programs.Add(p);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Program {vm.ProgramId} added successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to add new Program. Error: Program ID duplicate!";
            }
            return RedirectToAction(nameof(ProgramManagement));
        }

        [HttpPost]
        public IActionResult EditProgram(ProgramManageVM vm)
        {
            var s = db.Programs.FirstOrDefault(p => p.Id == vm.ProgramId);
            if (s == null)
            {
                TempData["ErrorMessage"] = "Program not found!";
                return RedirectToAction("ProgramManagement");
            }

            // Only update properties, never change the primary key
            s.Name = vm.ProgramName!.Trim();
            s.FacultyId = vm.FacultyId!;

            db.SaveChanges(); // tracked entity, no Update() needed

            TempData["SuccessMessage"] = $"Program {vm.ProgramId} updated successfully.";
            return RedirectToAction("ProgramManagement");
        }

        [HttpPost]
        public IActionResult DeleteProgram(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "Program ID not provided.";
                return RedirectToAction(nameof(ProgramManagement));
            }

            var program = db.Programs.FirstOrDefault(p => p.Id == id);
            if (program == null)
            {
                TempData["ErrorMessage"] = "Program not found.";
                return RedirectToAction(nameof(ProgramManagement));
            }

            try
            {
                db.Programs.Remove(program);
                db.SaveChanges();
                TempData["SuccessMessage"] = $"Program '{program.Name}' deleted successfully.";
            }
            catch (DbUpdateException)
            {

                TempData["ErrorMessage"] = $"Cannot delete program. It may be used by other records.";
            }

            return RedirectToAction(nameof(ProgramManagement));
        }

        public IActionResult ExportProgramsCsv(string? filter)
        {
            var query = db.Programs
                         .Include(p => p.Faculty)
                         .AsQueryable();



            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(p => p.FacultyId == filter);
                Console.WriteLine("Filter applied: " + filter);
            }

            var data = query
                .Select(p => new ProgramCsvVM
                {
                    ProgramId = p.Id,
                    ProgramName = p.Name,
                    FacultyName = p.Faculty.Name,
                })
                .ToList();


            var bytes = Helper.ExportToCsv(data);
            return File(bytes, "text/csv", "Programs.csv");
        }


        //Course Management
        public IActionResult CourseManagement(string? sort, string? name, string? editId)
        {
            ViewData["Title"] = "Course Management";
            Course? editCourse = null;

            if (!string.IsNullOrEmpty(editId))
            {
                editCourse = db.Courses.FirstOrDefault(p => p.Id == editId);
            }
            name = name?.Trim() ?? "";

            var courseQuery = db.Courses

                .Where(p => p.Name.Contains(name))
                .AsQueryable();

            courseQuery =
                 (sort == "1") ? courseQuery.OrderByDescending(f => f.Id) :
                 (sort == "2") ? courseQuery.OrderBy(f => f.Name) :
                 (sort == "3") ? courseQuery.OrderByDescending(f => f.Name) :
                 (sort == "4") ? courseQuery.OrderBy(f => f.CreditHours) :
                 (sort == "5") ? courseQuery.OrderByDescending(f => f.CreditHours) :
                                      courseQuery.OrderBy(f => f.Id);


            var courseList = courseQuery.ToList();
            var vm = new CourseManageVM
            {
                Courses = courseList,
                CourseId = editCourse?.Id,
                CourseName = editCourse?.Name,
                CreditHours = editCourse?.CreditHours,
                LectureHours = editCourse?.LectureHours,
                PracticalHours = editCourse?.PracticalHours,
                TutorialHours = editCourse?.TutorialHours
            };

            if (!string.IsNullOrEmpty(editId))
            {
                return PartialView("_CourseForm", vm);
            }
            if (Request.IsAjax())
            {
                return PartialView("_Course", vm);
            }
            return View(vm);
        }

        [HttpPost]
        public IActionResult AddCourse(CourseManageVM vm)
        {
            var s = db.Courses.FirstOrDefault(p => p.Id == vm.CourseId);


            if (s == null)
            {
                if (vm.CreditHours == null || vm.CreditHours <= 0)
                {
                    TempData["ErrorMessage"] = "Credit Hours must be greater than 0.";
                    return RedirectToAction(nameof(CourseManagement));
                }


                var p = new Course
                {
                    Id = vm.CourseId!,
                    Name = vm.CourseName!,
                    CreditHours = vm.CreditHours ?? 0,
                    LectureHours = vm.LectureHours ?? 1,
                    PracticalHours = vm.PracticalHours,
                    TutorialHours = vm.TutorialHours ?? 1
                };

                db.Courses.Add(p);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Course {vm.CourseId} added successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to add new Course. Error: Course ID duplicate!";
            }
            return RedirectToAction(nameof(CourseManagement));
        }

        [HttpPost]
        public IActionResult EditCourse(CourseManageVM vm)
        {
            var s = db.Courses.FirstOrDefault(p => p.Id == vm.CourseId);

            if (s == null)
            {
                TempData["ErrorMessage"] = "Course not found!";
                return RedirectToAction(nameof(CourseManagement));
            }
            if (vm.CreditHours == null || vm.CreditHours <= 0)
            {
                TempData["ErrorMessage"] = "Credit Hours must be greater than 0.";
                return RedirectToAction(nameof(CourseManagement));
            }
            // Only update properties, never change the primary key
            s.Name = vm.CourseName!.Trim();
            s.CreditHours = vm.CreditHours ?? 0;
            s.LectureHours = vm.LectureHours ?? 1;
            s.PracticalHours = vm.PracticalHours;
            s.TutorialHours = vm.TutorialHours ?? 1;

            db.SaveChanges(); // tracked entity, no Update() needed

            TempData["SuccessMessage"] = $"Course {vm.CourseId} updated successfully.";
            return RedirectToAction(nameof(CourseManagement));
        }

        [HttpPost]
        public IActionResult DeleteCourse(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "Course ID not provided.";
                return RedirectToAction(nameof(CourseManagement));
            }

            var course = db.Courses.FirstOrDefault(p => p.Id == id);
            if (course == null)
            {
                TempData["ErrorMessage"] = "Course not found.";
                return RedirectToAction(nameof(CourseManagement));
            }

            try
            {
                db.Courses.Remove(course);
                db.SaveChanges();
                TempData["SuccessMessage"] = $"Course '{course.Name}' deleted successfully.";
            }
            catch (DbUpdateException)
            {

                TempData["ErrorMessage"] = $"Cannot delete course. It may be used by other records.";
            }

            return RedirectToAction(nameof(CourseManagement));
        }

        public IActionResult ExportCourseCsv()
        {
            var query = db.Courses;


            var data = query
                .Select(p => new CourseCsvVM
                {
                    CourseId = p.Id,
                    CourseName = p.Name,
                    CreditHours = p.CreditHours,
                })
                .ToList();


            var bytes = Helper.ExportToCsv(data);
            return File(bytes, "text/csv", "Course.csv");
        }

        //Group Management
        public IActionResult GroupManagement(string? ProgramId, int? yearLevel, int? semester)
        {
            ViewData["Title"] = "Group Management";
            var groupList = db.TutorialGroups
                .Include(g => g.Program)
                .OrderBy(g => g.Id)
                .AsQueryable();

            if (!string.IsNullOrEmpty(ProgramId))
            {
                groupList = groupList.Where(g => g.ProgramId == ProgramId);
            }

            if (yearLevel != null)
            {
                groupList = groupList.Where(g => g.YearLevel == yearLevel);
            }

            if (semester != null)
            {
                groupList = groupList.Where(g => g.Semester == semester);
            }


            var program = db.Programs
                .OrderBy(p => p.Id)
                .ToList();

            ViewBag.Program = program;
            ViewBag.ProgramList = new SelectList(program, "Id", "Name");
            var vm = new GroupManageVM
            {
                Groups = groupList.ToList()
            };

            if (Request.IsAjax())
            {
                return PartialView("_Group", vm);
            }
            return View(vm);
        }

        public IActionResult AddGroup(GroupManageVM vm)
        {
            var groupExists = db.TutorialGroups.Any(g =>
                g.ProgramId == vm.ProgramId &&
                g.YearLevel == vm.YearLevel &&
                g.Semester == vm.Semester &&
                g.GroupNumber == vm.GroupNumber);

            var groupName = vm.ProgramId + "Y" + vm.YearLevel + "S" + vm.Semester + "G" + vm.GroupNumber;
            if (groupExists)
            {
                TempData["ErrorMessage"] = $"Failed to add new Group. Error: Group {groupName} already exists!";
                return RedirectToAction(nameof(GroupManagement));
            }

            var p = new TutorialGroup
            {
                GroupName = groupName,
                GroupNumber = vm.GroupNumber ?? 1,
                ProgramId = vm.ProgramId!,
                YearLevel = vm.YearLevel ?? 1,
                Semester = vm.Semester ?? 1,
                NumberOfStudents = 0
            };
            db.TutorialGroups.Add(p);
            db.SaveChanges();
            TempData["SuccessMessage"] = $"Group {p.GroupName} added successfully.";


            return RedirectToAction(nameof(GroupManagement));
        }

        public IActionResult DeleteGroup(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Group ID not provided.";
                return RedirectToAction(nameof(GroupManagement));
            }

            var group = db.TutorialGroups.FirstOrDefault(g => g.Id == id);
            if (group == null)
            {
                TempData["ErrorMessage"] = "Group not found.";
                return RedirectToAction(nameof(GroupManagement));
            }

            try
            {
                db.TutorialGroups.Remove(group);
                db.SaveChanges();
                TempData["SuccessMessage"] = $"Group '{group.GroupName}' deleted successfully.";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] = $"Cannot delete group. It may be used by other records.";
            }

            return RedirectToAction(nameof(GroupManagement));
        }

        public IActionResult ExportGroupsCsv(string? ProgramId, int? yearLevel, int? semester)
        {
            var query = db.TutorialGroups
                         .Include(p => p.Program)
                         .AsQueryable();



            if (!string.IsNullOrWhiteSpace(ProgramId))
            {
                query = query.Where(p => p.ProgramId == ProgramId);
            }

            if (yearLevel != null)
            {
                query = query.Where(p => p.YearLevel == yearLevel);
            }

            if (semester != null)
            {
                query = query.Where(p => p.Semester == semester);
            }

            var data = query
                .Select(p => new GroupCsvVM
                {
                    GroupName = p.GroupName,
                    ProgramName = p.Program.Name,
                    GroupNumber = p.GroupNumber,
                    YearLevel = p.YearLevel,
                    Semester = p.Semester,
                    NumOfStudent = p.NumberOfStudents
                })
                .ToList();


            var bytes = Helper.ExportToCsv(data);
            return File(bytes, "text/csv", "TutorialGroups.csv");
        }

        //Group Assignment 
        public IActionResult AssignGroup(string? ProgramId, int? Year, int? Sem)
        {
            var program = db.Programs
                .OrderBy(p => p.Id)
                .ToList();

            ViewBag.ProgramList = new SelectList(program, "Id", "Name");

            if (string.IsNullOrEmpty(ProgramId) || !Year.HasValue || !Sem.HasValue)
            {
                if (Request.IsAjax())
                {
                    return PartialView("_AssignGroup", new AssignGroupVM
                    {
                        Groups = new List<TutorialGroup>()
                    });
                }
            }

            var group = db.TutorialGroups
                    .Where(t => t.ProgramId == ProgramId)
                    .OrderBy(p => p.GroupName)
                    .AsQueryable();

            if (!string.IsNullOrEmpty(ProgramId))
            {
                group = group.Where(g => g.ProgramId == ProgramId);
            }
            if (Year != null)
            {
                group = group.Where(g => g.YearLevel == Year);
            }

            if (Sem != null)
            {
                group = group.Where(g => g.Semester == Sem);
            }




            var vm = new AssignGroupVM
            {
                Groups = group.ToList(),
            };
            if (Request.IsAjax())
            {
                return PartialView("_AssignGroup", vm);
            }
            return View(vm);
        }

        [HttpGet]
        public IActionResult GetStudentsByGroup(int groupId)
        {
            var students = db.Students
                .Where(s => s.TutorialGroupId == groupId)
                .Select(s => new
                {
                    s.Id,
                    FullName = s.LastName + " " + s.FirstName
                })
                .OrderBy(s => s.FullName)
                .ToList();

            return Json(students);
        }

        [HttpGet]
        public IActionResult GetUnassignedStudents(string? programId, int? year, int? semester)
        {
            var query = db.Students.AsQueryable();

            Console.WriteLine("ProramId: " + programId);
            query = query.Where(s => s.TutorialGroupId == null);

            // Apply filters if provided
            if (!string.IsNullOrEmpty(programId))
                query = query.Where(s => s.ProgramId == programId);

            if (year != null)
                query = query.Where(s => s.YearLevel == year);

            if (semester != null)
                query = query.Where(s => s.Semester == semester);

            var students = query
                .Select(s => new
                {
                    s.Id,
                    FullName = s.LastName + " " + s.FirstName
                })
                .OrderBy(s => s.FullName)
                .ToList();

            return Json(students);
        }

        [HttpPost]
        public IActionResult RemoveStudentFromGroup(string studentId)
        {
            var student = db.Students.FirstOrDefault(s => s.Id == studentId);

            if (student == null)
                return Json(new { success = false, message = "Student not found" });

            if (student.TutorialGroupId == null)
                return Json(new { success = false, message = "Student not assigned to any group" });

            var groupId = student.TutorialGroupId;

            var group = db.TutorialGroups.FirstOrDefault(g => g.Id == groupId);

            student.TutorialGroupId = null;

            if (group != null && group.NumberOfStudents > 0)
                group.NumberOfStudents--;

            db.SaveChanges();

            return Json(new
            {
                success = true,
                message = $"Student {student.FullName} was removed from group '{group!.GroupName}'.",
                newCount = group?.NumberOfStudents ?? 0
            });
        }

        [HttpPost]
        public IActionResult AddStudentToGroup([FromBody] AssignGroupVM model)
        {
            Console.WriteLine("Selected Group ID: " + model.SelectedGroupId);
            Console.WriteLine("Number of Students to Add: " + (model.StudentList?.Count ?? 0));
            if (model.StudentList == null || !model.StudentList.Any())
            {
                return Json(new { success = false, message = "No students selected." });
            }

            var group = db.TutorialGroups.FirstOrDefault(g => g.Id == model.SelectedGroupId);
            if (group == null)
            {
                return Json(new { success = false, message = "Group not found." });
            }

            int addedCount = 0;
            foreach (var student in model.StudentList)
            {
                var s = db.Students.FirstOrDefault(st => st.Id == student.Id);
                if (s != null)
                {
                    s.TutorialGroupId = model.SelectedGroupId;
                    addedCount++;
                }
            }

            group.NumberOfStudents += addedCount;
            db.SaveChanges();
            var count = db.Students.Count(s => s.TutorialGroupId == model.SelectedGroupId);

            return Json(new
            {
                success = true,
                message = $"{addedCount} student(s) added to group '{group.GroupName}' successfully.",
                newCount = count
            });
        }

        //Program Course Assignment
        public IActionResult ProgramCourse(string programId, string facultyId)
        {
            ViewData["Title"] = "Program Course Assignment";

            var f = db.Faculties
                .OrderBy(f => f.Id)
                .ToList();

            var pc = new List<ProgramCourse>();

            var course = db.Courses
                .Select(c => new
                {
                    Id = c.Id,
                    Name = c.Name,
                    Display = c.Id + " - " + c.Name
                })
                .OrderBy(c => c.Name)
                .ToList();

            if (!string.IsNullOrEmpty(programId))
            {
                pc = db.ProgramCourses
                    .Where(pc => pc.ProgramId == programId)
                    .Include(pc => pc.Program)
                    .Include(pc => pc.Course)
                    .OrderBy(pc => pc.YearLevel)
                    .ThenBy(pc => pc.Semester)
                    .ToList();
            }
            var vm = new ProgramCourseVM
            {
                ProgramCourses = pc,
                SelectedFaculty = db.Faculties
                                .FirstOrDefault(f => f.Id == facultyId),
                SelectedProgram = db.Programs
                                .FirstOrDefault(f => f.Id == programId),
                Faculties = f
            };
            ViewBag.CourseList = new SelectList(course, "Id", "Display");
            if (Request.IsAjax())
            {
                return PartialView("_ProgramCourse", vm);
            }
            return View(vm);
        }

        [HttpGet]
        public IActionResult GetProgramFromFaculty(string facultyId)
        {
            Console.WriteLine("Faculty:" + facultyId);
            var program = db.Programs
                .Where(p => p.FacultyId == facultyId)
                .ToList();

            return Json(program);
        }

        [HttpPost]
        public IActionResult AddCourseToProgram(string CourseId, string programId, int yearLevel, int semester)
        {

            var exists = db.ProgramCourses.Any(pc =>
                            pc.CourseId == CourseId &&
                            pc.ProgramId == programId);

            var course = db.Courses.FirstOrDefault(c => c.Id == CourseId);
            if (exists)
            {
                return Json(new
                {
                    success = false,
                    message = "Course " + course!.Id + " - " + course!.Name + " already assigned to this program."
                });
            }
            var pc = new ProgramCourse
            {
                CourseId = CourseId,
                ProgramId = programId,
                YearLevel = yearLevel,
                Semester = semester
            };
            db.ProgramCourses.Add(pc);
            db.SaveChanges();
            return Json(new
            {
                success = true,
                message = "Course " + course!.Id + " - " + course!.Name + " added to program successfully."
            });
        }

        [HttpPost]
        public IActionResult RemoveCourseFromProgram(string courseId, string programId)
        {
            Console.WriteLine("CourseId: " + courseId + ", ProgramId: " + programId);
            var pc = db.ProgramCourses
                     .FirstOrDefault(p => p.CourseId == courseId && p.ProgramId == programId);
            if (pc == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Program Course not found."
                });
            }
            var course = db.Courses
                        .FirstOrDefault(c => c.Id == courseId);

            db.ProgramCourses.Remove(pc);
            db.SaveChanges();
            return Json(new
            {
                success = true,
                message = "Course " + course!.Id + " - " + course!.Name + " removed from program successfully."
            });
        }

        //Class Section Management
        public IActionResult ClassSection()
        {
            ViewData["Title"] = "Class Section Management";

            var faculty = db.Faculties
                .OrderBy(f => f.Id)
                .ToList();

            var vm = new ClassSectionVM
            {
                Faculties = faculty
            };
            return View(vm);
        }

        public IActionResult GetGroupFromProgram(string programId)
        {
            var groups = db.TutorialGroups
                .Where(g => g.ProgramId == programId)
                .OrderBy(g => g.GroupName)
                .ToList();
            return Json(groups);
        }

        public IActionResult CreateClass([FromBody] ClassSectionVM vm)
        {
            if (vm == null)
                return Json(new { success = false, message = "No data received." });

            if (vm.GroupList == null || vm.GroupList.Count == 0)
                return Json(new { success = false, message = "No groups selected." });

            var possibleStartTimes = new List<TimeSpan>
            {
                new TimeSpan(8, 0, 0),
                new TimeSpan(9, 30, 0),
                new TimeSpan(11, 0, 0),
                new TimeSpan(13, 0, 0),
                new TimeSpan(14, 30, 0),
                new TimeSpan(16, 0, 0),
                new TimeSpan(18, 0, 0),
            };
            var possibleDays = new List<string> { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
            //get random start time and day
            var random = new Random();
            var randomDay = possibleDays[random.Next(possibleDays.Count)];
            var startTime = possibleStartTimes[random.Next(possibleStartTimes.Count)];

            Console.WriteLine("tier1");
            //loop each group id to create class
            foreach (var groupId in vm.GroupList!)
            {
                //check if class already exists for the group
                var group = db.TutorialGroups
                            .Include(g => g.Program)
                            .Include(g => g.Program.Faculty)
                            .FirstOrDefault(g => g.Id == groupId);


                Console.WriteLine("tier2");
                if (group != null)
                {
                    var programCourses = db.ProgramCourses
                         .Where(pc => pc.ProgramId == group.ProgramId
                         && pc.YearLevel == group.YearLevel
                         && pc.Semester == group.Semester)
                         .ToList();


                    Console.WriteLine("tier3");
                    foreach (var pc in programCourses)
                    {
                        
                        // check if lecture class exists for the course in the year level and semester
                        bool LectureExists = db.ClassSections.Any(c =>
                            c.CourseId == pc.CourseId &&
                            c.TutorialGroupId == null &&
                            c.YearLevel == group!.YearLevel &&
                            c.Sem == group.Semester &&
                            c.SectionType == "Lecture");

                        
                  
                        var staff = db.Staffs
                        .Where(s => s.FacultyId == group.Program.FacultyId)
                        .OrderBy(s => EF.Functions.Random()) // EF Core 7+ for SQL Server or SQLite
                        .FirstOrDefault();
                        Console.WriteLine("Assigned Staff to Lecture Class: " + (staff != null ? staff.FullName : "No staff available"));
                        
                      
                        Console.WriteLine("tier4");
                        if (!LectureExists)
                        {
                            //create lecture class
                            CreateClassSection();
                        }
                        else
                        {

                        }

                        if (pc.Course?.PracticalHours.GetValueOrDefault() > 0)
                        {
                            //check if practical class exists for the course and group
                            var PclassExists = db.ClassSections.Any(c =>
                            c.CourseId == pc.CourseId &&
                            c.TutorialGroupId == group.Id &&
                            c.SectionType == "Practical");

                            if (!PclassExists)
                            {
                                //create practical class  
                            }
                            else
                            {
                                Console.WriteLine("Practical class already exists for CourseId: " + pc.CourseId + ", GroupId: " + group.GroupName);
                            }
                        }

                        Console.WriteLine("tier5");

                        var TclassExists = db.ClassSections.Any(c =>
                             c.CourseId == pc.CourseId &&
                             c.TutorialGroupId == group.Id &&
                             c.SectionType == "Tutorial");

                        if (!TclassExists)
                        {

                        }
                        else
                        {
                            Console.WriteLine("Tutorial Class already exists for CourseId: " + pc.CourseId + ", GroupId: " + group!.GroupName);
                        }

                    }
                }
            }

            return Json(new { success = true, message = "Class created successfully." });
        }

        public void CreateClassSection()
        {
            // Method intentionally left empty.
        }
        //JS
        // Add this method to your AdminController.cs

        public IActionResult RoomManagement(string? sort, string? search, string? type, string? block, string? status, string? editId)
        {
            ViewData["Title"] = "Room Management";
            Room? editRoom = null;

            if (!string.IsNullOrEmpty(editId))
            {
                editRoom = db.Rooms.FirstOrDefault(r => r.Id == editId);
            }

            search = search?.Trim() ?? "";
            type = type?.Trim() ?? "";
            block = block?.Trim() ?? "";
            status = status?.Trim() ?? "";

            var filteredRooms = db.Rooms
                .Where(r => r.Id.Contains(search))
                .Where(r => type == "" || r.Type == type)
                .Where(r => block == "" || r.Block == block)
                .Where(r => status == "" || r.Status == status)
                .AsQueryable();

            var roomsQuery =
                (sort == "1") ? filteredRooms.OrderBy(r => r.Id) :
                (sort == "2") ? filteredRooms.OrderByDescending(r => r.Id) :
                (sort == "3") ? filteredRooms.OrderBy(r => r.Floor) :
                (sort == "4") ? filteredRooms.OrderByDescending(r => r.Floor) :
                                filteredRooms.OrderBy(r => r.Id);

            var rooms = roomsQuery.ToList();

            var vm = new RoomManageVM
            {
                Rooms = rooms,
                EditRoom = editRoom
            };

            if (!string.IsNullOrEmpty(editId))
            {
                return PartialView("_RoomForm", vm);
            }

            if (Request.IsAjax())
            {
                return PartialView("_Room", vm);
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRoom(string roomId, string type, string block, int? floor)
        {
            try
            {
                roomId = roomId?.Trim().ToUpper();
                type = type?.Trim();
                block = block?.Trim().ToUpper();

                // For Lecture Hall, validate and set floor to null
                if (type == "Lecture Hall")
                {
                    if (string.IsNullOrWhiteSpace(roomId))
                    {
                        return Json(new { success = false, message = "Room ID is required for Lecture Hall." });
                    }

                    if (!roomId.StartsWith("DK"))
                    {
                        return Json(new { success = false, message = "Lecture Hall Room ID must start with 'DK'." });
                    }

                    block = "DK";
                    floor = null;
                }
                else if (type == "Lab")
                {
                    // Validate Lab format: Block[0-2]0[1-5]
                    if (!floor.HasValue || floor < 0 || floor > 2)
                    {
                        return Json(new { success = false, message = "Lab floor must be between 0 and 2." });
                    }

                    var pattern = $@"^{Regex.Escape(block)}[0-2]0[1-5]$";
                    if (!Regex.IsMatch(roomId, pattern))
                    {
                        return Json(new { success = false, message = $"Invalid Lab Room ID format. Expected: {block}[0-2]0[1-5]" });
                    }

                    // Convert floor 0-2 to database floor 1-3
                    floor = floor.Value + 1;
                }
                else if (type == "Classroom")
                {
                    // Validate Classroom format: Block[1-3]0[1-5]
                    if (!floor.HasValue || floor < 1 || floor > 3)
                    {
                        return Json(new { success = false, message = "Classroom floor must be between 1 and 3." });
                    }

                    var pattern = $@"^{Regex.Escape(block)}[1-3]0[1-5]$";
                    if (!Regex.IsMatch(roomId, pattern))
                    {
                        return Json(new { success = false, message = $"Invalid Classroom Room ID format. Expected: {block}[1-3]0[1-5]" });
                    }
                }

                // Check if room already exists
                bool exists = await db.Rooms.AnyAsync(r => r.Id == roomId);

                if (exists)
                {
                    return Json(new { success = false, message = $"Room ID '{roomId}' already exists." });
                }

                var room = new Room
                {
                    Id = roomId,
                    Type = type,
                    Block = block,
                    Floor = floor,
                    Status = "Available"
                };

                db.Rooms.Add(room);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = $"Room '{roomId}' added successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error adding room: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRoom(string roomId, string status)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(status))
                {
                    return Json(new { success = false, message = "Status is required." });
                }

                var room = await db.Rooms.FindAsync(roomId);

                if (room == null)
                {
                    return Json(new { success = false, message = "Room not found." });
                }

                room.Status = status;

                db.Rooms.Update(room);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Room status updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating room: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoom(string id)
        {
            try
            {
                var room = await db.Rooms.FindAsync(id);

                if (room == null)
                {
                    TempData["ErrorMessage"] = "Room not found.";
                    return RedirectToAction(nameof(RoomManagement));
                }

                var roomId = room.Id;

                db.Rooms.Remove(room);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Room '{roomId}' deleted successfully!";
                return RedirectToAction(nameof(RoomManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting room: {ex.Message}";
                return RedirectToAction(nameof(RoomManagement));
            }
        }

        public IActionResult ExportRoomsCsv(string? search, string? type, string? block, string? status)
        {
            var query = db.Rooms.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r => r.Id.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(r => r.Type == type);
            }

            if (!string.IsNullOrWhiteSpace(block))
            {
                query = query.Where(r => r.Block == block);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.Status == status);
            }

            var data = query
                .Select(r => new RoomCsvVM
                {
                    RoomId = r.Id,
                    Type = r.Type,
                    Block = r.Block,
                    Floor = r.Floor ?? 0,
                    Status = r.Status
                })
                .ToList();

            var bytes = Helper.ExportToCsv(data);
            return File(bytes, "text/csv", "Rooms.csv");
        }

        // LeaveManagement
        public IActionResult LeaveManagement(string? search, string? status, string? leaveType, string? dateFrom, string? dateTo)
        {
            ViewData["Title"] = "Leave Applications Management";

            try
            {
                search = search?.Trim() ?? "";
                status = status?.Trim() ?? "";
                leaveType = leaveType?.Trim() ?? "";

                var query = db.LeaveApplications.AsQueryable();

                // Search by User ID
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(la => la.UserId.Contains(search));
                }

                // Filter by status
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(la => la.Status == status);
                }

                // Filter by leave type
                if (!string.IsNullOrEmpty(leaveType))
                {
                    query = query.Where(la => la.LeaveType == leaveType);
                }

                // Filter by date range
                if (!string.IsNullOrEmpty(dateFrom) && DateOnly.TryParse(dateFrom, out var fromDate))
                {
                    query = query.Where(la => la.StartDate >= fromDate);
                }

                if (!string.IsNullOrEmpty(dateTo) && DateOnly.TryParse(dateTo, out var toDate))
                {
                    query = query.Where(la => la.EndDate <= toDate);
                }

                // Order by most recent application date, then by pending status first
                var applications = query
                    .OrderByDescending(la => la.Status == "Pending")
                    .ThenByDescending(la => la.ApplicationDate)
                    .ToList();

                // Check if it's an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return PartialView("_AdminLeaveList", applications);
                }

                return View(applications);
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error in LeaveManagement: {ex.Message}");

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return StatusCode(500, "Error loading leave applications");
                }

                TempData["ErrorMessage"] = "Error loading leave applications. Please try again.";
                return View(new List<LeaveApplication>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveLeaveApplication(int id)
        {
            try
            {
                var leaveApplication = await db.LeaveApplications.FindAsync(id);

                if (leaveApplication == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "Leave application not found." });
                    }
                    TempData["ErrorMessage"] = "Leave application not found.";
                    return RedirectToAction(nameof(LeaveManagement));
                }

                // Only allow approval of pending applications
                if (leaveApplication.Status != "Pending")
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = $"Cannot approve a leave application that is already {leaveApplication.Status.ToLower()}." });
                    }
                    TempData["ErrorMessage"] = $"Cannot approve a leave application that is already {leaveApplication.Status.ToLower()}.";
                    return RedirectToAction(nameof(LeaveManagement));
                }

                // Update status to Approved
                leaveApplication.Status = "Approved";
                leaveApplication.RejectReason = null; // Clear any previous reject reason

                await db.SaveChangesAsync();

                // Check if the user is a Student and apply leave to attendance
                var isStudent = await db.Students.AnyAsync(s => s.Id == leaveApplication.UserId);

                if (isStudent)
                {
                    // Apply approved leave to attendance for students only
                    _leaveService.ApplyApprovedLeaveToAttendance(
                        leaveApplication.UserId,
                        leaveApplication.StartDate,
                        leaveApplication.EndDate
                    );
                }

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = $"Leave application for {leaveApplication.UserId} approved successfully." });
                }

                TempData["SuccessMessage"] = $"Leave application for {leaveApplication.UserId} approved successfully.";
                return RedirectToAction(nameof(LeaveManagement));
            }
            catch (Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = $"Error approving application: {ex.Message}" });
                }
                TempData["ErrorMessage"] = $"Failed to approve leave application: {ex.Message}";
                return RedirectToAction(nameof(LeaveManagement));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectLeaveApplication(int id, string? rejectReason)
        {
            try
            {
                var leaveApplication = await db.LeaveApplications.FindAsync(id);

                if (leaveApplication == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "Leave application not found." });
                    }
                    TempData["ErrorMessage"] = "Leave application not found.";
                    return RedirectToAction(nameof(LeaveManagement));
                }

                // Only allow rejection of pending applications
                if (leaveApplication.Status != "Pending")
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = $"Cannot reject a leave application that is already {leaveApplication.Status.ToLower()}." });
                    }
                    TempData["ErrorMessage"] = $"Cannot reject a leave application that is already {leaveApplication.Status.ToLower()}.";
                    return RedirectToAction(nameof(LeaveManagement));
                }

                // Update status to Rejected
                leaveApplication.Status = "Rejected";

                // Set reject reason (can be empty/null)
                leaveApplication.RejectReason = string.IsNullOrWhiteSpace(rejectReason)
                    ? "No reason provided"
                    : rejectReason.Trim();

                await db.SaveChangesAsync();

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = $"Leave application for {leaveApplication.UserId} rejected successfully." });
                }

                TempData["SuccessMessage"] = $"Leave application for {leaveApplication.UserId} rejected successfully.";
                return RedirectToAction(nameof(LeaveManagement));
            }
            catch (Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = $"Error rejecting application: {ex.Message}" });
                }
                TempData["ErrorMessage"] = $"Failed to reject leave application: {ex.Message}";
                return RedirectToAction(nameof(LeaveManagement));
            }
        }

        // KM - Academic Calendar

        //method to calculate semester type from dates
        private string CalculateSemesterType(DateOnly startDate, DateOnly endDate)
        {
            var weeks = (endDate.ToDateTime(TimeOnly.MinValue) - startDate.ToDateTime(TimeOnly.MinValue)).TotalDays / 7;
            return weeks >= 12 && weeks <= 16 ? "Long" : "Short"; // Long: 12-16 weeks, Short: 6-8 weeks
        }

        //method to get semester for a given date
        private Semester? GetSemesterForDate(DateTime date)
        {
            var dateOnly = DateOnly.FromDateTime(date);
            return db.Semesters
                .FirstOrDefault(s => s.IsActive &&
                                    dateOnly >= s.StartDate &&
                                    dateOnly <= s.EndDate);
        }

        //semester configuration
        public IActionResult SemesterConfig()
        {
            ViewData["Title"] = "Semester Configuration";

            var semesters = db.Semesters
                .OrderByDescending(s => s.Id)
                .ToList();

            //calculate semester types for display
            var semestersWithType = semesters.Select(s => new SemesterWithTypeInfo
            {
                Semester = s,
                CalculatedType = CalculateSemesterType(s.StartDate, s.EndDate),
                Weeks = (s.EndDate.ToDateTime(TimeOnly.MinValue) - s.StartDate.ToDateTime(TimeOnly.MinValue)).TotalDays / 7
            }).ToList();

            var viewModel = new SemesterConfigVM
            {
                Semesters = semesters,
                SemestersWithType = semestersWithType
            };

            return View("~/Views/Admin/AcademicCalendar.cshtml", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateOrUpdateSemester(string id, DateOnly startDate, string semesterType, bool isActive = true)
        {
            try
            {
                //calculate end date based on semester type
                int weeks = semesterType == "Long" ? 14 : 7;
                var endDate = startDate.AddDays(weeks * 7 - 1);

                if (endDate <= startDate)
                {
                    return Json(new { success = false, message = "Invalid date calculation." });
                }

                var calculatedWeeks = (endDate.ToDateTime(TimeOnly.MinValue) - startDate.ToDateTime(TimeOnly.MinValue)).TotalDays / 7;

                var semester = db.Semesters.FirstOrDefault(s => s.Id == id);

                if (semester == null)
                {
                    semester = new Semester
                    {
                        Id = id,
                        StartDate = startDate,
                        EndDate = endDate,
                        IsActive = isActive
                    };
                    db.Semesters.Add(semester);
                }
                else
                {
                    semester.StartDate = startDate;
                    semester.EndDate = endDate;
                    semester.IsActive = isActive;
                }

                db.SaveChanges();

                if (Request.IsAjax() || Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new
                    {
                        success = true,
                        message = "Semester saved successfully.",
                        semesterType = semesterType,
                        weeks = Math.Round(calculatedWeeks, 1),
                        endDate = endDate.ToString("yyyy-MM-dd")
                    });
                }

                TempData["SuccessMessage"] = $"Semester saved successfully. Type: {semesterType} ({Math.Round(calculatedWeeks, 1)} weeks)";
                return RedirectToAction("EventManagement");
            }
            catch (Exception ex)
            {
                if (Request.IsAjax() || Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Error: " + ex.Message });
                }

                TempData["ErrorMessage"] = "Error saving semester: " + ex.Message;
                return RedirectToAction("EventManagement");
            }
        }

        //event management
        public IActionResult EventManagement(string? semesterId = null)
        {
            ViewData["Title"] = "Academic Calendar";

            var eventsQuery = db.CalendarEvents.AsQueryable();

            if (!string.IsNullOrEmpty(semesterId))
            {
                var semester = db.Semesters.FirstOrDefault(s => s.Id == semesterId);
                if (semester != null)
                {
                    eventsQuery = eventsQuery.Where(e =>
                        e.StartTime.Date >= semester.StartDate.ToDateTime(TimeOnly.MinValue) &&
                        e.EndTime.Date <= semester.EndDate.ToDateTime(TimeOnly.MaxValue));
                }
            }

            var events = eventsQuery
                .OrderBy(e => e.StartTime)
                .ToList();

            var eventsWithSemester = events.Select(e => new EventWithSemesterInfo
            {
                Event = e,
                Semester = GetSemesterForDate(e.StartTime)
            }).ToList();

            var semesters = db.Semesters
                .OrderByDescending(s => s.Id)
                .ToList();

            var viewModel = new EventManagementVM
            {
                Events = events,
                Semesters = semesters,
                SelectedSemesterId = semesterId,
                EventsWithSemester = eventsWithSemester
            };

            return View("~/Views/Admin/AcademicCalendar.cshtml", viewModel);
        }

        //get events for FullCalendar (JSON) - Update to include semesters
        [HttpGet]
        public IActionResult GetCalendarEvents(string? start, string? end, string? semesterId = null)
        {
            DateTime startDate;
            DateTime endDate;

            if (!DateTime.TryParse(start, out startDate))
            {
                startDate = DateTime.Now.AddMonths(-1);
            }

            if (!DateTime.TryParse(end, out endDate))
            {
                endDate = DateTime.Now.AddMonths(1);
            }

            var eventsList = new List<object>();

            //get calendar events
            var eventsQuery = db.CalendarEvents
                .Where(e => e.StartTime >= startDate && e.EndTime <= endDate)
                .AsQueryable();

            if (!string.IsNullOrEmpty(semesterId))
            {
                var semester = db.Semesters.FirstOrDefault(s => s.Id == semesterId);
                if (semester != null)
                {
                    //convert DateOnly to DateTime for comparison
                    var semesterStart = semester.StartDate.ToDateTime(TimeOnly.MinValue);
                    var semesterEnd = semester.EndDate.ToDateTime(TimeOnly.MaxValue);

                    eventsQuery = eventsQuery.Where(e =>
                        e.StartTime.Date >= semesterStart.Date &&
                        e.StartTime.Date <= semesterEnd.Date);
                }
            }

            var calendarEvents = eventsQuery.ToList();

            //add calendar events
            eventsList.AddRange(calendarEvents.Select(e =>
            {
                //check if event is effectively all-day (starts at midnight or spans full day)
                var isAllDay = (e.StartTime.Hour == 0 && e.StartTime.Minute == 0) &&
                              (e.EndTime.Hour == 0 && e.EndTime.Minute == 0 ||
                               e.EndTime.Hour == 23 && e.EndTime.Minute == 59);

                return new
                {
                    id = e.Id,
                    title = e.Title,
                    description = e.Description,
                    //use date-only format for all-day events
                    start = isAllDay ? e.StartTime.ToString("yyyy-MM-dd") : e.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = isAllDay ? e.EndTime.ToString("yyyy-MM-dd") : e.EndTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    type = e.Type,
                    backgroundColor = GetEventColor(e.Type),
                    borderColor = GetEventColor(e.Type),
                    textColor = GetEventTextColor(e.Type),
                    allDay = isAllDay,
                    eventType = "calendar"
                };
            }));

            //get semesters and add them as events
            var allSemesters = db.Semesters.ToList();

            var semesters = allSemesters.Where(s =>
            {
                var sStart = s.StartDate.ToDateTime(TimeOnly.MinValue);
                var sEnd = s.EndDate.ToDateTime(TimeOnly.MaxValue);
                return sStart <= endDate && sEnd >= startDate;
            }).ToList();

            //filter by semesterId if provided
            if (!string.IsNullOrEmpty(semesterId))
            {
                semesters = semesters.Where(s => s.Id == semesterId).ToList();
            }

            //add semester periods as events
            eventsList.AddRange(semesters.Select(s =>
            {
                var semesterType = CalculateSemesterType(s.StartDate, s.EndDate);
                return new
                {
                    id = $"semester_{s.Id}",
                    title = $"{s.Id} - {semesterType}",
                    description = $"Semester Period: {s.StartDate:dd MMM yyyy} to {s.EndDate:dd MMM yyyy}",
                    start = s.StartDate.ToString("yyyy-MM-dd"),
                    end = s.EndDate.AddDays(1).ToString("yyyy-MM-dd"),
                    type = "Semester Period",
                    backgroundColor = semesterType == "Long" ? "#6f42c1" : "#e83e8c",
                    borderColor = semesterType == "Long" ? "#5a32a3" : "#d91a72",
                    textColor = "#ffffff",
                    allDay = true,
                    eventType = "semester",
                    classNames = new[] { "semester-period" },
                    extendedProps = new
                    {
                        eventType = "semester",
                        semesterId = s.Id
                    }
                };
            }));

            return Json(eventsList);
        }

        private string GetEventColor(string eventType)
        {
            return eventType switch
            {
                "Public Holiday" => "#c82333",
                "Exam Period" => "#e0a800",
                "Academic Event" => "#218838",
                "Semester Break" => "#138496",
                _ => "#343a40"
            };
        }

        private string GetEventTextColor(string eventType)
        {
            return "#ffffff";
        }

        //create or update event
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveEvent(string? id, string title, string description,
            DateTime startTime, DateTime endTime, string type)
        {
            try
            {
                if (endTime <= startTime)
                {
                    return Json(new { success = false, message = "End time must be after start time." });
                }

                //check for conflicts
                var conflictCheck = CheckConflict(id, startTime, endTime);
                if (conflictCheck.HasConflict)
                {
                    return Json(new
                    {
                        success = false,
                        message = conflictCheck.Message,
                        conflicts = conflictCheck.ConflictingEvents.Select(e => new { e.Title, e.StartTime, e.EndTime })
                    });
                }

                CalendarEvent calendarEvent;

                if (string.IsNullOrEmpty(id))
                {
                    calendarEvent = new CalendarEvent
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Title = title,
                        Description = description,
                        StartTime = startTime,
                        EndTime = endTime,
                        Type = type
                    };
                    db.CalendarEvents.Add(calendarEvent);
                }
                else
                {
                    calendarEvent = db.CalendarEvents.FirstOrDefault(e => e.Id == id);
                    if (calendarEvent == null)
                    {
                        return Json(new { success = false, message = "Event not found." });
                    }

                    calendarEvent.Title = title;
                    calendarEvent.Description = description;
                    calendarEvent.StartTime = startTime;
                    calendarEvent.EndTime = endTime;
                    calendarEvent.Type = type;
                }

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = string.IsNullOrEmpty(id) ? "Event created successfully." : "Event updated successfully.",
                    eventId = calendarEvent.Id
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        //update event via drag & drop
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateEventDate(string eventId, DateTime newStart, DateTime? newEnd = null)
        {
            try
            {
                var calendarEvent = db.CalendarEvents.FirstOrDefault(e => e.Id == eventId);
                if (calendarEvent == null)
                {
                    return Json(new { success = false, message = "Event not found." });
                }

                var originalStart = calendarEvent.StartTime;
                var originalEnd = calendarEvent.EndTime;
                var duration = originalEnd - originalStart;

                var updatedEnd = newEnd ?? newStart.Add(duration);

                //check for conflicts
                var conflictCheck = CheckConflict(eventId, newStart, updatedEnd);
                if (conflictCheck.HasConflict)
                {
                    return Json(new
                    {
                        success = false,
                        message = conflictCheck.Message,
                        conflicts = conflictCheck.ConflictingEvents.Select(e => new { e.Title, e.StartTime, e.EndTime })
                    });
                }

                calendarEvent.StartTime = newStart;
                calendarEvent.EndTime = updatedEnd;

                db.SaveChanges();

                return Json(new { success = true, message = "Event rescheduled successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        //delete event
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteEvent(string eventId)
        {
            try
            {
                var calendarEvent = db.CalendarEvents.FirstOrDefault(e => e.Id == eventId);
                if (calendarEvent == null)
                {
                    return Json(new { success = false, message = "Event not found." });
                }

                db.CalendarEvents.Remove(calendarEvent);
                db.SaveChanges();

                return Json(new { success = true, message = "Event deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        //conflict validation
        private ConflictCheckVM CheckConflict(string? excludeEventId, DateTime startTime, DateTime endTime)
        {
            var conflictingEvents = db.CalendarEvents
                .Where(e => e.Id != excludeEventId &&
                           ((e.StartTime <= startTime && e.EndTime > startTime) ||
                            (e.StartTime < endTime && e.EndTime >= endTime) ||
                            (e.StartTime >= startTime && e.EndTime <= endTime)))
                .ToList();

            if (conflictingEvents.Any())
            {
                var conflictTitles = string.Join(", ", conflictingEvents.Select(e => e.Title));
                return new ConflictCheckVM
                {
                    HasConflict = true,
                    ConflictingEvents = conflictingEvents,
                    Message = $"This event conflicts with: {conflictTitles}"
                };
            }

            return new ConflictCheckVM { HasConflict = false, ConflictingEvents = new List<CalendarEvent>(), Message = "" };
        }

        //ical export
        [HttpGet]
        public IActionResult ExportICal(string? semesterId = null)
        {
            var eventsQuery = db.CalendarEvents.AsQueryable();

            if (!string.IsNullOrEmpty(semesterId))
            {
                var semester = db.Semesters.FirstOrDefault(s => s.Id == semesterId);
                if (semester != null)
                {
                    eventsQuery = eventsQuery.Where(e =>
                        e.StartTime.Date >= semester.StartDate.ToDateTime(TimeOnly.MinValue) &&
                        e.EndTime.Date <= semester.EndDate.ToDateTime(TimeOnly.MaxValue));
                }
            }

            var events = eventsQuery.ToList();

            var ical = new StringBuilder();
            ical.AppendLine("BEGIN:VCALENDAR");
            ical.AppendLine("VERSION:2.0");
            ical.AppendLine("PRODID:-//TARUMT//Academic Calendar//EN");
            ical.AppendLine("CALSCALE:GREGORIAN");
            ical.AppendLine("METHOD:PUBLISH");

            foreach (var evt in events)
            {
                ical.AppendLine("BEGIN:VEVENT");
                ical.AppendLine($"UID:{evt.Id}@tarumt.edu.my");
                ical.AppendLine($"DTSTART:{evt.StartTime:yyyyMMddTHHmmss}");
                ical.AppendLine($"DTEND:{evt.EndTime:yyyyMMddTHHmmss}");
                ical.AppendLine($"SUMMARY:{evt.Title}");
                ical.AppendLine($"DESCRIPTION:{evt.Description}");
                ical.AppendLine($"LOCATION:Academic Calendar");
                ical.AppendLine($"STATUS:CONFIRMED");
                ical.AppendLine("END:VEVENT");
            }

            ical.AppendLine("END:VCALENDAR");

            var bytes = Encoding.UTF8.GetBytes(ical.ToString());
            return File(bytes, "text/calendar", $"academic_calendar_{DateTime.Now:yyyyMMdd}.ics");
        }

        //get semester details for calendar navigation
        [HttpGet]
        public IActionResult GetSemesterDetails(string semesterId)
        {
            var semester = db.Semesters.FirstOrDefault(s => s.Id == semesterId);
            if (semester == null)
            {
                return Json(new { success = false, message = "Semester not found." });
            }

            return Json(new
            {
                success = true,
                id = semester.Id,
                startDate = semester.StartDate.ToString("yyyy-MM-dd"),
                endDate = semester.EndDate.ToString("yyyy-MM-dd"),
                isActive = semester.IsActive
            });
        }

    }
}