using Microsoft.Data.SqlClient;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();















var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();

//app.MapGet("/ado-check", async (IConfiguration cfg) =>
//{
//    var cs = cfg.GetConnectionString("DefaultConnection"); // tu cadena del appsettings
//    try
//    {
//        await using var con = new SqlConnection(cs);
//        await con.OpenAsync();                   // si falla, lanzará excepción

//        // Ejecutamos algo simple para comprobar
//        await using var cmd = new SqlCommand("SELECT DB_NAME()", con);
//        var dbName = (string)await cmd.ExecuteScalarAsync();

//        return Results.Ok($"ADO.NET OK. Conectado a: {dbName}");
//    }
//    catch (Exception ex)
//    {
//        return Results.Problem($"Error ADO.NET: {ex.Message}");
//    }
//});


