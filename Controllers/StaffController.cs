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

            return View(sections); // Views/Staff/ClassSelection.cshtml
        }

        public IActionResult MarkAttendance(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId))
                return RedirectToAction("ClassSelection");

            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // ensure the section belongs to this staff
            var section = db.ClassSections
                .Include(c => c.Course)
                .Include(c => c.Room)
                .Include(c => c.Semester)
                .FirstOrDefault(c => c.SectionId == sectionId && c.StaffId == staffId);

            if (section == null)
                return RedirectToAction("ClassSelection");

            return View(section);   // pass ClassSection model to MarkAttendance
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

            ViewData["Section"] = section;   // used only to display class info
            ViewData["Title"] = "Attendance Management";
            return View();                   // still uses dummy rows inside the view
        }


        public IActionResult AttendanceReport()
        {
            ViewData["Title"] = "Attendance Analytics";
            return View();
        }
    }
}