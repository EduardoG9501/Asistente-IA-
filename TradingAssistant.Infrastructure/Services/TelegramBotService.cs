using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TradingAssistant.Core.Interfaces;
using TradingAssistant.Core.Models;

namespace TradingAssistant.Infrastructure.Services;

public class TelegramBotService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly string _botToken;

    public TelegramBotService(HttpClient httpClient, IConfiguration configuration, ILogger<TelegramBotService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _botToken = configuration["TradingSettings:TelegramBotToken"] ?? string.Empty;
    }

    public async Task SendSignalAlertAsync(TradingSignal signal, string chatId)
    {
        if (string.IsNullOrEmpty(_botToken) || _botToken == "TU_TELEGRAM_BOT_TOKEN" ||
            string.IsNullOrEmpty(chatId) || chatId == "TU_TELEGRAM_CHAT_ID")
        {
            _logger.LogWarning("Telegram Bot no configurado o ChatId inválido. Imprimiendo alerta en consola:");
            _logger.LogInformation("ALERTA MOCK: \n{Message}", BuildHtmlMessage(signal));
            return;
        }

        try
        {
            var message = BuildHtmlMessage(signal);
            _logger.LogInformation("Enviando señal a Telegram mediante API HTTP...");

            // Estructurar el cuerpo de la petición POST para api.telegram.org
            var payload = new
            {
                chat_id = chatId,
                text = message,
                parse_mode = "HTML"
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";

            var response = await _httpClient.PostAsync(url, jsonContent);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("La API de Telegram devolvió error: {StatusCode} - {Error}", response.StatusCode, errorContent);
            }
            else
            {
                _logger.LogInformation("Señal enviada correctamente a Telegram.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar mensaje por Telegram.");
        }
    }

    private string BuildHtmlMessage(TradingSignal signal)
    {
        string icon = signal.Type == SignalType.BUY ? "🔵 <b>COMPRA (BUY)</b>" : "🔴 <b>VENTA (SELL)</b>";
        
        // Calcular la distancia al SL en pips
        decimal pipMultiplier = signal.Symbol.Contains("JPY", StringComparison.OrdinalIgnoreCase) ? 100m : 10000m;
        decimal slPips = Math.Round(Math.Abs(signal.EntryPrice - signal.StopLoss) * pipMultiplier, 1);
        decimal tp1Pips = Math.Round(Math.Abs(signal.EntryPrice - signal.TakeProfit1) * pipMultiplier, 1);
        decimal tp2Pips = Math.Round(Math.Abs(signal.EntryPrice - signal.TakeProfit2) * pipMultiplier, 1);
        decimal tp3Pips = Math.Round(Math.Abs(signal.EntryPrice - signal.TakeProfit3) * pipMultiplier, 1);

        return $@"
{icon}
<b>Activo:</b> {signal.Symbol}
📲 <b>Buscar en MT5:</b> <code>{GetMt5SymbolSuggestion(signal.Symbol)}</code>
<b>Temporalidad:</b> {signal.Timeframe}

⏰ <b>HORARIOS Y VENTANA DE ENTRADA:</b>
• <b>Hora de entrada aproximada:</b> {signal.EntryWindowStart.ToLocalTime():HH:mm:ss} (Local) / {signal.EntryWindowStart:HH:mm:ss} (UTC)
• <b>Vencimiento sugerido (Límite):</b> {signal.EntryWindowEnd.ToLocalTime():HH:mm:ss} (Local) / {signal.EntryWindowEnd:HH:mm:ss} (UTC)

📊 <b>PARÁMETROS:</b>
• <b>Tipo de Orden MT5:</b> <code>{signal.OrderType}</code> → <b>{GetOrderTypeDescription(signal.OrderType)}</b>
• <b>Estrategia:</b> {signal.StrategyName}
• <b>Entrada:</b> {signal.EntryPrice:F5}
• 🛡️ <b>Stop Loss (SL):</b> {signal.StopLoss:F5} (<i>{slPips} pips</i>)
• 🎯 <b>Take Profit 1 (TP1):</b> {signal.TakeProfit1:F5} (<i>{tp1Pips} pips</i>)
• 🎯 <b>Take Profit 2 (TP2):</b> {signal.TakeProfit2:F5} (<i>{tp2Pips} pips</i>)
• 🎯 <b>Take Profit 3 (TP3):</b> {signal.TakeProfit3:F5} (<i>{tp3Pips} pips</i>)

💰 <b>GESTIÓN DE RIESGO:</b>
• <b>Riesgo Máximo:</b> {signal.RiskAmount:C} USD
• <b>Tamaño Posición:</b> <code>{signal.PositionSize}</code> lotes/unidades

🔍 <b>ANÁLISIS TÉCNICO:</b>
{signal.TechnicalJustification}

🤖 <b>EXPLICACIÓN DE IA:</b>
{signal.IaExplanation}
";
    }

    private static string GetOrderTypeDescription(OrderType orderType) => orderType switch
    {
        OrderType.BuyMarket  => "⚡ Compra por Mercado (Market Buy) — Presionar COMPRAR inmediatamente",
        OrderType.SellMarket => "⚡ Venta por Mercado (Market Sell) — Presionar VENDER inmediatamente",
        OrderType.BuyLimit   => "⏳ Buy Limit (Orden Pendiente) — ⬇️ Colocar DEBAJO del precio actual (Comprar si baja)",
        OrderType.SellLimit  => "⏳ Sell Limit (Orden Pendiente) — ⬆️ Colocar ENCIMA del precio actual (Vender si sube)",
        OrderType.BuyStop    => "⏳ Buy Stop (Orden Pendiente) — ⬆️ Colocar ENCIMA del precio actual (Comprar si rompe hacia arriba)",
        OrderType.SellStop   => "⏳ Sell Stop (Orden Pendiente) — ⬇️ Colocar DEBAJO del precio actual (Vender si rompe hacia abajo)",
        _ => "Orden de Mercado"
    };

    private static string GetMt5SymbolSuggestion(string symbol) => symbol.ToUpperInvariant() switch
    {
        "EUR/USD" => "EURUSD (o EURUSD.m / EURUSD.ecn)",
        "GBP/USD" => "GBPUSD (o GBPUSD.m / GBPUSD.ecn)",
        "USD/JPY" => "USDJPY (o USDJPY.m)",
        "AUD/USD" => "AUDUSD",
        "USD/CAD" => "USDCAD",
        "XAU/USD" => "XAUUSD o GOLD",
        "XAG/USD" => "XAGUSD o SILVER",
        "BTC/USD" => "BTCUSD o BTCUSDT",
        "ETH/USD" => "ETHUSD o ETHUSDT",
        "SOL/USD" => "SOLUSD o SOLUSDT",
        "XRP/USD" => "XRPUSD o XRPUSDT",
        "ADA/USD" => "ADAUSD o ADAUSDT",
        "AAPL"    => "AAPL o #AAPL",
        "TSLA"    => "TSLA o #TSLA",
        "NVDA"    => "NVDA o #NVDA",
        "MSFT"    => "MSFT o #MSFT",
        "AMZN"    => "AMZN o #AMZN",
        _ => symbol.Replace("/", "")
    };
}
