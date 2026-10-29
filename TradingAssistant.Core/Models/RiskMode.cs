namespace TradingAssistant.Core.Models;

/// <summary>
/// Modo de estrategia de negociación.
/// </summary>
public enum RiskMode
{
    /// <summary>Modo Seguro: Reglas estrictas, menos señales pero mayor precisión</summary>
    Safe = 0,

    /// <summary>Modo Agresivo: Reglas flexibles/amplias, más frecuencia de señales con mayor riesgo</summary>
    Aggressive = 1
}
