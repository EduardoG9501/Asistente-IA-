using TradingAssistant.Core.Models;

namespace TradingAssistant.Core.Interfaces;

public interface IRiskManagementService
{
    TradingSignal ApplyRiskManagement(TechnicalIndicatorsResult technicals, SignalType direction, AccountSettings settings);
}
