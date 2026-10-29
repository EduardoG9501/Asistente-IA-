namespace TradingAssistant.Core.Models;

public class TechnicalIndicatorsResult
{
    public string Symbol { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal Ema20 { get; set; }
    public decimal Ema50 { get; set; }
    public decimal Ema200 { get; set; }
    public decimal Rsi { get; set; }
    public decimal Atr { get; set; }
    
    // Tendencias estructuradas
    public bool Ema50GreaterThanEma200 => Ema50 > Ema200;
    public bool IsPriceAboveEma200 => CurrentPrice > Ema200;
}
