using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers;

public class HomeController : Controller
{
    // GET: Home/Index
    public IActionResult Index()
    {
        return View();
    }
}
