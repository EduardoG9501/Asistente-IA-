using System.Collections.Generic;
using TradingAssistant.Core.Models;

namespace TradingAssistant.Core.Interfaces;

public interface IIndicatorService
{
    TechnicalIndicatorsResult CalculateIndicators(List<Candle> candles);
}
