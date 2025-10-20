using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

public static class ControllerRenderExtensions
{
    public static async Task<string> RenderViewAsync<TModel>(
        this Controller controller,
        string viewName,
        TModel model,
        bool partial = false)
    {
        controller.ViewData.Model = model;

        var serviceProvider = controller.HttpContext.RequestServices;
        var viewEngine = serviceProvider.GetRequiredService<ICompositeViewEngine>();

        var viewResult = partial
            ? viewEngine.FindView(controller.ControllerContext, viewName, false)
            : viewEngine.GetView(executingFilePath: null, viewPath: viewName, isMainPage: false);

        if (!viewResult.Success)
            throw new InvalidOperationException($"No se encontró la vista '{viewName}'.");

        await using var sw = new StringWriter();
        var viewContext = new ViewContext(
            controller.ControllerContext,
            viewResult.View,
            controller.ViewData,
            controller.TempData,
            sw,
            new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return sw.ToString();
    }
}
