using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace Swarmer.Models.Logging;

public static class DiscordSinkExtensions
{
	public static LoggerConfiguration Discord(
		this LoggerSinkConfiguration loggerConfiguration,
		ulong webhookId,
		string webhookToken,
		LogEventLevel minimumLogLevel = LogEventLevel.Verbose)
	{
		return loggerConfiguration.Sink(new DiscordSink(webhookId, webhookToken, minimumLogLevel));
	}
}
