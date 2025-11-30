using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SHAPEE.Models; // ? ADD THIS LINE

namespace SHAPEE.Controllers;

public class DashboardController : Controller
{
    private readonly DB db;

    public DashboardController(DB context)
    {
        db = context;
    }

    // GET: Dashboard/Student
    [Authorize(Roles = "Student")]
    public IActionResult Student()
    {
        ViewData["Title"] = "Student Dashboard";
        return View("Student");
    }

    // GET: Dashboard/ViewAttendance
    [Authorize(Roles = "Student")]
    public IActionResult ViewAttendance()
    {
        ViewData["Title"] = "View Attendance";
        return View("ViewAttendance");
    }

    // GET: Dashboard/AttendanceHistory
    [Authorize(Roles = "Student")]
    public IActionResult AttendanceHistory()
    {
        ViewData["Title"] = "Attendance History";
        return View("AttendanceHistory");
    }

    // GET: Dashboard/Staff
    [Authorize(Roles = "Staff")]
    public IActionResult Staff()
    {
        ViewData["Title"] = "Staff Dashboard";
        return View("Staff");
    }

    // GET: Dashboard/Admin
    [Authorize(Roles = "Admin")]
    public IActionResult Admin()
    {
        ViewData["Title"] = "Admin Dashboard";
        return View("Admin");
    }

    // GET: Dashboard/UserManagement
    [Authorize(Roles = "Admin")]
    public IActionResult UserManagement()
    {
        ViewData["Title"] = "User Management";
        var students = db.Students
            .OrderBy(s => s.Id)
            .ToList();
        return View(students);
    }
}