var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

// Hands the browser its OIDC settings so nothing is hardcoded in the JavaScript.
app.MapGet("/config.json", (IConfiguration configuration) =>
    configuration.GetSection("Oidc").GetChildren().ToDictionary(c => c.Key, c => c.Value));

app.Run();
