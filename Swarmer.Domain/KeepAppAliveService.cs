using Serilog;
using Swarmer.Domain.Models;

namespace Swarmer.Domain;

public sealed class KeepAppAliveService(IHttpClientFactory httpClientFactory) : RepeatingBackgroundService
{
    protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

    protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
    {
        const string envVarName = "RAILWAY_PUBLIC_DOMAIN";
        if (Environment.GetEnvironmentVariable(envVarName) is not { } appUrl)
        {
            Log.Warning("{EnvVarName} environment variable not set", envVarName);
            return;
        }

        try
        {
            HttpClient client = httpClientFactory.CreateClient("KeepAlive");
            using HttpResponseMessage response = await client
                .SendAsync(new(HttpMethod.Head, $"https://{appUrl}"), HttpCompletionOption.ResponseHeadersRead, stoppingToken)
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to ping {AppUrl}", appUrl);
        }
    }
}
