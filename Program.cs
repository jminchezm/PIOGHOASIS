using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Infraestructure.Email;
using Rotativa.AspNetCore;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();


// AUTENTICACIÓN POR COOKIES
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Index";
        options.LogoutPath = "/Login/Salir";
        options.AccessDeniedPath = "/Login/Index";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// DbContext (ajusta tu cadena de conexión/nombre)
builder.Services.AddDbContext<AppDbContext>
(
    opt =>
    {
        opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
);

//SMTP correo
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, SmtpEmailSender>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//--------------------------------
//builder.Services.AddDbContext<AppDbContext>(opt =>
//    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

//builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
//builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

//--------------------------------

app.UseHttpsRedirection();
app.UseStaticFiles();

RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa");

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();

//RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa");

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


