using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PIOGHOASIS.Models;
using System.Diagnostics;

namespace PIOGHOASIS.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        //public IActionResult Index()
        //{
        //    return RedirectToAction("Index", "Login");
        //}

        // Opcional: protege el dashboard
        [Authorize]
        [HttpGet]
        public IActionResult Dashboard()
        {
            return View();  // busca Views/Home/Dashboard.cshtml
        }

        // Si alguien entra a /Home/Index, lo mandamos al Dashboard
        [HttpGet]
        public IActionResult Index() => RedirectToAction(nameof(Dashboard));

        //public IActionResult Index()
        //{
        //    return View();
        //}

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
