using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHAPEE.Models;
using System.Security.Claims;

namespace SHAPEE.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController(DB db) : Controller
    {
        // GET: /Staff
        public IActionResult Dashboard()
        {
            ViewData["Title"] = "Staff Dashboard";
            return View();    
        }


        //===============KOH KAI MINN================

        public IActionResult ClassSelection(string mode = "take")
        {
            ViewData["Title"] = mode == "view"
                ? "Select Class to View Attendance"
                : "Select Class to Take Attendance";
            ViewData["Mode"] = mode;

            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var now = DateTime.Now;
            var todayName = now.DayOfWeek.ToString();  
            var todayAbbrev = todayName[..3];                  
            var currentTime = TimeOnly.FromDateTime(now);

            var sections = db.ClassSections
                .Include(c => c.Course)
                .Include(c => c.Room)
                .Include(c => c.Semester)
                .Where(c => c.StaffId == staffId)
                .OrderBy(c => c.Day)
                .ThenBy(c => c.StartTime)
                .ToList();

            var isView = (mode ?? "take").Equals("view", StringComparison.OrdinalIgnoreCase);

            List<ClassSelectionItemVM> vm;

            if (isView)
            {
                // mode = view (show all class)
                vm = sections.Select(s => new ClassSelectionItemVM
                {
                    Section = s,
                    CanStart = true,
                    DisabledReason = null
                }).ToList();
            }
            else
            {
                //mode = take (show only today class)
                var todaysSections = sections
                    .Where(s =>
                        string.Equals(s.Day, todayName, StringComparison.OrdinalIgnoreCase) ||
                        s.Day.StartsWith(todayAbbrev, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                vm = todaysSections.Select(s =>
                {
                    var canStart = currentTime >= s.StartTime && currentTime <= s.EndTime;
                    string? reason = null;
                    if (currentTime < s.StartTime) reason = "Class not started yet.";
                    else if (currentTime > s.EndTime) reason = "Class already ended.";

                    return new ClassSelectionItemVM
                    {
                        Section = s,
                        CanStart = canStart,
                        DisabledReason = reason
                    };
                }).ToList();
            }

            return View(vm);
        }

        public IActionResult MarkAttendance(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId))
                return RedirectToAction("ClassSelection");

            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var section = db.ClassSections
                .Include(c => c.Course)
                .Include(c => c.Room)
                .Include(c => c.Semester)
                .FirstOrDefault(c => c.SectionId == sectionId && c.StaffId == staffId);

            if (section == null)
                return RedirectToAction("ClassSelection");

            return View(section);
        }

        // generate a simple code for the attendance code (can be used for join code also)
        private static string GenerateSimpleCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var rnd = new Random();
            return new string(Enumerable.Range(0, 5)
                .Select(_ => chars[rnd.Next(chars.Length)])
                .ToArray());
        }

        [HttpPost]
        [Authorize(Roles = "Staff")]
        public IActionResult StartAttendanceSession(string sectionId)
        {
            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var section = db.ClassSections.FirstOrDefault(s => s.SectionId == sectionId && s.StaffId == staffId);
            if (section == null) return NotFound();

            section.CurrentQrToken = Guid.NewGuid().ToString("N");
            section.CurrentQrExpiry = DateTime.Now.AddSeconds(120);
            section.IsAttendanceSessionActive = true;

            if (string.IsNullOrEmpty(section.AttendanceCode))
                section.AttendanceCode = GenerateSimpleCode();

            db.SaveChanges();

            return Json(new
            {
                success = true,
                token = section.CurrentQrToken,
                manualCode = section.AttendanceCode
            });
        }

        [HttpPost]
        [Authorize(Roles = "Staff")]
        public IActionResult RefreshQrToken(string sectionId)
        {
            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var section = db.ClassSections.FirstOrDefault(s => s.SectionId == sectionId && s.StaffId == staffId);
            if (section == null || !section.IsAttendanceSessionActive) return BadRequest();

            section.CurrentQrToken = Guid.NewGuid().ToString("N");
            section.CurrentQrExpiry = DateTime.Now.AddSeconds(120);
            db.SaveChanges();

            return Json(new { success = true, token = section.CurrentQrToken });
        }

        [HttpPost]
        [Authorize(Roles = "Staff")]
        public IActionResult RegenerateManualCode(string sectionId)
        {
            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var section = db.ClassSections.FirstOrDefault(s => s.SectionId == sectionId && s.StaffId == staffId);
            if (section == null || !section.IsAttendanceSessionActive) return BadRequest();

            section.AttendanceCode = GenerateSimpleCode();
            db.SaveChanges();

            return Json(new { success = true, manualCode = section.AttendanceCode });
        }

        [HttpPost]
        [Authorize(Roles = "Staff")]
        public IActionResult StopAttendanceSession(string sectionId)
        {
            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var section = db.ClassSections.FirstOrDefault(s => s.SectionId == sectionId && s.StaffId == staffId);
            if (section == null)
                return Json(new { success = false, message = "Section not found" });

            section.IsAttendanceSessionActive = false;
            section.CurrentQrToken = null;
            section.CurrentQrExpiry = null;
            db.SaveChanges();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, redirectUrl = Url.Action("SessionSummary", new { sectionId }) });
            }

            return RedirectToAction("SessionSummary", new { sectionId });
        }

        public IActionResult SessionSummary(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId))
                return RedirectToAction("ClassSelection", new { mode = "take" });

            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var today = DateOnly.FromDateTime(DateTime.Now);

            var section = db.ClassSections
                .Include(c => c.Course)
                .Include(c => c.Room)
                .Include(c => c.Semester)
                .FirstOrDefault(c => c.SectionId == sectionId && c.StaffId == staffId);

            if (section == null)
                return RedirectToAction("ClassSelection", new { mode = "take" });

            // Get all enrolled students for this section
            var enrolledStudents = db.Enrollments
                .Include(e => e.Student)
                .Where(e => e.ClassSectionId == sectionId 
                 && e.Type == "Enrolled" 
                 && e.Status == "Approved")
                .Select(e => e.Student)
                .ToList();

            // Get attendance records for today
            var todayAttendances = db.Attendances
                .Include(a => a.Student)
                .Where(a => a.ClassSectionId == sectionId && a.Date == today)
                .ToList();

            var presentStudents = todayAttendances
                .Where(a => a.Status == "Present")
                .Select(a => a.Student)
                .ToList();

            var flaggedAttendances = todayAttendances
                .Where(a => a.IsFlaged)
                .ToList();

            var absentStudents = enrolledStudents
                .Where(s => !todayAttendances.Any(a => a.StudentId == s.Id))
                .ToList();

            var totalEnrolled = enrolledStudents.Count;
            var presentCount = presentStudents.Count;
            var absentCount = absentStudents.Count;
            var flaggedCount = flaggedAttendances.Count;
            var attendancePercentage = totalEnrolled > 0 
                ? Math.Round((double)presentCount / totalEnrolled * 100, 1) 
                : 0;

            var viewModel = new SessionSummaryVM
            {
                Section = section,
                TotalEnrolled = totalEnrolled,
                PresentCount = presentCount,
                AbsentCount = absentCount,
                FlaggedCount = flaggedCount,
                AttendancePercentage = attendancePercentage,
                PresentStudents = presentStudents,
                AbsentStudents = absentStudents,
                FlaggedAttendances = flaggedAttendances,
                Today = today
            };

            ViewData["Title"] = "Session Summary";
            return View(viewModel);
        }

        [HttpGet]
        [Authorize(Roles = "Staff")]
        public IActionResult LiveAttendanceRecord(string sectionId)
        {
            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var today = DateOnly.FromDateTime(DateTime.Now);

            var section = db.ClassSections.FirstOrDefault(s => s.SectionId == sectionId && s.StaffId == staffId);
            if (section == null) return Unauthorized();

            var items = db.Attendances
                .Include(a => a.Student)
                .Where(a => a.ClassSectionId == sectionId && a.Date == today)
                .OrderByDescending(a => a.RegisteredAt)
                .Take(50) // latest 50 records
                .Select(a => new
                {
                    time = a.RegisteredAt.ToString("HH:mm:ss"),
                    studentName = a.Student.FirstName + " " + a.Student.LastName,
                    source = (a.UserAgent ?? "").StartsWith("QR||") ? "QR Scan"
                        : (a.UserAgent ?? "").StartsWith("MANUAL||") ? "Manual Code"
                        : "Unknown",
                    status = a.Status
                })
                .ToList();

            return Json(new { success = true, items });
        }


        public IActionResult AttendanceManagement(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId))
                return RedirectToAction("ClassSelection", new { mode = "view" });

            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var section = db.ClassSections
                .Include(c => c.Course)
                .Include(c => c.Room)
                .Include(c => c.Semester)
                .FirstOrDefault(c => c.SectionId == sectionId && c.StaffId == staffId);

            if (section == null)
                return RedirectToAction("ClassSelection", new { mode = "view" });

            ViewData["Section"] = section; 
            ViewData["Title"] = "Attendance Management";
            return View(); 
        }


        public IActionResult AttendanceReport()
        {
            ViewData["Title"] = "Attendance Analytics";
            return View();
        }
    }
}