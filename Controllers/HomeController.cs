using Microsoft.AspNetCore.Mvc;

namespace SHAPEE.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
