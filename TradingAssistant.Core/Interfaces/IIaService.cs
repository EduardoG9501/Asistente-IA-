using System.Threading.Tasks;
using TradingAssistant.Core.Models;

namespace TradingAssistant.Core.Interfaces;

public interface IIaService
{
    Task<string> ExplainSignalAsync(TradingSignal signal, TechnicalIndicatorsResult technicals);
}
