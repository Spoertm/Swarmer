using Swarmer.Web.Server.Endpoints;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("Main", new() { Version = "Main", Title = "Swarmer API" });
});

WebApplication app = builder.Build();

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

app.Run();
