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

            if (string.IsNullOrEmpty(sectionId))
                return BadRequest("Section is required.");

            //load class section and verify it exists
            var section = db.ClassSections.FirstOrDefault(s => s.SectionId == sectionId);
            if (section == null)
                return BadRequest("Invalid section ID.");

            if (!section.IsAttendanceSessionActive)
                return BadRequest("Attendance session is not active.");

            // validate QR (token and expiry)
            bool validQr = !string.IsNullOrEmpty(qrToken)
                           && qrToken == section.CurrentQrToken
                           && section.CurrentQrExpiry.HasValue
                           && section.CurrentQrExpiry.Value >= now;

            // validate manual code
            bool validCode = !string.IsNullOrEmpty(manualCode)
                             && manualCode == section.AttendanceCode;

            if (!validQr && !validCode)
                return BadRequest("Invalid or expired token/code.");

            // prevent duplicate attendance
            bool already = db.Attendances.Any(a =>
                a.ClassSectionId == sectionId &&
                a.StudentId == studentId &&
                a.Date == today);

            if (already)
                return BadRequest("You have already marked attendance for this class today.");

            // suspicious IP check
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

            //create attendance
            var att = new Attendance
            {
                Id = Guid.NewGuid().ToString("N"),
                ClassSectionId = sectionId,  // FK property
                ClassSection = section, // Navigation
                StudentId = studentId,
                Date = today,
                RegisteredAt = now,
                Status = "Present",
                IpAddress = ip,
                UserAgent = ua,
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