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
            ViewData["SectionId"] = sectionId;
            ViewData["QrToken"] = token;
            return View("ViewAttendance");
        }

        public IActionResult AttendanceHistory()
        {
            ViewData["Title"] = "Attendance History";
            return View("AttendanceHistory");
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

        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var user = await db.Users
                .OfType<Student>()
                .Include(s => s.Program)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound();

            return View("~/Views/Shared/Profile.cshtml", user);
        }
    }
}