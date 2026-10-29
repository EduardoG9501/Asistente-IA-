using System;
using Skender.Stock.Indicators;

namespace TradingAssistant.Core.Models;

public class Candle : IQuote
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    
    // Propiedades de la interfaz IQuote
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
}
