using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Swarmer.Domain;
using Swarmer.Domain.Database;
using Swarmer.Domain.Discord;
using Swarmer.Domain.Models;
using Swarmer.Domain.Twitch;
using Swarmer.Web.Server.Endpoints;
using System.Globalization;
using System.Text;
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

		builder.Services.AddOptions<SwarmerConfig>()
			.Bind(builder.Configuration.GetRequiredSection("SwarmerConfig"))
			.ValidateDataAnnotations()
			.ValidateOnStart();

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
		builder.Services.AddHostedService<KeepAppAliveService>();
		builder.Services.AddDbContext<AppDbContext>(options =>
		{
			const string key = "PostgresConnectionString";
			string connectionString = builder.Environment.IsProduction()
				? Environment.GetEnvironmentVariable(key) ?? throw new($"Envvar {key} not found.")
				: builder.Configuration[key] ?? throw new($"{key} was not found in configuration.");

			options.UseNpgsql(connectionString);
		});

		builder.Services.AddSingleton<ITwitchAPI, TwitchAPI>(services =>
		{
			SwarmerConfig config = services.GetRequiredService<IOptions<SwarmerConfig>>().Value;
			TwitchAPI api = new()
			{
				Settings =
				{
					AccessToken = config.AccessToken,
					ClientId = config.ClientId,
					Secret = config.ClientSecret,
				},
			};

			return api;
		});

		builder.Services.AddSingleton<SwarmerDiscordClient>(services =>
		{
			const GatewayIntents gatewayIntents = GatewayIntents.AllUnprivileged & ~GatewayIntents.GuildInvites & ~GatewayIntents.GuildScheduledEvents;
			IOptions<SwarmerConfig> options = services.GetRequiredService<IOptions<SwarmerConfig>>();
			SwarmerDiscordClient client = new(options, new() { GatewayIntents = gatewayIntents });

			return client;
		});

		builder.Services.AddScoped<SwarmerRepository>();
		builder.Services.AddScoped<ConfigRepository>();

		builder.Services.AddHttpClient();

		WebApplication app = builder.Build();
		app.Lifetime.ApplicationStopping.Register(() =>
		{
			SwarmerDiscordClient client = app.Services.GetRequiredService<SwarmerDiscordClient>();
			Task.Run(() => client.StopAsync());
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
			await Log.CloseAndFlushAsync();
		}
	}

	private static async Task SetConfigFromDb(WebApplicationBuilder builder)
	{
		DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
			.UseNpgsql(Environment.GetEnvironmentVariable("PostgresConnectionString") ?? throw new("Envvar PostgresConnectionString not found."))
			.Options;

		await using AppDbContext appDbContext = new(options);

		string jsonConfig = appDbContext.BotConfigurations.AsNoTracking().First(c => c.BotName == "Swarmer").JsonConfig;
		using MemoryStream configStream = new(Encoding.UTF8.GetBytes(jsonConfig));

		builder.Configuration.AddJsonStream(configStream);
	}
}
