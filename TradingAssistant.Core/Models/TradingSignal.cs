using System;

namespace TradingAssistant.Core.Models;

public class TradingSignal
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public SignalType Type { get; set; }
    public OrderType OrderType { get; set; }
    public RiskMode RiskMode { get; set; } = RiskMode.Safe;
    public string StrategyName { get; set; } = string.Empty; // "RETROCESO" o "CONTINUACIÓN"
    private DateTime _createdAt;
    public DateTime CreatedAt
    {
        get => _createdAt;
        set => _createdAt = DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private DateTime _entryWindowStart;
    public DateTime EntryWindowStart
    {
        get => _entryWindowStart;
        set => _entryWindowStart = DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private DateTime _entryWindowEnd;
    public DateTime EntryWindowEnd
    {
        get => _entryWindowEnd;
        set => _entryWindowEnd = DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
    
    public decimal EntryPrice { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit1 { get; set; }
    public decimal TakeProfit2 { get; set; }
    public decimal TakeProfit3 { get; set; }
    public decimal RiskRewardRatio { get; set; }
    public double ConfidenceScore { get; set; }
    
    // Gestión de Riesgo
    public decimal PositionSize { get; set; } // En lotes o unidades
    public decimal RiskAmount { get; set; } // Monto arriesgado en USD
    
    // Análisis Técnico
    public string TechnicalJustification { get; set; } = string.Empty;
    
    // Explicación de la IA
    public string IaExplanation { get; set; } = string.Empty;
}
