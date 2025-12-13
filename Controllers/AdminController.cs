using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SHAPEE.Models;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
                    Semester = semester, // Use the submitted semester
                    DateJoin = DateOnly.FromDateTime(DateTime.Now),
                    Hash = passwordHash,
                    LoginAttempts = 0,
                    Status = "Active"
                };

                db.Students.Add(student);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    $"Student '{student.FirstName} {student.LastName}' created successfully! " +
                    $"Generated password: {rawPassword}";

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

                // Check if student exists
                var existingStudent = await db.Students.FindAsync(id);
                if (existingStudent == null)
                {
                    TempData["ErrorMessage"] = "Student not found.";
                    return RedirectToAction(nameof(StudentManagement));
                }

                // Check for duplicate email (excluding current student)
                var duplicateEmail = await db.Students
                    .AnyAsync(s => s.Email == email && s.Id != id);

                if (duplicateEmail)
                {
                    TempData["ErrorMessage"] = "Email already exists for another student.";
                    return RedirectToAction(nameof(StudentManagement));
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
                // Check duplicate email
                if (await db.Staffs.AnyAsync(s => s.Email == email.Trim().ToLower()))
                {
                    TempData["ErrorMessage"] = "Email already exists.";
                    return RedirectToAction(nameof(StaffManagement));
                }

                // Generate Staff ID
                var lastStaff = await db.Staffs
                    .OrderByDescending(s => s.Id)
                    .FirstOrDefaultAsync();

                string newId = (lastStaff != null && lastStaff.Id.StartsWith("STF"))
                    ? $"STF{(int.Parse(lastStaff.Id.Substring(1)) + 1):D4}"
                    : "STF0001";

                // Generate random password + hash
                string rawPassword = GenerateRandomPassword(8);
                var passwordHasher = new PasswordHasher<User>();
                string passwordHash = passwordHasher.HashPassword(null, rawPassword);

                // Create Staff
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

                TempData["SuccessMessage"] =
                    $"Staff '{staff.FirstName} {staff.LastName}' created successfully! " +
                    $"Generated password: {rawPassword}";

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
                // Check duplicate email
                if (await db.Admins.AnyAsync(s => s.Email == email.Trim().ToLower()))
                {
                    TempData["ErrorMessage"] = "Email already exists.";
                    return RedirectToAction(nameof(AdminManagement));
                }

                // Generate admin ID
                var lastAdmin = await db.Admins
                    .OrderByDescending(s => s.Id)
                    .FirstOrDefaultAsync();

                string newId = (lastAdmin != null && lastAdmin.Id.StartsWith("Admin"))
                    ? $"Admin{(int.Parse(lastAdmin.Id.Substring(1)) + 1):D2}"
                    : "Admin01";

                // Generate random password + hash
                string rawPassword = GenerateRandomPassword(8);
                var passwordHasher = new PasswordHasher<User>();
                string passwordHash = passwordHasher.HashPassword(null, rawPassword);

                // Create admin
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

                TempData["SuccessMessage"] =
                    $"Admin '{admin.FirstName} {admin.LastName}' created successfully! " +
                    $"Generated password: {rawPassword}";

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




        public IActionResult FacultyManagement(string? sort, string? name, string? editId)
        {
            ViewData["Title"] = "Faculty Management";
            Faculty? editFaculty = null;

            if (!string.IsNullOrEmpty(editId))
            {
                editFaculty = db.Faculties.FirstOrDefault(f => f.Id == editId);
            }
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
                Faculties = faculties,
                EditFaculty = editFaculty
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
            catch (DbUpdateException ex)
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
                EditCourse = editCourse
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
                    CreditHours = vm.CreditHours ?? 0
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
            catch (DbUpdateException ex)
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
        public IActionResult GroupManagement()
        {
            ViewData["Title"] = "Group Management";
            TutorialGroup? editGroup = null;
            var groupList = db.TutorialGroups
                .Include(g => g.Program)
                .OrderBy(g => g.Id)
                .ToList();

            var program = db.Programs
                .OrderBy(p => p.Id)
                .ToList();
            ViewBag.Program = program;
            ViewBag.ProgramList = new SelectList(program, "Id", "Name", editGroup?.ProgramId);
            var vm = new GroupManageVM
            {
                Groups = groupList
            };
            return View(vm);
        }

        public IActionResult AddGroup(GroupManageVM vm)
        {
           

            var p = new TutorialGroup
            {
                ProgramId = vm.ProgramId!,
                YearLevel = vm.YearLevel ?? 1,
                Semester = vm.Semester ?? 1,
                NumberOfStudents = 0
            };
            db.TutorialGroups.Add(p);
            db.SaveChanges();
            TempData["SuccessMessage"] = $"Group added successfully.";


            return RedirectToAction(nameof(GroupManagement));
        }
        //JS
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
            if (!floor.HasValue)
            {
                return Json(new { success = false, message = "Floor is required." });
            }

            roomId = roomId?.Trim().ToUpper();
            type = type?.Trim();
            block = block?.Trim().ToUpper();

            // For Labs: Room ID uses 0-2, but database stores as 1-3
            int actualFloor = floor.Value;

            if (type == "Lab")
            {
                if (floor < 0 || floor > 2)
                {
                    return Json(new { success = false, message = "Lab floor must be between 0 and 2." });
                }
                // Convert Room ID floor (0-2) to database floor (1-3)
                actualFloor = floor.Value + 1;
            }
            else if (type == "Classroom")
            {
                if (floor < 1 || floor > 3)
                {
                    return Json(new { success = false, message = "Classroom floor must be between 1 and 3." });
                }
            }

            // Check if room already exists
            bool exists = await db.Rooms.AnyAsync(r => r.Id == roomId);

            if (exists)
            {
                return Json(new { success = false, message = $"Room ID '{roomId}' already exists." });
            }

            // Validate Room ID format
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return Json(new { success = false, message = "Room ID is required." });
            }

            if (!roomId.StartsWith(block))
            {
                return Json(new { success = false, message = $"Room ID must start with block '{block}'." });
            }

            if (type == "Lab")
            {
                // Lab format: [Block][0-2][0][0-2] where floor 0=Floor1, 1=Floor2, 2=Floor3 in DB
                var pattern = $@"^{Regex.Escape(block)}[0-2]0[0-2]$";
                if (!Regex.IsMatch(roomId, pattern))
                {
                    return Json(new { success = false, message = $"Lab Room ID must follow format: {block}[0-2]0[0-2] (e.g., {block}000, {block}101, {block}202)" });
                }

                // Check if floor digit matches selected floor
                var floorDigitInId = roomId.Substring(block.Length, 1);
                if (floorDigitInId != floor.Value.ToString())
                {
                    return Json(new { success = false, message = $"Room ID floor digit must match the selected floor. Expected: {block}{floor}0[0-2]" });
                }
            }
            else if (type == "Classroom")
            {
                // Classroom format: [Block][1-3][0][1-3]
                var pattern = $@"^{Regex.Escape(block)}[1-3]0[1-3]$";
                if (!Regex.IsMatch(roomId, pattern))
                {
                    return Json(new { success = false, message = $"Classroom Room ID must follow format: {block}[1-3]0[1-3] (e.g., {block}101, {block}202, {block}303)" });
                }

                // Check if floor digit matches selected floor
                var floorDigitInId = roomId.Substring(block.Length, 1);
                if (floorDigitInId != floor.Value.ToString())
                {
                    return Json(new { success = false, message = $"Room ID floor digit must match the selected floor. Expected: {block}{floor}0[1-3]" });
                }
            }
            else if (type == "Lecture Hall")
            {
                if (!roomId.StartsWith("DK"))
                {
                    return Json(new { success = false, message = "Lecture Hall Room ID must start with 'DK'." });
                }
            }

            try
            {
                var room = new Room
                {
                    Id = roomId,
                    Type = type,
                    Block = block,
                    Floor = actualFloor, // Store the converted floor for Labs (0-2), normal for Classroom (1-3)
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
                    TempData["ErrorMessage"] = "Status is required.";
                    return RedirectToAction(nameof(RoomManagement));
                }

                // Fetch room
                var room = await db.Rooms.FindAsync(roomId);

                if (room == null)
                {
                    TempData["ErrorMessage"] = "Room not found.";
                    return RedirectToAction(nameof(RoomManagement));
                }

                // Only update status
                room.Status = status;

                db.Rooms.Update(room);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = "Room status updated successfully!";
                return RedirectToAction(nameof(RoomManagement));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating room: " + ex.Message;
                return RedirectToAction(nameof(RoomManagement));
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

        // GET: ExportRoomsCSV
        public IActionResult ExportRoomsCSV()
        {
            var rooms = db.Rooms.OrderBy(r => r.Id).ToList();

            var csv = new StringBuilder();
            csv.AppendLine("Room ID,Type,Block,Floor,Status");

            foreach (var room in rooms)
            {
                csv.AppendLine($"{room.Id},{room.Type},{room.Block},{room.Floor},{room.Status}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"Rooms_{DateTime.Now:yyyyMMdd}.csv");
        }

    }
}
