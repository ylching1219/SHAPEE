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
        public IActionResult ViewAttendance(string? sectionId, string? token)
        {
            ViewData["Title"] = "View Attendance";
            
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // get student tutorial group
            var student = db.Students
                .Include(s => s.TutorialGroup)
                .FirstOrDefault(s => s.Id == studentId);
            
            if (student == null || !student.TutorialGroupId.HasValue)
            {
                // return empty view if student has no tutorial group
                var emptyViewModel = new StudentAttendanceVM
                {
                    ClassAttendances = new List<StudentAttendanceVM>(),
                    AvailableSubjects = new List<SubjectFilterOption>(),
                    AvailableMonths = new List<MonthFilterOption>()
                };
                return View(emptyViewModel);
            }
            
            // get all class sections for student tutorial group
            var enrollments = db.ClassSections
                .Include(cs => cs.Course)
                .Include(cs => cs.Semester)
                .Include(cs => cs.Staff)
                .Include(cs => cs.Room)
                .Where(cs => cs.TutorialGroupId == student.TutorialGroupId.Value)
                .ToList();
            
            // get available subjects from class sections
            var availableSubjects = enrollments
                .Select(cs => new { cs.Course.Id, cs.Course.Name })
                .Distinct()
                .OrderBy(c => c.Id)
                .Select(c => new SubjectFilterOption
                {
                    CourseId = c.Id,
                    CourseName = c.Name
                })
                .ToList();
            
            // get available months from attendance records
            var attendanceDates = db.Attendances
                .Where(a => a.StudentId == studentId)
                .Select(a => a.Date)
                .Distinct()
                .ToList();
            
            var monthNames = new[] { "January", "February", "March", "April", "May", "June",
                             "July", "August", "September", "October", "November", "December" };
            
            var availableMonths = attendanceDates
                .GroupBy(d => new { d.Year, d.Month })
                .OrderByDescending(g => g.Key.Year)
                .ThenByDescending(g => g.Key.Month)
                .Select(g => new MonthFilterOption
                {
                    Month = g.Key.Month,
                    Year = g.Key.Year,
                    DisplayText = $"{monthNames[g.Key.Month - 1]} {g.Key.Year}"
                })
                .ToList();
            
            // calculate attendance for each class section
            var classAttendances = enrollments.Select(cs =>
            {
                var attendances = db.Attendances
                    .Where(a => a.StudentId == studentId 
                        && a.ClassSectionId == cs.SectionId)
                    .ToList();
                
                var presentCount = attendances.Count(a => a.Status == "Present");
                var absentCount = attendances.Count(a => a.Status == "Absent");
                var totalSessions = attendances.Count;
                var attendancePercentage = totalSessions > 0 
                    ? Math.Round((double)presentCount / totalSessions * 100, 2) 
                    : 0;
                
                return new StudentAttendanceVM
                {
                    ClassSection = cs,
                    Course = cs.Course,
                    PresentCount = presentCount,
                    AbsentCount = absentCount,
                    TotalSessions = totalSessions,
                    AttendancePercentage = attendancePercentage
                };
            }).ToList();
            
            // calculate overall statistics
            var allAttendances = db.Attendances
                .Where(a => a.StudentId == studentId)
                .ToList();
            
            var overallPresent = allAttendances.Count(a => a.Status == "Present");
            var overallAbsent = allAttendances.Count(a => a.Status == "Absent");
            var overallPercentage = allAttendances.Count > 0 
                ? Math.Round((double)overallPresent / allAttendances.Count * 100, 2) 
                : 0;
            
            // get recent attendance records (last 10)
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

        [HttpGet]
        public IActionResult GetFilteredAttendance(string? subjectCode, string? month, string? status)
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var now = DateTime.Now;
            
            // get student tutorial group
            var student = db.Students.FirstOrDefault(s => s.Id == studentId);
            if (student == null || !student.TutorialGroupId.HasValue)
            {
                return Json(new { classAttendances = new List<object>(), recentAttendances = new List<object>() });
            }
            
            // get all class sections for student tutorial group
            var enrollmentsQuery = db.ClassSections
                .Include(cs => cs.Course)
                .Include(cs => cs.Semester)
                .Include(cs => cs.Staff)
                .Include(cs => cs.Room)
                .Where(cs => cs.TutorialGroupId == student.TutorialGroupId.Value)
                .AsQueryable();
            
            // filter by subject
            if (!string.IsNullOrEmpty(subjectCode) && subjectCode != "all")
            {
                enrollmentsQuery = enrollmentsQuery.Where(cs => cs.Course.Id == subjectCode);
            }
            
            var enrollments = enrollmentsQuery.ToList();
            
            // get available subjects (for filter dropdown)
            var availableSubjects = db.ClassSections
                .Include(cs => cs.Course)
                .Where(cs => cs.TutorialGroupId == student.TutorialGroupId.Value)
                .Select(cs => new { cs.Course.Id, cs.Course.Name })
                .Distinct()
                .OrderBy(c => c.Id)
                .Select(c => new { courseId = c.Id, courseName = c.Name })
                .ToList();
            
            // get available months (for filter dropdown)
            var attendanceDates = db.Attendances
                .Where(a => a.StudentId == studentId)
                .Select(a => a.Date)
                .Distinct()
                .ToList();
            
            var monthNames = new[] { "January", "February", "March", "April", "May", "June",
                             "July", "August", "September", "October", "November", "December" };
            
            var availableMonths = attendanceDates
                .GroupBy(d => new { d.Year, d.Month })
                .OrderByDescending(g => g.Key.Year)
                .ThenByDescending(g => g.Key.Month)
                .Select(g => new 
                { 
                    month = g.Key.Month, 
                    year = g.Key.Year,
                    displayText = $"{monthNames[g.Key.Month - 1]} {g.Key.Year}",
                    value = g.Key.Month.ToString("00")
                })
                .ToList();
            
            // calculate attendance for each class section with filters
            var classAttendances = enrollments.Select(cs =>
            {
                var attendancesQuery = db.Attendances
                    .Where(a => a.StudentId == studentId && a.ClassSectionId == cs.SectionId)
                    .AsQueryable();
                
                // filter by month
                if (!string.IsNullOrEmpty(month) && month != "all")
                {
                    if (int.TryParse(month, out int monthNum))
                    {
                        var year = now.Year;
                        attendancesQuery = attendancesQuery.Where(a => 
                            a.Date.Year == year && a.Date.Month == monthNum);
                    }
                }
                
                var attendances = attendancesQuery.ToList();
                
                var presentCount = attendances.Count(a => a.Status == "Present");
                var absentCount = attendances.Count(a => a.Status == "Absent");
                var totalSessions = attendances.Count;
                var attendancePercentage = totalSessions > 0 
                    ? Math.Round((double)presentCount / totalSessions * 100, 2) 
                    : 0;
                
                return new
                {
                    sectionId = cs.SectionId,
                    courseId = cs.Course.Id,
                    courseName = cs.Course.Name,
                    presentCount = presentCount,
                    absentCount = absentCount,
                    totalSessions = totalSessions,
                    attendancePercentage = attendancePercentage,
                    statusClass = attendancePercentage >= 80 ? "success" :
                                 attendancePercentage >= 70 ? "info" :
                                 attendancePercentage >= 60 ? "warning" : "danger",
                    statusText = attendancePercentage >= 80 ? "Excellent" :
                                attendancePercentage >= 70 ? "Good" :
                                attendancePercentage >= 60 ? "Fair" : "Poor"
                };
            }).ToList();
            
            // filter by status (percentage)
            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                classAttendances = status switch
                {
                    "excellent" => classAttendances.Where(c => c.attendancePercentage >= 80).ToList(),
                    "good" => classAttendances.Where(c => c.attendancePercentage >= 70 && c.attendancePercentage < 80).ToList(),
                    "fair" => classAttendances.Where(c => c.attendancePercentage >= 60 && c.attendancePercentage < 70).ToList(),
                    "poor" => classAttendances.Where(c => c.attendancePercentage < 60).ToList(),
                    _ => classAttendances
                };
            }
            
            // get recent attendances with filters
            var recentAttendancesQuery = db.Attendances
                .Include(a => a.ClassSection)
                    .ThenInclude(cs => cs.Course)
                .Include(a => a.ClassSection)
                    .ThenInclude(cs => cs.Room)
                .Where(a => a.StudentId == studentId)
                .AsQueryable();
            
            // filter by subject for recent attendances
            if (!string.IsNullOrEmpty(subjectCode) && subjectCode != "all")
            {
                recentAttendancesQuery = recentAttendancesQuery.Where(a => a.ClassSection.Course.Id == subjectCode);
            }
            
            // filter by month for recent attendances
            if (!string.IsNullOrEmpty(month) && month != "all")
            {
                if (int.TryParse(month, out int monthNum))
                {
                    var year = now.Year;
                    recentAttendancesQuery = recentAttendancesQuery.Where(a => 
                        a.Date.Year == year && a.Date.Month == monthNum);
                }
            }
            
            // filter by status for recent attendances
            if (!string.IsNullOrEmpty(status) && status != "all" && status != "excellent" && status != "good" && status != "fair" && status != "poor")
            {
                recentAttendancesQuery = recentAttendancesQuery.Where(a => a.Status == status);
            }
            
            var recentAttendances = recentAttendancesQuery
                .OrderByDescending(a => a.Date)
                .ThenByDescending(a => a.RegisteredAt)
                .Take(10)
                .Select(a => new
                {
                    date = a.Date.ToString("yyyy-MM-dd"), 
                    status = a.Status,
                    courseId = a.ClassSection.Course.Id,
                    courseName = a.ClassSection.Course.Name,
                    roomId = a.ClassSection.Room != null ? a.ClassSection.Room.Id : "N/A",
                    startTime = a.ClassSection.StartTime.ToString("HH:mm"), 
                    endTime = a.ClassSection.EndTime.ToString("HH:mm")
                })
                .ToList();
            
            // calculate filtered overall statistics
            var allAttendancesQuery = db.Attendances
                .Include(a => a.ClassSection)
                    .ThenInclude(cs => cs.Course)
                .Where(a => a.StudentId == studentId)
                .AsQueryable();
            
            if (!string.IsNullOrEmpty(subjectCode) && subjectCode != "all")
            {
                allAttendancesQuery = allAttendancesQuery.Where(a => a.ClassSection.Course.Id == subjectCode);
            }
            
            if (!string.IsNullOrEmpty(month) && month != "all")
            {
                if (int.TryParse(month, out int monthNum))
                {
                    var year = now.Year;
                    allAttendancesQuery = allAttendancesQuery.Where(a => 
                        a.Date.Year == year && a.Date.Month == monthNum);
                }
            }
            
            var allAttendances = allAttendancesQuery.ToList();
            
            var overallPresent = allAttendances.Count(a => a.Status == "Present");
            var overallAbsent = allAttendances.Count(a => a.Status == "Absent");
            var overallPercentage = allAttendances.Count > 0 
                ? Math.Round((double)overallPresent / allAttendances.Count * 100, 2) 
                : 0;
            
            return Json(new
            {
                classAttendances,
                recentAttendances,
                overallPresent,
                overallAbsent,
                overallPercentage,
                availableSubjects,
                availableMonths
            });
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
                if (section == null) return BadRequest("Invalid section ID.");
            }
            else if (!string.IsNullOrEmpty(manualCode))
            {
                section = db.ClassSections.FirstOrDefault(s =>
                    s.AttendanceCode == manualCode &&
                    s.IsAttendanceSessionActive);
                if (section == null)
                    return BadRequest("Invalid attendance code or no active session found for this code.");
                sectionId = section.SectionId; 
            }
            else
            {
                return BadRequest("Either QR token or attendance code is required.");
            }

            //check tutorial group match
            var student = db.Students.FirstOrDefault(s => s.Id == studentId);
            if (student == null || !student.TutorialGroupId.HasValue)
                return BadRequest("You are not assigned to a tutorial group.");
            
            if (student.TutorialGroupId.Value != section.TutorialGroupId)
                return BadRequest("Your tutorial group does not match this class section's tutorial group.");

            //check session is active
            if (!section.IsAttendanceSessionActive)
                return BadRequest("Attendance session is not active.");

            //check whether is Qr or manual code
            bool validQr = !string.IsNullOrEmpty(qrToken)
                        && qrToken == section.CurrentQrToken
                        && section.CurrentQrExpiry is not null
                        && section.CurrentQrExpiry.Value >= now;

            bool validCode = !string.IsNullOrEmpty(manualCode)
                            && manualCode == section.AttendanceCode;

            if (!validQr && !validCode)
                return BadRequest("Invalid or expired token/code.");

            var sourceLabel = validQr ? "QR" : "MANUAL";

            //prevent duplicate att
            bool already = db.Attendances.Any(a =>
                a.ClassSectionId == sectionId &&
                a.StudentId == studentId &&
                a.Date == today);
            if (already)
                return BadRequest("You have already marked attendance for this class today.");

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
                UserAgent = $"{sourceLabel}||{ua}", // tag the source for live scans
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
                return BadRequest("Error saving attendance: " + ex.Message);
            }

            TempData["AttendanceMessage"] = isFlagged
                ? "Attendance recorded (flagged for review)."
                : "Attendance recorded successfully.";

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