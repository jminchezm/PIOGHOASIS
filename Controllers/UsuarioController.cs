using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;

namespace PIOGHOASIS.Controllers
{
    [Authorize]
    public class UsuarioController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public UsuarioController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // GET /Usuario/Avatar/USR0000001
        [HttpGet]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
        public async Task<IActionResult> Avatar(string id)
        {
            // Join: Usuario -> Empleado -> Persona (FotoPath)
            var data = await _db.usuarios
                .AsNoTracking()
                .Include(u => u.Empleado)
                    .ThenInclude(e => e.Persona)
                .Where(u => u.UsuarioID == id)
                .Select(u => new {
                    u.UsuarioID,
                    FotoPath = u.Empleado.Persona.FotoPath
                })
                .FirstOrDefaultAsync();

            string filePath;

            if (data?.FotoPath is string rel && !string.IsNullOrWhiteSpace(rel))
            {
                // normaliza y busca dentro de wwwroot
                var safeRel = rel.TrimStart('/', '\\');
                filePath = Path.Combine(_env.WebRootPath, safeRel);
                if (!System.IO.File.Exists(filePath))
                    filePath = Path.Combine(_env.WebRootPath, "img", "DefaultUsuario.png");
            }
            else
            {
                filePath = Path.Combine(_env.WebRootPath, "img", "DefaultUsuario.png");
            }

            var contentType = GetContentType(filePath);
            return PhysicalFile(filePath, contentType);
        }

        private static string GetContentType(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }
    }
}
