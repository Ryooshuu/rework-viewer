using Microsoft.AspNetCore.Mvc;
using rework_viewer.Database;
using rework_viewer.Results;
using rework_viewer.Reworks;

namespace rework_viewer.Controllers;

[ApiController]
[Route("[controller]")]
public class RulesetController : ControllerBase
{
    private readonly ILogger<RulesetController> logger;
    private readonly RealmAccess realm;

    public RulesetController(ILogger<RulesetController> logger, RealmAccess realm)
    {
        this.logger = logger;
        this.realm = realm;
    }

    [HttpGet]
    public ActionResult? Get(RulesetType type)
    {
        var ruleset = realm.Realm.All<RealmRuleset>()
           .FirstOrDefault(rule => rule.TypeInt == (int) type);
        
        return new DataResult(ruleset);
    }
}
