using Microsoft.AspNetCore.Mvc;

namespace SHAPEE.Controllers;

public class HomeController : Controller
{
    // GET: Home/Index
    public IActionResult Index()
    {
        return View();
    }
}
