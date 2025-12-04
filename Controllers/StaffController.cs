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

        public IActionResult ClassSelection(string mode = "take")
        {
            ViewData["Title"] = mode == "view"
                ? "Select Class to View Attendance"
                : "Select Class to Take Attendance";
            ViewData["Mode"] = mode;

            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var sections = db.ClassSections
                .Include(c => c.Course)
                .Include(c => c.Room)
                .Include(c => c.Semester)
                .Where(c => c.StaffId == staffId)
                .OrderBy(c => c.Day)
                .ThenBy(c => c.StartTime)
                .ToList();

            return View(sections); 
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