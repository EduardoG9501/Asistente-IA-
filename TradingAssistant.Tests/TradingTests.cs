using System;
using System.Collections.Generic;
using Xunit;
using TradingAssistant.Core.Models;
using TradingAssistant.Core.Services;

namespace TradingAssistant.Tests;

public class TradingTests
{
    private readonly RiskManagementService _riskService;
    private readonly SignalService _signalService;

    public TradingTests()
    {
        _riskService = new RiskManagementService();
        _signalService = new SignalService();
    }

    [Fact]
    public void RiskManagementService_ShouldCalculateStopLossAndTakeProfitCorrectly_ForBuy()
    {
        // Arrange
        var technicals = new TechnicalIndicatorsResult
        {
            Symbol = "EUR/USD",
            Timeframe = "15m",
            CurrentPrice = 1.1680m,
            Atr = 0.0015m // 15 pips de volatilidad
        };
        var settings = new AccountSettings
        {
            Capital = 200.00m,
            MaxRiskPercentage = 1.0m // 1% = 2 USD
        };

        // Act
        var signal = _riskService.ApplyRiskManagement(technicals, SignalType.BUY, settings);

        // Assert
        // SL = 1.1680 - (2 * 0.0015) = 1.1650
        // TP1 = 1.1680 + (2 * 0.0015 * 1) = 1.1710
        // TP2 = 1.1680 + (2 * 0.0015 * 2) = 1.1740
        // TP3 = 1.1680 + (2 * 0.0015 * 3) = 1.1770
        Assert.Equal(1.1650m, signal.StopLoss);
        Assert.Equal(1.1710m, signal.TakeProfit1);
        Assert.Equal(1.1740m, signal.TakeProfit2);
        Assert.Equal(1.1770m, signal.TakeProfit3);
        Assert.Equal(2.00m, signal.RiskAmount);
    }

    [Fact]
    public void RiskManagementService_ShouldCalculateCorrectPositionSizeForForex()
    {
        // Arrange
        var technicals = new TechnicalIndicatorsResult
        {
            Symbol = "EUR/USD",
            Timeframe = "15m",
            CurrentPrice = 1.1680m,
            Atr = 0.0015m // 15 pips -> SL estará a 30 pips
        };
        var settings = new AccountSettings
        {
            Capital = 1000m, // Capital mayor para ver tamaño de posición real > mínimo
            MaxRiskPercentage = 2.0m // 2% riesgo = 20 USD
        };

        // Act
        var signal = _riskService.ApplyRiskManagement(technicals, SignalType.BUY, settings);

        // Assert
        // SL = 1.1650 (30 pips de distancia)
        // Lotes = RiesgoUSD / (Pips * 10) = 20 / (30 * 10) = 20 / 300 = 0.0667 -> Redondeado a 0.07 lotes
        Assert.Equal(0.07m, signal.PositionSize);
    }

    [Fact]
    public void SignalService_ShouldReturnNoTrade_WhenIndicatorsAreNeutral()
    {
        // Arrange
        var technicals = new TechnicalIndicatorsResult
        {
            Symbol = "EUR/USD-TEST",
            CurrentPrice = 1.1680m,
            Ema20 = 1.1670m,
            Ema50 = 1.1650m,
            Ema200 = 1.1600m, // Ema50 > Ema200 -> Tendencia Alcista
            Rsi = 55m, // RSI neutral, no hay sobreventa en retroceso (requiere 30-48) y no hay momentum
            Atr = 0.0015m
        };
        var candles = GetBaseMockCandles(1.1680m);

        // Act
        var signal = _signalService.DetermineSignal(technicals, candles, out string justification, out _, out _);

        // Assert
        Assert.Equal(SignalType.NO_TRADE, signal);
        Assert.Contains("NO OPERAR", justification);
    }

    [Fact]
    public void SignalService_ShouldReturnBuy_WhenInAggressiveModeForImpulse()
    {
        // Arrange: Mismos indicadores neutrales del modo seguro
        var technicals = new TechnicalIndicatorsResult
        {
            Symbol = "EUR/USD-TEST",
            CurrentPrice = 1.1680m,
            Ema20 = 1.1670m,
            Ema50 = 1.1650m,
            Ema200 = 1.1600m,
            Rsi = 55m, // RSI 55 activa impulso en modo agresivo
            Atr = 0.0015m
        };
        var candles = GetBaseMockCandles(1.1680m);

        // Act: Ejecutar en Modo Agresivo
        var signal = _signalService.DetermineSignal(technicals, candles, out string justification, out var orderType, out var strategyName, RiskMode.Aggressive);

        // Assert
        Assert.Equal(SignalType.BUY, signal);
        Assert.Equal(OrderType.BuyMarket, orderType);
        Assert.Contains("AGRESIVO", strategyName);
    }

    [Fact]
    public void SignalService_ShouldReturnBuy_WhenConditionsAreAlignedForPullback()
    {
        // Arrange
        var technicals = new TechnicalIndicatorsResult
        {
            Symbol = "EUR/USD-TEST",
            CurrentPrice = 1.1660m, // Cierre por debajo de EMA 20 pero encima de EMA 50 (retroceso a soporte)
            Ema20 = 1.1670m,
            Ema50 = 1.1650m,
            Ema200 = 1.1600m, // EMA 50 > EMA 200 -> Alcista
            Rsi = 40m, // RSI sobrevendido relativo (en retroceso)
            Atr = 0.0015m
        };
        var candles = GetBaseMockCandles(1.1660m);

        // Act
        var signal = _signalService.DetermineSignal(technicals, candles, out string justification, out _, out _);

        // Assert
        Assert.Equal(SignalType.BUY, signal);
        Assert.Contains("COMPRA EN RETROCESO", justification);
    }

    [Fact]
    public void SignalService_ShouldReturnBuy_WhenConditionsAreAlignedForMomentumBreakout()
    {
        // Arrange
        var technicals = new TechnicalIndicatorsResult
        {
            Symbol = "EUR/USD-TEST",
            CurrentPrice = 1.1680m, // Precio alto (rompiendo resistencia)
            Ema20 = 1.1650m,
            Ema50 = 1.1620m, // Tendencia alcista fuerte: Ema20 > Ema50
            Ema200 = 1.1550m, // Ema50 > Ema200
            Rsi = 65m, // RSI con momentum elevado (55-75)
            Atr = 0.0015m
        };
        
        // Crear 6 velas donde el máximo cierre de las últimas 5 es 1.1640 y la actual es 1.1680 (breakout)
        var candles = new List<Candle>();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            candles.Add(new Candle { Date = now.AddMinutes(-15 * (6 - i)), Close = 1.1640m });
        }
        candles.Add(new Candle { Date = now, Close = 1.1680m }); // Vela actual (CurrentPrice)

        // Act
        var signal = _signalService.DetermineSignal(technicals, candles, out string justification, out _, out _);

        // Assert
        Assert.Equal(SignalType.BUY, signal);
        Assert.Contains("COMPRA POR RUPTURA", justification);
    }

    private List<Candle> GetBaseMockCandles(decimal currentPrice)
    {
        var list = new List<Candle>();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            list.Add(new Candle { Date = now.AddMinutes(-15 * (6 - i)), Close = currentPrice });
        }
        list.Add(new Candle { Date = now, Close = currentPrice });
        return list;
    }
}
