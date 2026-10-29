using System.Collections.Generic;
using System.Threading.Tasks;
using TradingAssistant.Core.Models;

namespace TradingAssistant.Core.Interfaces;

public interface IMarketDataService
{
    Task<List<Candle>> GetHistoricalCandlesAsync(string symbol, string timeframe, int size = 250);
}
