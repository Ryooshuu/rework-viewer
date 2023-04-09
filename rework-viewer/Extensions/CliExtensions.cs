using CliWrap;
using osu.Framework.Logging;

namespace rework_viewer.Extensions;

public static class CliExtensions
{
    private static readonly Logger logger;

    static CliExtensions()
    {
        logger = Logger.GetLogger("cli");
    }

    public static CommandTask<CommandResult> ExecuteWithLogging(this Command command)
        => command.WithStandardOutputPipe(PipeTarget.ToDelegate(log))
           .WithStandardErrorPipe(PipeTarget.ToDelegate(log))
           .ExecuteAsync();

    private static void log(string message)
    {
        logger.Add(message);
    }
}
