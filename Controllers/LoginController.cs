using Microsoft.AspNetCore.Mvc;

namespace PIOGHOASIS.Controllers
{
    public class LoginController : Controller
    {
        [HttpGet]                 // GET /Login  y  GET /Login/Index
        public IActionResult Index()
        {
            return View();        // busca Views/Login/Index.cshtml
        }

        [HttpPost]
        public IActionResult Index(string Usuario, string Contrasena)
        {
            // lógica de autenticación (luego la agregamos)
            return View();
        }
    }
}
