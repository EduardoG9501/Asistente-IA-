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

public class GeminiClient : IIaService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiClient> _logger;

    public GeminiClient(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["TradingSettings:GeminiApiKey"] ?? string.Empty;
    }

    public async Task<string> ExplainSignalAsync(TradingSignal signal, TechnicalIndicatorsResult technicals)
    {
        if (string.IsNullOrEmpty(_apiKey) || _apiKey == "TU_GEMINI_API_KEY")
        {
            _logger.LogWarning("Gemini API Key no configurada. Retornando explicación básica predeterminada.");
            return GetDefaultExplanation(signal, technicals);
        }

        try
        {
            var prompt = BuildPrompt(signal, technicals);
            var requestBody = new GeminiRequest
            {
                Contents = new[]
                {
                    new GeminiContent
                    {
                        Parts = new[]
                        {
                            new GeminiPart { Text = prompt }
                        }
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";

            var response = await _httpClient.PostAsync(url, new StringContent(jsonContent, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GeminiResponse>(jsonString);

            var text = result?.Candidates?[0]?.Content?.Parts?[0]?.Text;
            if (string.IsNullOrEmpty(text))
            {
                _logger.LogError("La API de Gemini retornó un resultado vacío. Respuesta: {Json}", jsonString);
                return GetDefaultExplanation(signal, technicals);
            }

            return text.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al invocar la API de Gemini. Retornando explicación de contingencia.");
            return GetDefaultExplanation(signal, technicals);
        }
    }

    private string BuildPrompt(TradingSignal signal, TechnicalIndicatorsResult technicals)
    {
        return $@"Actúa como un Analista de Riesgos y Trading Profesional.
Se ha generado una señal de trading automática basada en reglas técnicas deterministas. Tu objetivo es explicar brevemente la lógica detrás de esta señal y los posibles riesgos a tener en cuenta. Sé claro, directo y no garantices ganancias.

=== DETALLES DE LA OPERACIÓN ===
Activo: {signal.Symbol}
Temporalidad: {signal.Timeframe}
Tipo de Señal: {signal.Type}
Precio de Entrada: {signal.EntryPrice:F5}
Stop Loss: {signal.StopLoss:F5}
Take Profit 1 (TP1): {signal.TakeProfit1:F5}
Take Profit 2 (TP2): {signal.TakeProfit2:F5}
Take Profit 3 (TP3): {signal.TakeProfit3:F5}
Relación Riesgo/Beneficio: {signal.RiskRewardRatio}:1
Riesgo Máximo de la Operación: {signal.RiskAmount:C} USD
Tamaño de la Posición Calculada: {signal.PositionSize} lotes

=== INDICADORES TÉCNICOS ===
Precio de Cierre Actual: {technicals.CurrentPrice:F5}
EMA 20 (Corto plazo): {technicals.Ema20:F5}
EMA 50 (Medio plazo): {technicals.Ema50:F5}
EMA 200 (Tendencia mayor): {technicals.Ema200:F5}
RSI (14): {technicals.Rsi:F2}
ATR (14 - Volatilidad): {technicals.Atr:F5}

=== INSTRUCCIONES DE RESPUESTA ===
1. Explica brevemente por qué la estructura de medias móviles (EMA 20/50/200) y el RSI justifican esta decisión (máximo 2-3 párrafos cortos).
2. Explica la gestión de riesgo aplicada (distancia del Stop Loss basada en ATR y tamaño de posición en lotes para proteger el capital de $200).
3. Añade una advertencia corta sobre el riesgo en mercados volátiles.
4. Responde en español.";
    }

    private string GetDefaultExplanation(TradingSignal signal, TechnicalIndicatorsResult technicals)
    {
        var direction = signal.Type == SignalType.BUY ? "Compra (Long)" : "Venta (Short)";
        return $"[Explicación Determinista de Contingencia]: Se ha detectado una señal de {direction} en {signal.Symbol} ({signal.Timeframe}). " +
               $"La tendencia estructural está alineada (EMA 50 {(technicals.Ema50GreaterThanEma200 ? ">" : "<")} EMA 200) y el precio se encuentra en una zona de retroceso técnico. " +
               $"El RSI actual es de {technicals.Rsi:F2}, justificando la entrada con un Stop Loss dinámico de {signal.StopLoss:F5} (2x ATR) y un tamaño de posición calculado de {signal.PositionSize} lotes para limitar la pérdida a {signal.RiskAmount:C} USD.";
    }
}

// Clases auxiliares para la serialización de Gemini API
public class GeminiRequest
{
    [JsonPropertyName("contents")]
    public GeminiContent[] Contents { get; set; } = Array.Empty<GeminiContent>();
}

public class GeminiContent
{
    [JsonPropertyName("parts")]
    public GeminiPart[] Parts { get; set; } = Array.Empty<GeminiPart>();
}

public class GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public GeminiCandidate[]? Candidates { get; set; }
}

public class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiResponseContent? Content { get; set; }
}

public class GeminiResponseContent
{
    [JsonPropertyName("parts")]
    public GeminiResponsePart[]? Parts { get; set; }
}

public class GeminiResponsePart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
