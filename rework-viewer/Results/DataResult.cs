namespace rework_viewer.Results;

public class DataResult : JsonResult
{
    public DataResult(object? value)
    {
        Value = value;
    }

    public DataResult(object? value, int statusCode)
        : base(statusCode)
    {
        Value = value;
    }
    
    public object? Value { get; set; }

    protected override object? CreateJsonData()
        => new
        {
            code = StatusCode,
            data = Value
        };
}
