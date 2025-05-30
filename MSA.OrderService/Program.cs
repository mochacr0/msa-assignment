using MSA.OrderService.Domain;
using MSA.OrderService.Infrastructure.Data;
using MSA.Common.Contracts.Settings;
using MSA.Common.PostgresMassTransit.PostgresDB;
using MSA.OrderService.Services;
using MSA.Common.PostgresMassTransit.MassTransit;
using MSA.OrderService.StateMachine;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using MSA.OrderService.Infrastructure.Saga;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

PostgresDBSetting serviceSetting = builder.Configuration.GetSection(nameof(PostgresDBSetting)).Get<PostgresDBSetting>();
builder.Services
     .AddPostgres<MainDbContext>()
     .AddPostgresRepositories<MainDbContext, Order>()
     .AddPostgresRepositories<MainDbContext, Product>()
     .AddPostgresUnitofWork<MainDbContext>()
     //.AddMassTransitWithRabbitMQ();
     .AddMassTransitWithPostgresOutbox<MainDbContext>(cfg =>
     {
         cfg.AddSagaStateMachine<OrderStateMachine, OrderState>()
            .EntityFrameworkRepository(r =>
            {
                r.ConcurrencyMode = ConcurrencyMode.Pessimistic;

                r.LockStatementProvider = new PostgresLockStatementProvider();

                r.AddDbContext<DbContext, OrderStateDbContext>((provider, builder) =>
                {
                    builder.UseNpgsql(serviceSetting.ConnectionString, n =>
                    {
                        n.MigrationsAssembly(Assembly.GetExecutingAssembly().GetName().Name);
                        n.MigrationsHistoryTable($"__{nameof(OrderStateDbContext)}");
                    });
                });
            });
     });

builder.Services.AddHttpClient<IProductService, ProductService>(cfg =>
{
    cfg.BaseAddress = new Uri("https://localhost:5002");
});

builder.Services.AddControllers(opt =>
{
    opt.SuppressAsyncSuffixInActionNames = false;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

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
