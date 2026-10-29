using System;
using System.Linq;
using TradingAssistant.Core.Interfaces;
using TradingAssistant.Core.Models;

namespace TradingAssistant.Core.Services;

public class RiskManagementService : IRiskManagementService
{
    private const decimal AtrMultiplier = 2.0m;
    private const decimal RewardRiskRatio = 2.0m; // 1:2 R/R

    public TradingSignal ApplyRiskManagement(TechnicalIndicatorsResult technicals, SignalType direction, AccountSettings settings)
    {
        var now = DateTime.UtcNow;
        var signal = new TradingSignal
        {
            Symbol = technicals.Symbol,
            Timeframe = technicals.Timeframe,
            Type = direction,
            CreatedAt = now,
            EntryPrice = technicals.CurrentPrice
        };

        if (direction != SignalType.NO_TRADE)
        {
            signal.EntryWindowStart = now;
            signal.EntryWindowEnd = now.Add(ParseTimeframe(technicals.Timeframe));
        }

        if (direction == SignalType.NO_TRADE)
        {
            signal.StopLoss = 0;
            signal.TakeProfit1 = 0;
            signal.TakeProfit2 = 0;
            signal.TakeProfit3 = 0;
            signal.RiskRewardRatio = 0;
            signal.PositionSize = 0;
            signal.RiskAmount = 0;
            return signal;
        }

        // 1. Calcular Stop Loss y Take Profits (TP1, TP2, TP3) basados en el ATR
        decimal atr = technicals.Atr;
        if (atr <= 0)
        {
            // Fallback en caso de que ATR sea 0 (muy improbable pero posible en mocks)
            atr = technicals.CurrentPrice * 0.0015m; 
        }

        decimal slDistance = atr * AtrMultiplier;

        if (direction == SignalType.BUY)
        {
            signal.StopLoss = Math.Round(signal.EntryPrice - slDistance, 5);
            signal.TakeProfit1 = Math.Round(signal.EntryPrice + slDistance, 5);       // R/R 1:1
            signal.TakeProfit2 = Math.Round(signal.EntryPrice + (slDistance * 2m), 5); // R/R 1:2
            signal.TakeProfit3 = Math.Round(signal.EntryPrice + (slDistance * 3m), 5); // R/R 1:3
        }
        else // SELL
        {
            signal.StopLoss = Math.Round(signal.EntryPrice + slDistance, 5);
            signal.TakeProfit1 = Math.Round(signal.EntryPrice - slDistance, 5);       // R/R 1:1
            signal.TakeProfit2 = Math.Round(signal.EntryPrice - (slDistance * 2m), 5); // R/R 1:2
            signal.TakeProfit3 = Math.Round(signal.EntryPrice - (slDistance * 3m), 5); // R/R 1:3
        }

        signal.RiskRewardRatio = RewardRiskRatio;

        // 2. Calcular Gestión de Riesgo y Tamaño de Posición
        decimal riskAmount = settings.Capital * (settings.MaxRiskPercentage / 100m);
        signal.RiskAmount = Math.Round(riskAmount, 2);

        // Distancia de SL en pips
        // Para pares comunes de Forex sin JPY, 1 pip = 0.0001 (multiplicamos por 10000)
        // Para pares con JPY, 1 pip = 0.01 (multiplicamos por 100)
        decimal pipMultiplier = 10000m;
        if (technicals.Symbol.Contains("JPY", StringComparison.OrdinalIgnoreCase))
        {
            pipMultiplier = 100m;
        }

        decimal slPips = Math.Abs(signal.EntryPrice - signal.StopLoss) * pipMultiplier;

        if (slPips > 0)
        {
            bool isStock = !technicals.Symbol.Contains("/");
            if (isStock)
            {
                // Acciones (AAPL, TSLA, NVDA, etc.): Posición en número de acciones = RiesgoUSD / DistanciaSL
                decimal priceDistance = Math.Abs(signal.EntryPrice - signal.StopLoss);
                signal.PositionSize = priceDistance > 0 ? Math.Round(riskAmount / priceDistance, 2) : 1m;
            }
            else
            {
                // Forex estándar (EUR/USD, GBP/USD, etc.)
                decimal pipValueStandardLot = 10m; 
                decimal positionSizeLots = riskAmount / (slPips * pipValueStandardLot);
                signal.PositionSize = Math.Max(0.01m, Math.Round(positionSizeLots, 2));
            }
        }
        else
        {
            signal.PositionSize = 0.01m;
        }

        return signal;
    }

    private TimeSpan ParseTimeframe(string timeframe)
    {
        if (string.IsNullOrEmpty(timeframe))
        {
            return TimeSpan.FromMinutes(15);
        }

        string clean = timeframe.Trim().ToLower();
        string digits = new string(clean.Where(char.IsDigit).ToArray());
        if (!int.TryParse(digits, out int value))
        {
            value = 15;
        }

        if (clean.Contains("h"))
        {
            return TimeSpan.FromHours(value);
        }
        if (clean.Contains("d"))
        {
            return TimeSpan.FromDays(value);
        }
        return TimeSpan.FromMinutes(value);
    }
}
