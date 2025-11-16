using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SHAPEE.Controllers;

public class DashboardController : Controller
{
    // GET: Dashboard/Index - redirect to home (temporary)
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Home");
    }

    // GET: Dashboard/Student
    [Authorize(Roles = "Student")]
    public IActionResult Student()
    {
        ViewData["Title"] = "Student Dashboard";
        return View("Index"); 
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
}


