using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SHAPEE.Models;
using System.Reflection;
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
                // Validate required fields
                if (string.IsNullOrWhiteSpace(firstName) ||
                    string.IsNullOrWhiteSpace(lastName) ||
                    string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(gender) ||
                    string.IsNullOrWhiteSpace(programName) ||
                    yearLevel <= 0 ||
                    semester <= 0)
                {
                    TempData["ErrorMessage"] = "Please fill in all required fields.";
                    return RedirectToAction(nameof(StudentManagement));
                }

                // Check duplicate email
                if (await db.Students.AnyAsync(s => s.Email == email.Trim().ToLower()))
                {
                    TempData["ErrorMessage"] = "Email already exists.";
                    return RedirectToAction(nameof(StudentManagement));
                }

                // Generate Student ID
                var lastStudent = await db.Students
                    .OrderByDescending(s => s.Id)
                    .FirstOrDefaultAsync();

                var yearPrefix = DateTime.Now.Year.ToString().Substring(2); // "25"

                string newId;

                if (lastStudent != null && lastStudent.Id.StartsWith(yearPrefix))
                {
                    // get the number part after the year prefix
                    int lastNumber = int.Parse(lastStudent.Id.Substring(2));

                    newId = $"{yearPrefix}{(lastNumber + 1):D5}";
                }
                else
                {
                    // first ID of the year
                    newId = $"{yearPrefix}00001";
                }


                // Find Program by CODE (not Name) since form sends code
                var program = await db.Programs
                    .FirstOrDefaultAsync(p => p.Name == programName);

                if (program == null)
                {
                    TempData["ErrorMessage"] = "Selected program not found.";
                    return RedirectToAction(nameof(StudentManagement));
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
                // Validate required fields
                if (string.IsNullOrWhiteSpace(firstName) ||
                    string.IsNullOrWhiteSpace(lastName) ||
                    string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(gender) ||
                    string.IsNullOrWhiteSpace(facultyId))
                {
                    TempData["ErrorMessage"] = "Please fill in all required fields.";
                    return RedirectToAction(nameof(StaffManagement));
                }

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
                // Validate required fields
                if (string.IsNullOrWhiteSpace(firstName) ||
                    string.IsNullOrWhiteSpace(lastName) ||
                    string.IsNullOrWhiteSpace(email))
                {
                    TempData["ErrorMessage"] = "Please fill in all required fields.";
                    return RedirectToAction(nameof(AdminManagement));
                }

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
                if (string.IsNullOrWhiteSpace(facultyId) ||
                    string.IsNullOrWhiteSpace(facultyName) ||
                    costPerCreditHour <= 0)
                {
                    TempData["ErrorMessage"] = "Please fill in all required fields.";
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


        public IActionResult SaveFaculty(FacultyVM vm)
        {
            return View();
        }
        public IActionResult DeleteFaculty(int id)
        {
            var faculty = db.Faculties.Find(id);
            if (faculty == null)
            {
                return NotFound();
            }
            return View(faculty);
        }

        //YL
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
                    Where(p=>p.FacultyId == filter)
                    .AsQueryable();
            }
            var faculty = db.Faculties.OrderBy(f => f.Id).ToList();
            ViewBag.Faculty = faculty;

            var programList = programQuery.ToList();
            var vm = new ProgramManageVM
            {
                Program = programList,
                EditProgram = editProgram,
           
               
            };

            if (Request.IsAjax())
            {
                return PartialView("_Program", vm);
            }
            return View(vm);
        }

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

            if (Request.IsAjax())
            {
                return PartialView("_Course", vm);
            }
            return View(vm);
        }



        //JS
        public IActionResult RoomManagement()
        {

            var rooms = db.Rooms
                .OrderBy(r => r.Id)
                .ToList();

            return View(rooms);
        }
    }
}
