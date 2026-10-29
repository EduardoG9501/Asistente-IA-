namespace TradingAssistant.Core.Models;

/// <summary>
/// Tipo de orden sugerida para ejecutar en MetaTrader 5.
/// </summary>
public enum OrderType
{
    /// <summary>Compra inmediata al precio actual del mercado (señal de momentum)</summary>
    BuyMarket = 0,
    
    /// <summary>Venta inmediata al precio actual del mercado (señal de momentum)</summary>
    SellMarket = 1,
    
    /// <summary>Orden pendiente: compra cuando el precio baje al nivel de entrada (señal de retroceso)</summary>
    BuyLimit = 2,
    
    /// <summary>Orden pendiente: venta cuando el precio suba al nivel de entrada (señal de retroceso)</summary>
    SellLimit = 3,

    /// <summary>Orden pendiente: compra cuando el precio suba y rompa la resistencia (señal de ruptura)</summary>
    BuyStop = 4,

    /// <summary>Orden pendiente: venta cuando el precio baje y rompa el soporte (señal de ruptura)</summary>
    SellStop = 5
}
