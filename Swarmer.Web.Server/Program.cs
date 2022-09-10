using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Swarmer.Domain.Models;
using Swarmer.Domain.Services;
using Swarmer.Web.Server.Endpoints;
using System.Globalization;
using TwitchLib.Api;
using TwitchLib.Api.Interfaces;

namespace Swarmer.Web.Server;

internal static class Program
{
	public static async Task Main(string[] args)
	{
		CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

		WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

		if (builder.Environment.IsProduction())
		{
			SetConfigFromDb(builder);
		}

		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Information()
			.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u4}] {Message:lj}{NewLine}{Exception}")
			.WriteTo.Sentry(o =>
			{
				o.Dsn = "https://2ff402b0e3df4a9aa8a42ed61d8343b9@o1402334.ingest.sentry.io/6734470";
				o.MinimumEventLevel = LogEventLevel.Information;
				o.TracesSampleRate = 0.5;
				o.Environment = builder.Environment.EnvironmentName;
			})
			.CreateLogger();

		builder.Logging.ClearProviders();
		builder.Host.UseSerilog();

		Log.Information("Starting application");

		builder.Services.AddRazorPages();

		builder.Services.AddEndpointsApiExplorer();

		builder.Services.AddSwaggerGen(options =>
		{
			options.SwaggerDoc("Main", new() { Version = "Main", Title = "Swarmer API" });
		});

		builder.Services.AddCors();

		TwitchAPI twitchApi = new() { Settings = { AccessToken = builder.Configuration["AccessToken"], ClientId = builder.Configuration["ClientId"] } };

		builder.Services.AddSingleton(typeof(ITwitchAPI), twitchApi);
		builder.Services.AddSingleton<StreamProvider>();
		builder.Services.AddHostedService<StreamRefresherService>();
		builder.Services.AddHostedService<DdStreamsPostingService>();
		builder.Services.AddHostedService<AccessTokenNotifierService>();
		builder.Services.AddDbContext<DbService>();

		builder.Services.AddSingleton<SwarmerDiscordClient>();
		builder.Services.AddHostedService<KeepDynoAliveService>();
		builder.Services.AddHttpClient();

		WebApplication app = builder.Build();

		app.Lifetime.ApplicationStopping.Register(() =>
		{
			Log.Information("Program shut-down requested");
			_ = app.Services.GetRequiredService<SwarmerDiscordClient>().Client.LogoutAsync();
		});

		if (app.Environment.IsDevelopment())
		{
			app.UseDeveloperExceptionPage();
			app.UseWebAssemblyDebugging();
		}

		app.UseSerilogRequestLogging();

		app.UseStaticFiles();

		app.UseSwagger();

		app.UseSwaggerUI(options =>
		{
			options.InjectStylesheet("/swagger-ui/SwaggerDarkReader.css");
			options.SwaggerEndpoint("/swagger/Main/swagger.json", "Main");
		});

		app.UseBlazorFrameworkFiles();

		app.UseRouting();

		app.MapRazorPages();

		app.MapFallbackToFile("index.html");

		app.RegisterSwarmerEndpoints();

		app.UseHttpsRedirection();

		app.UseCors(policyBuilder => policyBuilder.AllowAnyOrigin());

		await app.Services.GetRequiredService<SwarmerDiscordClient>().InitAsync();

		CancellationTokenSource tokenSource = new();

		try
		{
			await app.RunAsync(tokenSource.Token);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Caught error in main application loop");
		}
		finally
		{
			Log.Information("Shut-down complete");
			Log.CloseAndFlush();
		}
	}

	private static void SetConfigFromDb(WebApplicationBuilder builder)
	{
		using DbService dbService = new();
		string jsonConfig = dbService.SwarmerConfig.AsNoTracking().First().JsonConfig;
		string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DbConfig.json");
		File.WriteAllText(configPath, jsonConfig);
		builder.Configuration.AddJsonFile(configPath, optional: true, reloadOnChange: true);
	}
}
