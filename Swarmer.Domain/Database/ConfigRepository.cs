using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Swarmer.Domain.Models;
using System.Text.Json;

namespace Swarmer.Domain.Database;

public sealed class ConfigRepository
{
	private readonly AppDbContext _appDbContext;
	private readonly SwarmerConfig _config;

	public ConfigRepository(IOptions<SwarmerConfig> options, AppDbContext appDbContext)
	{
		_appDbContext = appDbContext;
		_config = options.Value;
	}

	public async Task UpdateAccessToken(string newToken)
	{
		try
		{
			SwarmerConfig newConfig = _config.Copy();
			newConfig.AccessToken = newToken;
			string serNewConfig = JsonSerializer.Serialize(newConfig);

			ConfigurationEntity currentDbConfig = await _appDbContext.BotConfigurations.FirstAsync(c => c.BotName == "Swarmer");
			currentDbConfig.JsonConfig = serNewConfig;

			await _appDbContext.SaveChangesAsync();
		}
		catch (Exception ex)
		{
			string errorMessage = ex switch
			{
				DbUpdateException => "Failed to update application configuration in the database",
				_                 => "Failed to update application configuration",
			};

			Log.Error(ex, "{ErrorMessage}", errorMessage);
			throw;
		}
	}
}
