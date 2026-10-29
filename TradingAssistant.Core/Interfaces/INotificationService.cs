using System.Threading.Tasks;
using TradingAssistant.Core.Models;

namespace TradingAssistant.Core.Interfaces;

public interface INotificationService
{
    Task SendSignalAlertAsync(TradingSignal signal, string chatId);
}
