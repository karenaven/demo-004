using Microsoft.EntityFrameworkCore;
using RodajeIA.Web.Components;
using RodajeIA.Web.Datos;
using RodajeIA.Web.Servicios;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// MaximumReceiveMessageSize: el campo del guion usa oninput y envía el texto completo en cada
// evento; con el valor por defecto (32 KB) un guion grande cortaría el circuito (FR-08).
// 1 MB cubre el máximo de 200 000 caracteres en UTF-8.
// DetailedErrors solo en Development: fuera de ahí el navegador no recibe detalles del servidor (M-05).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(o => o.DetailedErrors = builder.Environment.IsDevelopment())
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 1 * 1024 * 1024);

// Interactive Server: un contexto scoped viviría todo el circuito; los servicios crean uno por
// operación con la fábrica.
var cadenaConexion = builder.Configuration.GetConnectionString("RodajeIA")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:RodajeIA'.");
builder.Services.AddDbContextFactory<RodajeDbContext>(o => o.UseSqlite(cadenaConexion));

// Servicios que usa la UI; cada operación crea su propio contexto con la fábrica.
builder.Services.AddScoped<ServicioSeries>();
builder.Services.AddScoped<ServicioPersonajes>();
builder.Services.AddScoped<ServicioEpisodios>();
builder.Services.AddScoped<ServicioGuiones>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
