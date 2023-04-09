using osu.Framework.Extensions;
using osu.Framework.Platform;
using rework_viewer.Database;
using rework_viewer.Reworks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var storage = new NativeStorage(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "rework-viewer"));
var realm = new RealmAccess(storage, "client.realm");

ensureCoreRulesets();

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton(realm);

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

void ensureCoreRulesets()
{
    var types = Enum.GetValues<RulesetType>();
    var manager = new ReworkManager(storage, realm);

    var didntExist = new List<RulesetType>();
    
    realm.Write(r =>
    {
        foreach (var rulesetType in types)
        {
            if (!r.All<RealmRuleset>().Any(ruleset => ruleset.TypeInt == (int) rulesetType))
            {
                r.Add(new RealmRuleset { Type = rulesetType });
                didntExist.Add(rulesetType);
            }
        }
    });

    manager.PresentImport = imports =>
    {
        foreach (var import in imports)
        {
            import.PerformWrite(r =>
            {
                r.Protected = true;
                r.Name = r.Ruleset.Type.GetDescription();
                r.Author = "osu!dev";
                r.Description = "core";
            });
        }
    };
    
    manager.Import(@"D:\Projects\Projects\osu-irisu\osu.Game.Rulesets.Catch\bin\Release\net6.0\osu.Game.Rulesets.Catch.dll").Wait();

    // foreach (var rulesetType in didntExist)
    // {
    //     manager.Import(@"D:\Projects\Projects\osu-irisu\osu.Game.Rulesets.Catch\bin\Release\net6.0\osu.Game.Rulesets.Catch.dll");
    // }
}