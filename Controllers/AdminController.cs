using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHAPEE.Models;
using System.Security.Cryptography;
using System.Text;

namespace SHAPEE.Controllers
{
    public class AdminController : Controller
    {
        private readonly DB _db;

        public AdminController(DB db)
        {
            _db = db;
        }

        // Dashboard
        public IActionResult Index()
        {
            return View();
        }

        // User Management - Display all students
        public async Task<IActionResult> UserManagement()
        {
            var students = await _db.Students.ToListAsync();
            return View(students);
        }

        // CREATE - Add new student
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Student model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Generate student ID (e.g., STU + timestamp)
                    model.Id = "STU" + DateTime.Now.Ticks.ToString().Substring(8);

                    // Hash the default password (you should change this logic)
                    model.Hash = HashPassword("Password123");

                    // Set default values
                    model.Status = "Active";
                    model.LoginAttempts = 0;
                    model.DateJoin = DateOnly.FromDateTime(DateTime.Now);

                    _db.Students.Add(model);
                    await _db.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Student {model.FullName} has been added successfully!";
                    return RedirectToAction(nameof(UserManagement));
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error adding student: {ex.Message}";
                }
            }

            var students = await _db.Students.ToListAsync();
            return View("UserManagement", students);
        }

        // READ - Get student details for editing
        [HttpGet]
        public async Task<IActionResult> GetStudent(string id)
        {
            var student = await _db.Students.FindAsync(id);
            if (student == null)
            {
                return NotFound();
            }
            return Json(student);
        }

        // UPDATE - Edit existing student
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Student model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingStudent = await _db.Students.FindAsync(model.Id);
                    if (existingStudent == null)
                    {
                        TempData["ErrorMessage"] = "Student not found.";
                        return RedirectToAction(nameof(UserManagement));
                    }

                    // Update fields
                    existingStudent.FirstName = model.FirstName;
                    existingStudent.LastName = model.LastName;
                    existingStudent.Email = model.Email;
                    existingStudent.Contact = model.Contact;
                    existingStudent.Program = model.Program;
                    existingStudent.YearLevel = model.YearLevel;
                    existingStudent.Status = model.Status;

                    _db.Students.Update(existingStudent);
                    await _db.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Student {existingStudent.FullName} has been updated successfully!";
                    return RedirectToAction(nameof(UserManagement));
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error updating student: {ex.Message}";
                }
            }

            var students = await _db.Students.ToListAsync();
            return View("UserManagement", students);
        }

        // DELETE - Remove student
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string Id)
        {
            try
            {
                var student = await _db.Students.FindAsync(Id);
                if (student == null)
                {
                    TempData["ErrorMessage"] = "Student not found.";
                    return RedirectToAction(nameof(UserManagement));
                }

                var studentName = student.FullName;
                _db.Students.Remove(student);
                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Student {studentName} has been deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting student: {ex.Message}";
            }

            return RedirectToAction(nameof(UserManagement));
        }

        // SUSPEND - Change student status to suspended
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Suspend(string id)
        {
            try
            {
                var student = await _db.Students.FindAsync(id);
                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found." });
                }

                student.Status = student.Status == "Suspended" ? "Active" : "Suspended";
                _db.Students.Update(student);
                await _db.SaveChangesAsync();

                return Json(new { success = true, message = $"Student status updated to {student.Status}.", status = student.Status });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Helper method to hash passwords
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}