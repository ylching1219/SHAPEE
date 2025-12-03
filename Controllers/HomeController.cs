using Microsoft.AspNetCore.Mvc;

namespace SHAPEE.Controllers;

public class HomeController(DB db) : Controller
{
    // GET: Home/Index
    public IActionResult Index()
    {
        var items = db.Students;
        return View(items); 
    }
}
