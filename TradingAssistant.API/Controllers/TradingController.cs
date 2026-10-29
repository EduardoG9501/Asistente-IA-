using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Core.Interfaces;
using TradingAssistant.Core.Models;
using TradingAssistant.Core.Services;
using TradingAssistant.Infrastructure.Data;

namespace TradingAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TradingController : ControllerBase
{
    private readonly TradingDbContext _dbContext;
    private readonly IMarketDataService _marketDataService;
    private readonly IIndicatorService _indicatorService;
    private readonly ISignalService _signalService;
    private readonly IRiskManagementService _riskService;
    private readonly IIaService _iaService;
    private readonly INotificationService _notificationService;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

    public TradingController(
        TradingDbContext dbContext,
        IMarketDataService marketDataService,
        IIndicatorService indicatorService,
        ISignalService signalService,
        IRiskManagementService riskService,
        IIaService iaService,
        INotificationService notificationService,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _dbContext = dbContext;
        _marketDataService = marketDataService;
        _indicatorService = indicatorService;
        _signalService = signalService;
        _riskService = riskService;
        _iaService = iaService;
        _notificationService = notificationService;
        _configuration = configuration;
    }

    // 1. Obtener estado actual y configuraciones
    [HttpGet("settings")]
    public async Task<ActionResult<AccountSettings>> GetSettings()
    {
        var settings = await _dbContext.AccountSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            return NotFound("No se encontraron configuraciones de cuenta.");
        }
        return Ok(settings);
    }

    // 2. Actualizar configuraciones (Capital, riesgo, etc.)
    [HttpPost("settings")]
    public async Task<ActionResult<AccountSettings>> UpdateSettings([FromBody] AccountSettings updatedSettings)
    {
        var settings = await _dbContext.AccountSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new AccountSettings();
            _dbContext.AccountSettings.Add(settings);
        }

        settings.Capital = updatedSettings.Capital;
        settings.MaxRiskPercentage = updatedSettings.MaxRiskPercentage;
        settings.TelegramChatId = updatedSettings.TelegramChatId;
        settings.BotEnabled = updatedSettings.BotEnabled;
        settings.Mode = updatedSettings.Mode;

        await _dbContext.SaveChangesAsync();
        return Ok(settings);
    }

    // 3. Obtener el historial de señales generadas
    [HttpGet("signals")]
    public async Task<ActionResult<IEnumerable<TradingSignal>>> GetSignals([FromQuery] int limit = 20)
    {
        var signals = await _dbContext.TradingSignals
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync();
        return Ok(signals);
    }

    // 4. Forzar un análisis manual inmediato y opcionalmente enviar alerta
    [HttpPost("analyze")]
    public async Task<IActionResult> RunAnalysis([FromQuery] string symbol = "EUR/USD", [FromQuery] string timeframe = "15m", [FromQuery] bool sendAlert = false, [FromQuery] RiskMode? mode = null)
    {
        try
        {
            var settings = await _dbContext.AccountSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new AccountSettings();
                _dbContext.AccountSettings.Add(settings);
                await _dbContext.SaveChangesAsync();
            }

            var activeMode = mode ?? settings.Mode;

            // Obtener velas
            var candles = await _marketDataService.GetHistoricalCandlesAsync(symbol, timeframe, 200);
            if (candles == null || candles.Count < 200)
            {
                return BadRequest("No hay suficientes velas históricas para correr el análisis.");
            }

            // Calcular indicadores
            var technicals = _indicatorService.CalculateIndicators(candles);

            // Determinar señal según el modo configurado (Seguro o Agresivo)
            var signalType = _signalService.DetermineSignal(technicals, candles, out string justification, out var orderType, out var strategyName, activeMode);

            // Generar estructura de señal
            var tradingSignal = _riskService.ApplyRiskManagement(technicals, signalType, settings);
            tradingSignal.TechnicalJustification = justification;
            tradingSignal.OrderType = orderType;
            tradingSignal.StrategyName = strategyName;
            tradingSignal.RiskMode = activeMode;

            if (signalType != SignalType.NO_TRADE)
            {
                // Enriquecer con IA
                string explanation = await _iaService.ExplainSignalAsync(tradingSignal, technicals);
                tradingSignal.IaExplanation = explanation;

                // Guardar si se decide
                _dbContext.TradingSignals.Add(tradingSignal);
                await _dbContext.SaveChangesAsync();

                if (sendAlert)
                {
                    string targetChatId = string.IsNullOrEmpty(settings.TelegramChatId) 
                        ? _configuration["TradingSettings:DefaultTelegramChatId"] ?? string.Empty 
                        : settings.TelegramChatId;
                    await _notificationService.SendSignalAlertAsync(tradingSignal, targetChatId);
                }
            }

            return Ok(new
            {
                Analisis = technicals,
                SenalDeterminada = signalType.ToString(),
                JustificacionTecnica = justification,
                DetallesSenal = signalType != SignalType.NO_TRADE ? tradingSignal : null
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error interno al ejecutar análisis: {ex.Message}");
        }
    }

    [HttpPost("mock-signal")]
    public async Task<IActionResult> CreateMockSignal([FromQuery] string symbol = "EUR/USD", [FromQuery] string direction = "BUY")
    {
        try
        {
            var settings = await _dbContext.AccountSettings.FirstOrDefaultAsync() ?? new AccountSettings();
            var signalType = direction.ToUpper() == "SELL" ? SignalType.SELL : SignalType.BUY;
            
            var now = DateTime.UtcNow;
            var mockSignal = new TradingSignal
            {
                Symbol = symbol,
                Timeframe = "15m",
                Type = signalType,
                CreatedAt = now,
                EntryWindowStart = now,
                EntryWindowEnd = now.AddMinutes(15),
                EntryPrice = symbol.Contains("GBP", StringComparison.OrdinalIgnoreCase) ? 1.3120m : 1.1680m,
                StopLoss = signalType == SignalType.BUY 
                    ? (symbol.Contains("GBP", StringComparison.OrdinalIgnoreCase) ? 1.3090m : 1.1650m) 
                    : (symbol.Contains("GBP", StringComparison.OrdinalIgnoreCase) ? 1.3150m : 1.1710m),
                TakeProfit1 = signalType == SignalType.BUY 
                    ? (symbol.Contains("GBP", StringComparison.OrdinalIgnoreCase) ? 1.3150m : 1.1710m) 
                    : (symbol.Contains("GBP", StringComparison.OrdinalIgnoreCase) ? 1.3090m : 1.1650m),
                TakeProfit2 = signalType == SignalType.BUY 
                    ? (symbol.Contains("GBP", StringComparison.OrdinalIgnoreCase) ? 1.3180m : 1.1740m) 
                    : (symbol.Contains("GBP", StringComparison.OrdinalIgnoreCase) ? 1.3060m : 1.1620m),
                TakeProfit3 = signalType == SignalType.BUY 
                    ? (symbol.Contains("GBP", StringComparison.OrdinalIgnoreCase) ? 1.3210m : 1.1770m) 
                    : (symbol.Contains("GBP", StringComparison.OrdinalIgnoreCase) ? 1.3030m : 1.1590m),
                RiskRewardRatio = 2.0m,
                ConfidenceScore = 74,
                PositionSize = 0.01m,
                RiskAmount = Math.Round(settings.Capital * (settings.MaxRiskPercentage / 100m), 2),
                OrderType = signalType == SignalType.BUY ? OrderType.BuyLimit : OrderType.SellLimit,
                StrategyName = settings.Mode == RiskMode.Aggressive ? "RETROCESO (AGRESIVO)" : "RETROCESO (SEGURO)",
                RiskMode = settings.Mode,
                TechnicalJustification = $"SIMULACIÓN: Cruce de EMA 20 y soporte dinámico en EMA 50. RSI actual en {(signalType == SignalType.BUY ? 42 : 58)}.",
                IaExplanation = "Esta es una señal de prueba simulada para verificar la apariencia de las tarjetas en el Dashboard. En una señal real del mercado, aquí aparecería la explicación técnica y de gestión redactada por la IA de Gemini."
            };

            _dbContext.TradingSignals.Add(mockSignal);
            await _dbContext.SaveChangesAsync();

            return Ok(mockSignal);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error al crear señal simulada: {ex.Message}");
        }
    }

    [HttpDelete("signals/clear")]
    public async Task<IActionResult> ClearSignals()
    {
        try
        {
            _dbContext.TradingSignals.RemoveRange(_dbContext.TradingSignals);
            await _dbContext.SaveChangesAsync();
            return Ok("Historial de señales limpiado correctamente.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error al limpiar el historial: {ex.Message}");
        }
    }

    // 6. Consultar estado de los mercados (Abierto vs Cerrado)
    [HttpGet("market-status")]
    public IActionResult GetMarketStatus()
    {
        var symbols = new[]
        {
            "EUR/USD", "GBP/USD", "USD/JPY", "AUD/USD", "USD/CAD",
            "AAPL", "TSLA", "NVDA", "MSFT", "AMZN",
            "XAU/USD", "XAG/USD",
            "BTC/USD", "ETH/USD", "SOL/USD", "XRP/USD", "ADA/USD"
        };

        var result = symbols.Select(s => new
        {
            symbol = s,
            isOpen = MarketScheduleHelper.IsMarketOpen(s, out string details),
            details = details
        });

        return Ok(result);
    }
}
