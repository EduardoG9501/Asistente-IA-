using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TradingAssistant.Core.Interfaces;
using TradingAssistant.Core.Services;
using TradingAssistant.Infrastructure.Data;
using TradingAssistant.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Agregar soporte para controladores y Swagger
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serializar DateTime siempre con sufijo UTC 'Z' para que el navegador convierta a hora local automáticamente
        options.JsonSerializerOptions.Converters.Add(new TradingAssistant.API.UtcDateTimeConverter());
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Registrar DbContext con SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Data Source=trading_assistant.db";
builder.Services.AddDbContext<TradingDbContext>(options =>
    options.UseSqlite(connectionString, 
        b => b.MigrationsAssembly("TradingAssistant.API")));

// 3. Registrar clientes HTTP para APIs externas con inyección de dependencias
builder.Services.AddHttpClient<IMarketDataService, TwelveDataClient>(client =>
{
    client.BaseAddress = new Uri("https://api.twelvedata.com/");
});

builder.Services.AddHttpClient<IIaService, GeminiClient>();

// 4. Registrar servicios de lógica del asistente y notificaciones
builder.Services.AddSingleton<IIndicatorService, IndicatorService>();
builder.Services.AddSingleton<ISignalService, SignalService>();
builder.Services.AddSingleton<IRiskManagementService, RiskManagementService>();
builder.Services.AddHttpClient<INotificationService, TelegramBotService>();

// 5. Registrar el Worker de Background para ejecución recurrente del bot
builder.Services.AddHostedService<TradingBotWorker>();

var app = builder.Build();

// 6. Configurar el pipeline de solicitudes HTTP
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
