using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Newtonsoft.Json;

namespace rework_viewer.Results;

public abstract class JsonResult : ActionResult, IStatusCodeActionResult
{
    protected JsonResult()
    {
    }

    protected JsonResult(int statusCode)
    {
        StatusCode = statusCode;
    }

    public int? StatusCode { get; set; } = 200;

    protected abstract object? CreateJsonData();

    public override async Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var serialized = JsonConvert.SerializeObject(CreateJsonData());

        var result = new ContentResult
        {
            ContentType = "application/json",
            StatusCode = StatusCode,
            Content = serialized
        };

        await result.ExecuteResultAsync(context);
    }
}
