using Swarmer.Web.Server.Endpoints;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("Main", new() { Version = "Main", Title = "Swarmer API" });
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseDeveloperExceptionPage();
}

app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
	options.InjectStylesheet("/swagger-ui/SwaggerDarkReader.css");
	options.SwaggerEndpoint("/swagger/Main/swagger.json", "Main");
});

app.RegisterSwarmerEndpoints();

app.UseHttpsRedirection();

app.Run();
