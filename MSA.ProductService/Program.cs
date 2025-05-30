using MSA.ProductService.Entities;
using MSA.Common.MongoDB;
using MSA.Common.Contracts.Domain;
using MassTransit;
using MSA.Common.PostgresMassTransit.MassTransit;
using MSA.Common.Security.Authentication;
using MSA.Common.Security.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using MSA.Common.Contracts.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMongo()
    .AddRepositories<Product>("product")
    .AddMassTransitWithRabbitMQ()
    .AddMSAAuthentication()
    .AddMSAAuthorization(opt =>
    {
        opt.AddPolicy("read_access", policy =>
        {
            policy.RequireClaim("scope", "productapi.read");
        });
    });
builder.Services.AddControllers(options =>
{
    options.SuppressAsyncSuffixInActionNames = false;
});

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
//builder.Services.AddSwaggerGen();

var srvUrlsSetting = builder.Configuration.GetSection(nameof(ServiceUrlsSetting)).Get<ServiceUrlsSetting>();
builder.Services.AddSwaggerGen(options =>
{
    var scheme = new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"{srvUrlsSetting.IdentityServiceUrl}/connect/authorize"),
                TokenUrl = new Uri($"{srvUrlsSetting.IdentityServiceUrl}/connect/token"),
                Scopes = new Dictionary<string, string>
                {
                     { "productapi.read", "Access read operations" },
                     { "productapi.write", "Access write operations" }
                }
            }
        },
        Type = SecuritySchemeType.OAuth2
    };

    options.AddSecurityDefinition("OAuth", scheme);

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
         {
             new OpenApiSecurityScheme
             {
                 Reference = new OpenApiReference { Id = "OAuth", Type = ReferenceType.SecurityScheme }
             },
             new List<string> { }
         }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    //app.UseSwaggerUI();
    app.UseSwaggerUI(options =>
    {
        options.OAuthClientId("product-swagger");
        options.OAuthScopes("profile", "openid");
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");
app.MapControllers();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
