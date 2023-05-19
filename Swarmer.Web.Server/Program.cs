using Discord;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Swarmer.Domain;
using Swarmer.Domain.Database;
using Swarmer.Domain.Discord;
using Swarmer.Domain.Twitch;
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
			await SetConfigFromDb(builder);
		}

		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Warning()
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

		Log.Information("Starting application");

		builder.Services.AddRazorPages();

		builder.Services.AddEndpointsApiExplorer();

		builder.Services.AddSwaggerGen(options =>
		{
			options.EnableAnnotations();
			options.SwaggerDoc("Main", new()
			{
				Version = "Main",
				Title = "Swarmer API",
				Description = @"This API serves as a replacement for Twitch's, in case one is unable/unwilling to deal with the latter.
However only Devil Daggers and HYPER DEMON Twitch streams can be requested.",
			});
		});

		builder.Services.AddCors();

		builder.Services.AddSingleton<StreamProvider>();
		builder.Services.AddSingleton<IDiscordService, DiscordService>();

		builder.Services.AddHostedService<StreamRefresherService>();
		builder.Services.AddHostedService<StreamsPostingService>();
		builder.Services.AddHostedService<AccessTokenNotifierService>();
		builder.Services.AddHostedService<KeepAppAliveService>();
		builder.Services.AddDbContext<AppDbContext>(options =>
		{
			string connectionString = Environment.GetEnvironmentVariable("PostgresConnectionString") ?? throw new("Envvar PostgresConnectionString not found.");
			options.UseNpgsql(connectionString);
		});

		builder.Services.AddSingleton<ITwitchAPI, TwitchAPI>(_ => new() { Settings = { AccessToken = builder.Configuration["AccessToken"], ClientId = builder.Configuration["ClientId"] } });
		builder.Services.AddSingleton<SwarmerDiscordClient>(_ =>
		{
			const GatewayIntents gatewayIntents = GatewayIntents.AllUnprivileged & ~GatewayIntents.GuildInvites & ~GatewayIntents.GuildScheduledEvents;
			return new(builder.Configuration, new() { GatewayIntents = gatewayIntents });
		});

		builder.Services.AddScoped<SwarmerRepository>();

		builder.Services.AddHttpClient();

		WebApplication app = builder.Build();

		app.Lifetime.ApplicationStopping.Register(() =>
		{
			app.Services.GetRequiredService<SwarmerDiscordClient>().StopAsync().ConfigureAwait(false);
		});

		if (app.Environment.IsDevelopment())
		{
			app.UseDeveloperExceptionPage();
			app.UseWebAssemblyDebugging();
		}

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

	private static async Task SetConfigFromDb(WebApplicationBuilder builder)
	{
		DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
			.UseNpgsql(Environment.GetEnvironmentVariable("PostgresConnectionString") ?? throw new("Envvar PostgresConnectionString not found."))
			.Options;

		await using AppDbContext appDbContext = new(options);
		string jsonConfig = appDbContext.SwarmerConfig.AsNoTracking().First().JsonConfig;
		string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DbConfig.json");
		await File.WriteAllTextAsync(configPath, jsonConfig);
		builder.Configuration.AddJsonFile(configPath, optional: true, reloadOnChange: true);
	}
}
