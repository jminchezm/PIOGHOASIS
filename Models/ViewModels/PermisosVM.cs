using Microsoft.AspNetCore.Mvc.Rendering;

namespace PIOGHOASIS.Models.ViewModels
{
    public class PermisoModuloItemVM
    {
        public int ModuloID { get; set; }
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public bool PuedeAcceder { get; set; }
    }

    public class PermisosRolVM
    {
        public string RolID { get; set; } = "";
        public string? RolNombre { get; set; }
        public List<PermisoModuloItemVM> Items { get; set; } = new();
        public IEnumerable<SelectListItem>? Roles { get; set; }
    }

    public class RolPermisosRowVM
    {
        public string RolID { get; set; } = "";
        public string RolNombre { get; set; } = "";
        public List<string> ModulosPermitidos { get; set; } = new();
    }
}
