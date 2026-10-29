namespace TradingAssistant.Core.Models;

public class AccountSettings
{
    public int Id { get; set; }
    public decimal Capital { get; set; } = 200.00m; // Balance de la cuenta
    public decimal MaxRiskPercentage { get; set; } = 1.0m; // 1% de riesgo por operación por defecto
    public string TelegramChatId { get; set; } = string.Empty;
    public bool BotEnabled { get; set; } = true;
    public RiskMode Mode { get; set; } = RiskMode.Safe;
}
