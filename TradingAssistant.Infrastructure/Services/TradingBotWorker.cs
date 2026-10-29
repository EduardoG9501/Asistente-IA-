using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingAssistant.Core.Interfaces;
using TradingAssistant.Core.Models;
using TradingAssistant.Core.Services;
using TradingAssistant.Infrastructure.Data;

namespace TradingAssistant.Infrastructure.Services;

public class TradingBotWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TradingBotWorker> _logger;
    private readonly IConfiguration _configuration;
    
    // Rastreador por símbolo para no repetir análisis en la misma vela cerrada
    private readonly Dictionary<string, DateTime> _lastProcessedCandleDates = new();

    private static readonly string[] MonitoredSymbols = new[]
    {
        // Forex
        "EUR/USD",
        "GBP/USD",
        "USD/JPY",
        "AUD/USD",
        "USD/CAD",
        // Acciones (US Stocks)
        "AAPL",
        "TSLA",
        "NVDA",
        "MSFT",
        "AMZN",
        // Metales Preciosos
        "XAU/USD",
        "XAG/USD",
        // Criptomonedas (Operación 24/7)
        "BTC/USD",
        "ETH/USD",
        "SOL/USD"
    };

    public TradingBotWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<TradingBotWorker> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando Servicio en Background del Asistente de Trading Multiactivo...");

        await InitializeDatabaseAsync();

        int checkIntervalSeconds = _configuration.GetValue<int>("TradingSettings:CheckIntervalSeconds", 60);
        string timeframe = _configuration["TradingSettings:Timeframe"] ?? "15m";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                var marketDataService = scope.ServiceProvider.GetRequiredService<IMarketDataService>();
                var indicatorService = scope.ServiceProvider.GetRequiredService<IIndicatorService>();
                var signalService = scope.ServiceProvider.GetRequiredService<ISignalService>();
                var riskService = scope.ServiceProvider.GetRequiredService<IRiskManagementService>();
                var iaService = scope.ServiceProvider.GetRequiredService<IIaService>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                // Consultar configuraciones de la cuenta
                var settings = await dbContext.AccountSettings.FirstOrDefaultAsync(stoppingToken);
                if (settings == null || !settings.BotEnabled)
                {
                    _logger.LogDebug("El bot de trading está desactivado o no tiene configuraciones.");
                    await Task.Delay(TimeSpan.FromSeconds(checkIntervalSeconds), stoppingToken);
                    continue;
                }

                // Escanear lista de activos principales
                foreach (var symbol in MonitoredSymbols)
                {
                    try
                    {
                        // 0. Omitir activos cuyo mercado esté cerrado (no descargar datos ni emitir alertas)
                        if (!MarketScheduleHelper.IsMarketOpen(symbol, out string scheduleDetails))
                        {
                            _logger.LogDebug("Omitiendo {Symbol} en escaneo automático: {Details}", symbol, scheduleDetails);
                            continue;
                        }

                        var candles = await marketDataService.GetHistoricalCandlesAsync(symbol, timeframe, 200);
                        if (candles == null || candles.Count == 0) continue;

                        await SyncCandlesToDbAsync(dbContext, candles, stoppingToken);

                        var lastClosedCandle = candles.OrderBy(c => c.Date).Last();

                        if (_lastProcessedCandleDates.TryGetValue(symbol, out var lastDate) && lastDate == lastClosedCandle.Date)
                        {
                            continue; // Ya procesamos esta vela para este símbolo
                        }

                        var technicals = indicatorService.CalculateIndicators(candles);
                        var signalType = signalService.DetermineSignal(technicals, candles, out string justification, out var orderType, out var strategyName, settings.Mode);

                        if (signalType != SignalType.NO_TRADE)
                        {
                            _logger.LogInformation("¡SEÑAL DETECTADA! [{Symbol}] Tipo: {Type}. Modo: {Mode}. Justificación: {Justification}", 
                                symbol, signalType, settings.Mode, justification);

                            var tradingSignal = riskService.ApplyRiskManagement(technicals, signalType, settings);
                            tradingSignal.TechnicalJustification = justification;
                            tradingSignal.OrderType = orderType;
                            tradingSignal.StrategyName = strategyName;
                            tradingSignal.RiskMode = settings.Mode;

                            string explanation = await iaService.ExplainSignalAsync(tradingSignal, technicals);
                            tradingSignal.IaExplanation = explanation;

                            dbContext.TradingSignals.Add(tradingSignal);
                            await dbContext.SaveChangesAsync(stoppingToken);

                            string targetChatId = string.IsNullOrEmpty(settings.TelegramChatId) 
                                ? _configuration["TradingSettings:DefaultTelegramChatId"] ?? string.Empty 
                                : settings.TelegramChatId;
                                
                            await notificationService.SendSignalAlertAsync(tradingSignal, targetChatId);

                            _lastProcessedCandleDates[symbol] = lastClosedCandle.Date;
                        }
                        else
                        {
                            _lastProcessedCandleDates[symbol] = lastClosedCandle.Date;
                        }
                    }
                    catch (Exception exSymbol)
                    {
                        _logger.LogError(exSymbol, "Error monitoreando el activo {Symbol}", symbol);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el ciclo principal del bot de trading.");
            }

            await Task.Delay(TimeSpan.FromSeconds(checkIntervalSeconds), stoppingToken);
        }
    }

    private async Task InitializeDatabaseAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        if (!await dbContext.AccountSettings.AnyAsync())
        {
            dbContext.AccountSettings.Add(new AccountSettings
            {
                Capital = 200.00m,
                MaxRiskPercentage = 1.0m,
                TelegramChatId = string.Empty,
                BotEnabled = true,
                Mode = RiskMode.Safe
            });
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task SyncCandlesToDbAsync(TradingDbContext dbContext, List<Candle> candles, CancellationToken cancellationToken)
    {
        try
        {
            var existingDates = await dbContext.Candles
                .Where(c => c.Symbol == candles[0].Symbol && c.Timeframe == candles[0].Timeframe)
                .Select(c => c.Date)
                .ToListAsync(cancellationToken);

            var newCandles = candles.Where(c => !existingDates.Contains(c.Date)).ToList();
            if (newCandles.Any())
            {
                dbContext.Candles.AddRange(newCandles);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al sincronizar velas en la base de datos.");
        }
    }
}
