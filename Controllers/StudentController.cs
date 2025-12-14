using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHAPEE.Models;
using System.Security.Claims;

namespace SHAPEE.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController(DB db) : Controller
    {
        // GET: /Student
        public async Task<IActionResult> Dashboard()
        {
            await LoadProfilePicture();
            ViewData["Title"] = "Student Dashboard";
            return View();
        }

        
        //===============KOH KAI MINN================
        // student main attendance page
        public IActionResult ViewAttendance(string? sectionId, string? token, string? subject = null, string? month = null, string? search = null)
        {
            ViewData["Title"] = "View Attendance";

            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            //get student and verify tutorial group 
            var student = db.Students
                .Include(s => s.TutorialGroup)
                .FirstOrDefault(s => s.Id == studentId);

            if (student == null || !student.TutorialGroupId.HasValue)
            {
                return View(new StudentAttendanceVM
                {
                    ClassAttendances = new List<StudentAttendanceItemVM>(),
                    AvailableSubjects = new List<SubjectFilterOption>(),
                    AvailableMonths = new List<MonthFilterOption>()
                });
            }

            //get section id from attendance
            var sectionIds = db.Attendances
                .Where(a => a.StudentId == studentId)
                .Select(a => a.ClassSectionId)
                .Distinct()
                .ToList();

            var enrolledSectionsQuery = db.ClassSections
                .Include(cs => cs.Course)
                .Where(cs => sectionIds.Contains(cs.SectionId))
                .AsQueryable();

            //apply subject filter
            subject = subject?.Trim() ?? "";
            if (!string.IsNullOrEmpty(subject))
            {
                enrolledSectionsQuery = enrolledSectionsQuery
                    .Where(cs => cs.Course.Id == subject);
            }

            //apply search filter
            search = search?.Trim() ?? "";
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                enrolledSectionsQuery = enrolledSectionsQuery.Where(cs =>
                    cs.SectionId.ToLower().Contains(search) ||
                    cs.Course.Name.ToLower().Contains(search));
            }

            var enrolledSections = enrolledSectionsQuery.ToList();

            //get attendance records with month filter
            var attendancesQuery = db.Attendances
                .Where(a => a.StudentId == studentId)
                .AsQueryable();

            //apply month filter
            month = month?.Trim() ?? "";
            if (!string.IsNullOrEmpty(month))
            {
                if (DateTime.TryParse($"{month}-01", out var m))
                {
                    attendancesQuery = attendancesQuery
                        .Where(a => a.Date.Year == m.Year && a.Date.Month == m.Month);
                }
            }

            var attendances = attendancesQuery
                .Where(a => sectionIds.Contains(a.ClassSectionId))
                .ToList();


            //group attendances by class
            var classAttendances = enrolledSections
                .Select(section =>
                {
                    if (section == null || section.Course == null)
                        return null;

                    var sectionAttendances = attendances
                        .Where(a => a.ClassSectionId == section.SectionId)
                        .ToList();

                    return new StudentAttendanceItemVM
                    {
                        ClassSection = section,
                        Course = section.Course,
                        PresentCount = sectionAttendances.Count(x => x.Status == "Present"),
                        AbsentCount = sectionAttendances.Count(x => x.Status == "Absent"),
                        TotalSessions = sectionAttendances.Count,
                        AttendancePercentage = sectionAttendances.Any()
                            ? Math.Round((double)sectionAttendances.Count(x => x.Status == "Present") / sectionAttendances.Count * 100, 2)
                            : 0
                    };
                })
                .Where(x => x != null)
                // Only show sections with attendance if month filter is applied
                .Where(x => string.IsNullOrEmpty(month) || x!.TotalSessions > 0)
                .OrderBy(x => x!.Course!.Id)
                .ToList();

            //get available subjects
           var availableSubjects = db.ClassSections
                .Include(cs => cs.Course)
                .Where(cs => sectionIds.Contains(cs.SectionId))
                .Select(cs => new { cs.Course.Id, cs.Course.Name })
                .Distinct()
                .OrderBy(c => c.Id)
                .Select(c => new SubjectFilterOption
                {
                    CourseId = c.Id,
                    CourseName = c.Name
                })
                .ToList();


            //get available months
            var monthNames = new[]
            {
                "January","February","March","April","May","June",
                "July","August","September","October","November","December"
            };

            var availableMonths = db.Attendances
                .Where(a => a.StudentId == studentId)
                .Select(a => a.Date)
                .Distinct()
                .GroupBy(d => new { d.Year, d.Month })
                .OrderByDescending(g => g.Key.Year)
                .ThenByDescending(g => g.Key.Month)
                .Select(g => new MonthFilterOption
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    DisplayText = $"{monthNames[g.Key.Month - 1]} {g.Key.Year}"
                })
                .ToList();

            //get overall stats
            var allAttendances = db.Attendances
                .Where(a => a.StudentId == studentId)
                .ToList();

            var overallPresent = allAttendances.Count(a => a.Status == "Present");
            var overallAbsent = allAttendances.Count(a => a.Status == "Absent");
            var overallPercentage = allAttendances.Count > 0
                ? Math.Round((double)overallPresent / allAttendances.Count * 100, 2)
                : 0;

            //get recent attendances
            var recentAttendances = db.Attendances
                .Include(a => a.ClassSection)
                    .ThenInclude(cs => cs.Course)
                .Include(a => a.ClassSection)
                    .ThenInclude(cs => cs.Room)
                .Where(a => a.StudentId == studentId)
                .OrderByDescending(a => a.Date)
                .ThenByDescending(a => a.RegisteredAt)
                .Take(10)
                .ToList();

            //get view model
            var viewModel = new StudentAttendanceVM
            {
                ClassAttendances = classAttendances,
                OverallPresent = overallPresent,
                OverallAbsent = overallAbsent,
                OverallPercentage = overallPercentage,
                RecentAttendances = recentAttendances,
                SectionId = sectionId,
                QrToken = token,
                AvailableSubjects = availableSubjects,
                AvailableMonths = availableMonths
            };

            //ajax partial return
            if (Request.IsAjax())
            {
                return PartialView("_AttendanceTable", viewModel);
            }

            return View(viewModel);
        }


        public IActionResult AttendanceHistory()
        {
            ViewData["Title"] = "Attendance History";
            return View("AttendanceHistory");
        }

        [HttpGet]
        public IActionResult GetAttendanceDetails(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId))
                return PartialView("_AttendanceDetails", null);
            
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // get student and verify tutorial group match
            var student = db.Students.FirstOrDefault(s => s.Id == studentId);
            if (student == null || !student.TutorialGroupId.HasValue)
                return PartialView("_AttendanceDetails", null);
            
            var classSection = db.ClassSections
                .Include(cs => cs.Course)
                .Include(cs => cs.Semester)
                .Include(cs => cs.Staff)
                .Include(cs => cs.Room)
                .FirstOrDefault(cs => cs.SectionId == sectionId 
                    && cs.TutorialGroupId == student.TutorialGroupId.Value);
            
            if (classSection == null)
                return PartialView("_AttendanceDetails", null);
            
            var attendances = db.Attendances
                .Where(a => a.StudentId == studentId && a.ClassSectionId == sectionId)
                .OrderByDescending(a => a.Date)
                .ToList();
            
            var presentCount = attendances.Count(a => a.Status == "Present");
            var absentCount = attendances.Count(a => a.Status == "Absent");
            var totalSessions = attendances.Count;
            var attendancePercentage = totalSessions > 0 
                ? Math.Round((double)presentCount / totalSessions * 100, 2) 
                : 0;

            var viewModel = new StudentAttendanceDetailsVM
            {
                ClassSection = classSection,
                Course = classSection.Course,
                Semester = classSection.Semester,
                Attendances = attendances,
                PresentCount = presentCount,
                AbsentCount = absentCount,
                TotalSessions = totalSessions,
                AttendancePercentage = attendancePercentage
            };
            
            return PartialView("_AttendanceDetails", viewModel);
        }


        // student submits attendance (QR or manual code)
        [HttpPost]
        [Authorize(Roles = "Student")]
        public IActionResult SubmitAttendance(string sectionId, string? qrToken, string? manualCode)
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);

            ClassSection? section = null;
            if (!string.IsNullOrEmpty(qrToken) && !string.IsNullOrEmpty(sectionId))
            {
                section = db.ClassSections.FirstOrDefault(s => s.SectionId == sectionId);
                if (section == null)
                {
                    if (Request.IsAjax())
                        return Json(new { success = false, message = "Invalid section ID." });
                    return BadRequest("Invalid section ID.");
                }
            }
            else if (!string.IsNullOrEmpty(manualCode))
            {
                section = db.ClassSections.FirstOrDefault(s =>
                    s.AttendanceCode == manualCode &&
                    s.IsAttendanceSessionActive);
                if (section == null)
                {
                    if (Request.IsAjax())
                        return Json(new { success = false, message = "Invalid attendance code or no active session found for this code." });
                    return BadRequest("Invalid attendance code or no active session found for this code.");
                }
                sectionId = section.SectionId; 
            }
            else
            {
                if (Request.IsAjax())
                    return Json(new { success = false, message = "Either QR token or attendance code is required." });
                return BadRequest("Either QR token or attendance code is required.");
            }

            //check tutorial group match
            var student = db.Students.FirstOrDefault(s => s.Id == studentId);
            if (student == null || !student.TutorialGroupId.HasValue)
            {
                if (Request.IsAjax())
                    return Json(new { success = false, message = "You are not assigned to a tutorial group." });
                return BadRequest("You are not assigned to a tutorial group.");
            }
            
            if (student.TutorialGroupId.Value != section.TutorialGroupId)
            {
                if (Request.IsAjax())
                    return Json(new { success = false, message = "Your tutorial group does not match this class section's tutorial group." });
                return BadRequest("Your tutorial group does not match this class section's tutorial group.");
            }

            //check session is active
            if (!section.IsAttendanceSessionActive)
            {
                if (Request.IsAjax())
                    return Json(new { success = false, message = "Attendance session is not active." });
                return BadRequest("Attendance session is not active.");
            }

            //check whether is Qr or manual code
            bool validQr = !string.IsNullOrEmpty(qrToken)
                && qrToken == section.CurrentQrToken
                && section.CurrentQrExpiry is not null
                && section.CurrentQrExpiry.Value >= now;

            bool validCode = !string.IsNullOrEmpty(manualCode)
                && manualCode == section.AttendanceCode;

            if (!validQr && !validCode)
            {
                if (Request.IsAjax())
                    return Json(new { success = false, message = "Invalid or expired token/code." });
                return BadRequest("Invalid or expired token/code.");
            }

            var sourceLabel = validQr ? "QR" : "MANUAL";

            //prevent duplicate att
            bool already = db.Attendances.Any(a =>
                a.ClassSectionId == sectionId &&
                a.StudentId == studentId &&
                a.Date == today);
            if (already)
            {
                if (Request.IsAjax())
                    return Json(new { success = false, message = "You have already marked attendance for this class today." });
                return BadRequest("You have already marked attendance for this class today.");
            }

            //sus ip checking
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = Request.Headers["User-Agent"].ToString();
            var windowStart = now.AddMinutes(-10);

            int othersFromSameIp = db.Attendances
                .Where(a => a.ClassSectionId == sectionId
                    && a.Date == today
                    && a.IpAddress == ip
                    && a.StudentId != studentId
                    && a.RegisteredAt >= windowStart)
                .Select(a => a.StudentId)
                .Distinct()
                .Count();

            bool isFlagged = othersFromSameIp >= 1;

            //create attendance record
            var att = new Attendance
            {
                Id = Guid.NewGuid().ToString("N"),
                ClassSectionId = sectionId,
                ClassSection = section,
                StudentId = studentId,
                Date = today,
                RegisteredAt = now,
                Status = "Present",
                IpAddress = ip,
                UserAgent = $"{sourceLabel}||{ua}",
                IsFlaged = isFlagged,
                FlagReason = isFlagged ? "Multiple students using same IP in short time window." : null
            };

            db.Attendances.Add(att);

            try
            {
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                if (Request.IsAjax())
                    return Json(new { success = false, message = "Error saving attendance: " + ex.Message });
                return BadRequest("Error saving attendance: " + ex.Message);
            }

            TempData["AttendanceMessage"] = isFlagged
                ? "Attendance recorded (flagged for review)."
                : "Attendance recorded successfully.";

            if (Request.IsAjax())
            {
                return Json(new { 
                    success = true, 
                    message = isFlagged 
                        ? "Attendance recorded (flagged for review)." 
                        : "Attendance recorded successfully." 
                });
            }

            return RedirectToAction("ViewAttendance");
        }

        // GET: Student Profile
        public async Task<IActionResult> Profile()
        {
            await LoadProfilePicture();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var student = await db.Users
                .OfType<Student>()
                .Include(s => s.Program)
                .Include(s => s.TutorialGroup)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (student == null)
                return NotFound();

            return View("~/Views/Shared/Profile.cshtml", student);
        }

        //Update Student Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string firstName, string lastName,
            string email, string contact, string gender, IFormFile profilePicture)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var student = await db.Students.FindAsync(userId);

                if (student == null)
                    return NotFound();

                // Validate email uniqueness (exclude current user)
                var emailExists = await db.Users
                    .AnyAsync(u => u.Email == email.Trim().ToLower() && u.Id != userId);

                if (emailExists)
                {
                    TempData["ErrorMessage"] = "Email already exists.";
                    return RedirectToAction("Profile");
                }

                // Update student fields
                student.FirstName = firstName?.Trim();
                student.LastName = lastName?.Trim();
                student.Email = email?.Trim().ToLower();
                student.Contact = contact?.Trim();
                student.Gender = gender?.Trim();

                // Handle profile picture upload (inline)
                if (profilePicture != null && profilePicture.Length > 0)
                {
                    // Validate file size (2MB max)
                    if (profilePicture.Length > 2 * 1024 * 1024)
                    {
                        TempData["ErrorMessage"] = "File size must not exceed 2MB.";
                        return RedirectToAction("Profile");
                    }

                    // Validate file type
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    var fileExtension = Path.GetExtension(profilePicture.FileName).ToLower();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        TempData["ErrorMessage"] = "Only JPG and PNG files are allowed.";
                        return RedirectToAction("Profile");
                    }

                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/profiles");
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = $"{userId}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await profilePicture.CopyToAsync(fileStream);
                    }

                    // Delete old profile picture if exists
                    if (!string.IsNullOrEmpty(student.ProfilePictureUrl))
                    {
                        var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", student.ProfilePictureUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    student.ProfilePictureUrl = $"/uploads/profiles/{uniqueFileName}";
                }

                await db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Profile updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while updating profile: {ex.Message}";
            }

            return RedirectToAction("Profile");
        }

        //Upload Profile Picture
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadProfilePicture(IFormFile profilePicture)
        {
            try
            {
                if (profilePicture == null || profilePicture.Length == 0)
                {
                    TempData["ErrorMessage"] = "Please select a file to upload.";
                    return RedirectToAction("Profile");
                }

                // Validate file size (2MB max)
                if (profilePicture.Length > 2 * 1024 * 1024)
                {
                    TempData["ErrorMessage"] = "File size must not exceed 2MB.";
                    return RedirectToAction("Profile");
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var fileExtension = Path.GetExtension(profilePicture.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    TempData["ErrorMessage"] = "Only JPG and PNG files are allowed.";
                    return RedirectToAction("Profile");
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var student = await db.Students.FindAsync(userId);

                if (student == null)
                    return NotFound();

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/profiles");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{userId}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await profilePicture.CopyToAsync(fileStream);
                }

                // Delete old profile picture if exists
                if (!string.IsNullOrEmpty(student.ProfilePictureUrl))
                {
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", student.ProfilePictureUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                student.ProfilePictureUrl = $"/uploads/profiles/{uniqueFileName}";
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = "Profile picture uploaded successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while uploading: {ex.Message}";
            }

            return RedirectToAction("Profile");
        }

        private async Task LoadProfilePicture()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var profilePictureUrl = "/img/undraw_profile.svg";
            if (User.IsInRole("Staff"))
            {
                var staff = await db.Staffs.FindAsync(userId);
                if (staff != null && !string.IsNullOrEmpty(staff.ProfilePictureUrl))
                {
                    profilePictureUrl = staff.ProfilePictureUrl;
                }
            }
            else if (User.IsInRole("Student"))
            {
                var student = await db.Students.FindAsync(userId);
                if (student != null && !string.IsNullOrEmpty(student.ProfilePictureUrl))
                {
                    profilePictureUrl = student.ProfilePictureUrl;
                }
            }
            ViewBag.ProfilePictureUrl = profilePictureUrl;
        }
    }
}