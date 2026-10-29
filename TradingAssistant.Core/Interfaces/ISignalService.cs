using System.Collections.Generic;
using TradingAssistant.Core.Models;

namespace TradingAssistant.Core.Interfaces;

public interface ISignalService
{
    SignalType DetermineSignal(TechnicalIndicatorsResult technicals, List<Candle> candles, out string technicalJustification, out OrderType orderType, out string strategyName, RiskMode riskMode = RiskMode.Safe);
}
