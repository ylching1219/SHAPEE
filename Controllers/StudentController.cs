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
        public IActionResult Dashboard()
        {
            ViewData["Title"] = "Student Dashboard";
            return View();
        }

        
        //===============KOH KAI MINN================
        // student main attendance page
        public IActionResult ViewAttendance(string? sectionId, string? token)
        {
            ViewData["Title"] = "View Attendance";
            
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // get all approved enrollments for the student
            var enrollments = db.Enrollments
                .Include(e => e.ClassSection)
                    .ThenInclude(cs => cs.Course)
                .Include(e => e.ClassSection)
                    .ThenInclude(cs => cs.Semester)
                .Include(e => e.ClassSection)
                    .ThenInclude(cs => cs.Staff)
                .Include(e => e.ClassSection)
                    .ThenInclude(cs => cs.Room)
                .Where(e => e.StudentId == studentId 
                    && e.Type == "Enrolled" 
                    && e.Status == "Approved")
                .ToList();
            
            // get available subjects from enrollments
            var availableSubjects = enrollments
                .Select(e => new { e.ClassSection.Course.Id, e.ClassSection.Course.Name })
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
            
            // calculate attendance for each enrollment
            var classAttendances = enrollments.Select(e =>
            {
                var attendances = db.Attendances
                    .Where(a => a.StudentId == studentId 
                        && a.ClassSectionId == e.ClassSectionId)
                    .ToList();
                
                var presentCount = attendances.Count(a => a.Status == "Present");
                var absentCount = attendances.Count(a => a.Status == "Absent");
                var totalSessions = attendances.Count;
                var attendancePercentage = totalSessions > 0 
                    ? Math.Round((double)presentCount / totalSessions * 100, 2) 
                    : 0;
                
                return new StudentAttendanceVM
                {
                    ClassSection = e.ClassSection,
                    Course = e.ClassSection.Course,
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
            
            var enrollment = db.Enrollments
                .Include(e => e.ClassSection)
                    .ThenInclude(cs => cs.Course)
                .Include(e => e.ClassSection)
                    .ThenInclude(cs => cs.Semester)
                .Include(e => e.ClassSection)
                    .ThenInclude(cs => cs.Staff)
                .Include(e => e.ClassSection)
                    .ThenInclude(cs => cs.Room)
                .FirstOrDefault(e => e.StudentId == studentId 
                    && e.ClassSectionId == sectionId 
                    && e.Type == "Enrolled" 
                    && e.Status == "Approved");
            
            if (enrollment == null)
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
                ClassSection = enrollment.ClassSection,
                Course = enrollment.ClassSection.Course,
                Semester = enrollment.ClassSection.Semester,
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
            
            // get all approved enrollments
            var enrollmentsQuery = db.Enrollments
                .Include(e => e.ClassSection)
                    .ThenInclude(cs => cs.Course)
                .Include(e => e.ClassSection)
                    .ThenInclude(cs => cs.Semester)
                .Include(e => e.ClassSection)
                    .ThenInclude(cs => cs.Staff)
                .Include(e => e.ClassSection)
                    .ThenInclude(cs => cs.Room)
                .Where(e => e.StudentId == studentId 
                    && e.Type == "Enrolled" 
                    && e.Status == "Approved")
                .AsQueryable();
            
            // filter by subject
            if (!string.IsNullOrEmpty(subjectCode) && subjectCode != "all")
            {
                enrollmentsQuery = enrollmentsQuery.Where(e => e.ClassSection.Course.Id == subjectCode);
            }
            
            var enrollments = enrollmentsQuery.ToList();
            
            // get available subjects (for filter dropdown)
            var availableSubjects = db.Enrollments
                .Include(e => e.ClassSection)
                    .ThenInclude(cs => cs.Course)
                .Where(e => e.StudentId == studentId 
                    && e.Type == "Enrolled" 
                    && e.Status == "Approved")
                .Select(e => new { e.ClassSection.Course.Id, e.ClassSection.Course.Name })
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
            
            // calculate attendance for each enrollment with filters
            var classAttendances = enrollments.Select(e =>
            {
                var attendancesQuery = db.Attendances
                    .Where(a => a.StudentId == studentId && a.ClassSectionId == e.ClassSectionId)
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
                    sectionId = e.ClassSection.SectionId,
                    courseId = e.ClassSection.Course.Id,
                    courseName = e.ClassSection.Course.Name,
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
            
                // Calculate filtered overall statistics
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

            //check enrollment
            bool enrolled = db.Enrollments.Any(e =>
                e.StudentId == studentId &&
                e.ClassSectionId == sectionId &&
                e.Type == "Enrolled" &&
                e.Status == "Approved");
            if (!enrolled) return BadRequest("You are not enrolled in this class section or your enrollment is not approved.");

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
    }
}