using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHAPEE.Models;
using System.Security.Cryptography;
using System.Text;
using static SHAPEE.Models.DB;

namespace SHAPEE.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController(DB db) : Controller
    {

        // Dashboard
        public IActionResult Dashboard()
        {
            ViewData["Title"] = "Admin Dashboard";
            return View();
        }

        public IActionResult UserManagement(string? name, string? filter1, string? filter2, string? filter3)
        {
            ViewData["Title"] = "User Management";

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
                return PartialView("_User", result);
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


        // POST: Admin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string firstName, string lastName, string gender,
            string email, string contact, string programName, int yearLevel)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(firstName) ||
                    string.IsNullOrWhiteSpace(lastName) ||
                    string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(gender))
                {
                    TempData["ErrorMessage"] = "Please fill in all required fields.";
                    return RedirectToAction(nameof(UserManagement));
                }

                // Check duplicate email
                if (await db.Students.AnyAsync(s => s.Email == email))
                {
                    TempData["ErrorMessage"] = "Email already exists.";
                    return RedirectToAction(nameof(UserManagement));
                }

                // Generate Student ID
                var lastStudent = await db.Students.OrderByDescending(s => s.Id).FirstOrDefaultAsync();
                string newId = (lastStudent != null && lastStudent.Id.StartsWith("R"))
                    ? $"R{(int.Parse(lastStudent.Id.Substring(1)) + 1).ToString("D4")}"
                    : "R0001";

                // Find Program
                var program = await db.Programs.FirstOrDefaultAsync(p => p.Name == programName);

                if (program == null)
                {
                    TempData["ErrorMessage"] = "Selected program not found.";
                    return RedirectToAction(nameof(UserManagement));
                }

                // Generate random password + hash
                string rawPassword = GenerateRandomPassword(8);
                var passwordHasher = new PasswordHasher<User>();
                string passwordHash = passwordHasher.HashPassword(null, rawPassword);

                // Create student
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
                    Semester = 1,
                    DateJoin = DateOnly.FromDateTime(DateTime.Now),

                    // Required User fields
                    Hash = passwordHash,
                    LoginAttempts = 3,
                    Status = "Active",
                    // Discriminator will automatically be "Student"
                };

                db.Students.Add(student);
                await db.SaveChangesAsync();

                // OPTIONAL – show the generated password to admin (for giving to student)
                TempData["SuccessMessage"] =
                    $"Student '{student.FirstName} {student.LastName}' created successfully! " +
                    $"Generated password: {rawPassword}";

                return RedirectToAction(nameof(UserManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] =
                    $"Error creating student: {ex.InnerException?.Message ?? ex.Message}";
                return RedirectToAction(nameof(UserManagement));
            }
        }



        // POST: Admin/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, string firstName, string lastName, string gender,
            string email, string contact, string programName, int yearLevel, string status)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(firstName))
                {
                    TempData["ErrorMessage"] = "First name is required.";
                    return RedirectToAction(nameof(UserManagement));
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    TempData["ErrorMessage"] = "Email is required.";
                    return RedirectToAction(nameof(UserManagement));
                }

                if (string.IsNullOrWhiteSpace(gender))
                {
                    TempData["ErrorMessage"] = "Gender is required.";
                    return RedirectToAction(nameof(UserManagement));
                }

                // Check if student exists
                var existingStudent = await db.Students.FindAsync(id);
                if (existingStudent == null)
                {
                    TempData["ErrorMessage"] = "Student not found.";
                    return RedirectToAction(nameof(UserManagement));
                }

                // Check for duplicate email (excluding current student)
                var duplicateEmail = await db.Students
                    .AnyAsync(s => s.Email == email && s.Id != id);

                if (duplicateEmail)
                {
                    TempData["ErrorMessage"] = "Email already exists for another student.";
                    return RedirectToAction(nameof(UserManagement));
                }

                // Update student properties
                existingStudent.FirstName = firstName.Trim();
                existingStudent.LastName = lastName.Trim();
                existingStudent.Gender = gender.Trim();
                existingStudent.Email = email.Trim().ToLower();
                existingStudent.Contact = contact?.Trim();
                existingStudent.YearLevel = yearLevel;
                existingStudent.Status = status?.Trim() ?? "Active";

                // Handle Program - find program by name and set ProgramId
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
                        return RedirectToAction(nameof(UserManagement));
                    }
                }

                // Save changes to database
                db.Students.Update(existingStudent);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Student '{existingStudent.FullName}' updated successfully!";
                return RedirectToAction(nameof(UserManagement));
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
                return RedirectToAction(nameof(UserManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating student: {ex.Message}";
                return RedirectToAction(nameof(UserManagement));
            }
        }

        // POST: Admin/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var student = await db.Students.FindAsync(id);

                if (student == null)
                {
                    TempData["ErrorMessage"] = "Student not found.";
                    return RedirectToAction(nameof(UserManagement));
                }

                var studentName = student.FullName;

                db.Students.Remove(student);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Student '{studentName}' deleted successfully!";
                return RedirectToAction(nameof(UserManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting student: {ex.Message}";
                return RedirectToAction(nameof(UserManagement));
            }
        }

        // POST: Admin/Suspend
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Suspend(string id)
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

        public IActionResult CourseManagement()
        {
            ViewData["Title"] = "Course Management";
            return View();
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
                (sort == "3") ? filteredFaculties.OrderByDescending(f => f.Id) :
                (sort == "4") ? filteredFaculties.OrderBy(f => f.CostPerCreditHour) :
                (sort == "5") ? filteredFaculties.OrderByDescending(f => f.CostPerCreditHour) :
                                     filteredFaculties.OrderBy(f => f.Id);

            var faculties = facultiesQuery.ToList();



            if (Request.IsAjax())
            {
                return PartialView("_Faculty", faculties);
            }

            return View(faculties);
        }

        public IActionResult RoomManagement() {
            return View();
        }
    }
}
