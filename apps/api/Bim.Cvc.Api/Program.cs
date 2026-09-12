using Bim.Cvc.Api.Persistence;
using Bim.Cvc.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

const string WebClientCorsPolicy = "WebClient";
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:3000"];

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebClientCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(WebClientCorsPolicy);
app.UseAuthorization();
app.UseMiddleware<SaveChangesMiddleware>();
app.MapControllers();

app.Run();
