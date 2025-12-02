using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SHAPEE.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController : Controller
    {
        // GET: /Staff
        public IActionResult Dashboard()
        {
            ViewData["Title"] = "Staff Dashboard";
            return View();    
        }
    }
}