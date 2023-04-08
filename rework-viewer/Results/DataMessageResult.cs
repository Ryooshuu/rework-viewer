namespace rework_viewer.Results;

public class DataMessageResult : JsonResult
{
    public DataMessageResult(string message, object? value)
    {
        Message = message;
        Value = value;
    }

    public DataMessageResult(string message, object? value, int statusCode)
        : base(statusCode)
    {
        Message = message;
        Value = value;
    }
    
    public object? Value { get; set; }
    
    public string Message { get; set; }

    protected override object? CreateJsonData()
        => new
        {
            code = StatusCode,
            message = Message,
            data = Value
        };
}
