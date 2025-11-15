using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers;

public class DashboardController : Controller
{
    // GET: Dashboard/Index - redirect to home (temporary)
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Home");
    }

    // GET: Dashboard/Student
    public IActionResult Student()
    {
        ViewData["Title"] = "Student Dashboard";
        return View("Index"); 
    }

    // GET: Dashboard/Teacher
    public IActionResult Teacher()
    {
        ViewData["Title"] = "Teacher Dashboard";
        return View("Teacher");
    }

    // GET: Dashboard/Admin
    public IActionResult Admin()
    {
        ViewData["Title"] = "Admin Dashboard";
        return View("Admin");
    }
}


