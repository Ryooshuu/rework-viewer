using osu.Framework.Platform;
using rework_viewer.Database;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var storage = new NativeStorage(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "rework-viewer"));
var access = new RealmAccess(storage, "client.realm");

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton(access);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");

app.MapFallbackToFile("index.html");

app.Run();
