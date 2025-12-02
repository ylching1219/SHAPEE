using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SHAPEE.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        // GET: /Student
        public IActionResult Index()
        {
            ViewData["Title"] = "Student Dashboard";
            return View("Student");
        }

        public IActionResult ViewAttendance()
        {
            ViewData["Title"] = "View Attendance";
            return View("ViewAttendance");
        }

        public IActionResult AttendanceHistory()
        {
            ViewData["Title"] = "Attendance History";
            return View("AttendanceHistory");
        }
    }
}