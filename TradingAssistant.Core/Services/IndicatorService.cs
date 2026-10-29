using System;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using TradingAssistant.Core.Interfaces;
using TradingAssistant.Core.Models;

namespace TradingAssistant.Core.Services;

public class IndicatorService : IIndicatorService
{
    public TechnicalIndicatorsResult CalculateIndicators(List<Candle> candles)
    {
        if (candles == null || candles.Count < 200)
        {
            throw new ArgumentException("Se necesitan al menos 200 velas para calcular los indicadores correctamente (especialmente EMA 200).");
        }

        // Ordenamos cronológicamente ascendente (antiguas primero) que es lo que requiere Skender.Stock.Indicators
        var sortedQuotes = candles.OrderBy(c => c.Date).ToList();

        // Calcular EMAs
        var ema20Results = sortedQuotes.GetEma(20).ToList();
        var ema50Results = sortedQuotes.GetEma(50).ToList();
        var ema200Results = sortedQuotes.GetEma(200).ToList();

        // Calcular RSI
        var rsiResults = sortedQuotes.GetRsi(14).ToList();

        // Calcular ATR
        var atrResults = sortedQuotes.GetAtr(14).ToList();

        // Obtener los últimos resultados válidos (los de la última vela cerrada)
        var lastQuote = sortedQuotes.Last();
        var lastEma20 = ema20Results.Last().Ema;
        var lastEma50 = ema50Results.Last().Ema;
        var lastEma200 = ema200Results.Last().Ema;
        var lastRsi = rsiResults.Last().Rsi;
        var lastAtr = atrResults.Last().Atr;

        return new TechnicalIndicatorsResult
        {
            Symbol = lastQuote.Symbol,
            Timeframe = lastQuote.Timeframe,
            CurrentPrice = lastQuote.Close,
            Ema20 = (decimal)(lastEma20 ?? 0),
            Ema50 = (decimal)(lastEma50 ?? 0),
            Ema200 = (decimal)(lastEma200 ?? 0),
            Rsi = (decimal)(lastRsi ?? 0),
            Atr = (decimal)(lastAtr ?? 0)
        };
    }
}
