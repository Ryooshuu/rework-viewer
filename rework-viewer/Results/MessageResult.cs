namespace rework_viewer.Results;

public class MessageResult : JsonResult
{
    public MessageResult(string message)
    {
        Message = message;
    }

    public MessageResult(string message, int statusCode)
        : base(statusCode)
    {
        Message = message;
    }
    
    public string Message { get; set; }

    protected override object? CreateJsonData()
        => new
        {
            code = StatusCode,
            message = Message
        };
}
