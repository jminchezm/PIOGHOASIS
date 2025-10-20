using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;

namespace PIOGHOASIS.Filters
{
    public class RequireCajaAbiertaAttribute : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var abierta = await db.cajas.AnyAsync(c => c.EstadoCajaID == 1);

            if (abierta)
            {
                await next();
                return;
            }

            // ¿Es AJAX?
            var isAjax = context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (isAjax)
            {
                context.Result = new JsonResult(new
                {
                    ok = false,
                    reason = "nocaja",
                    message = "Debes abrir una caja para usar Reservaciones.",
                    redirectUrl = "/Caja/Index"
                })
                { StatusCode = StatusCodes.Status409Conflict };
            }
            else
            {
                // O redirige con mensaje
                var controller = (Controller)context.Controller;
                controller.TempData["Warn"] = "Debes abrir una caja para usar Reservaciones.";
                context.Result = new RedirectToActionResult("Index", "Caja", null);
            }
        }
    }
}
