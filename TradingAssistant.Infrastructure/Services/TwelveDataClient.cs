using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TradingAssistant.Core.Interfaces;
using TradingAssistant.Core.Models;

namespace TradingAssistant.Infrastructure.Services;

public class TwelveDataClient : IMarketDataService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<TwelveDataClient> _logger;

    public TwelveDataClient(HttpClient httpClient, IConfiguration configuration, ILogger<TwelveDataClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["TradingSettings:TwelveDataApiKey"] ?? string.Empty;
    }

    public async Task<List<Candle>> GetHistoricalCandlesAsync(string symbol, string timeframe, int size = 250)
    {
        if (string.IsNullOrEmpty(_apiKey) || _apiKey == "TU_TWELVE_DATA_API_KEY")
        {
            _logger.LogWarning("TwelveData API Key no configurada. Retornando velas de prueba (mock).");
            return GetMockCandles(symbol, timeframe, size);
        }

        try
        {
            // Twelve Data usa intervalos tipo: 1min, 5min, 15min, 30min, 45min, 1h, 2h, 4h, 1day...
            // Homologamos "15M" o "15m" a "15min"
            string interval = timeframe.ToLower().Replace("m", "min");
            if (interval == "1h") interval = "1h";
            if (interval == "4h") interval = "4h";

            var url = $"time_series?symbol={Uri.EscapeDataString(symbol)}&interval={interval}&outputsize={size}&apikey={_apiKey}";
            
            _logger.LogInformation("Consultando Twelve Data: {Url}", url);
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TwelveDataTimeSeriesResponse>(jsonString);

            if (result == null || result.Status != "ok" || result.Values == null)
            {
                _logger.LogError("Error de la API de Twelve Data o respuesta vacía: {Json}", jsonString);
                throw new Exception($"Error de Twelve Data: {jsonString}");
            }

            var candles = new List<Candle>();
            foreach (var val in result.Values)
            {
                // Parsear fecha en formato yyyy-MM-dd HH:mm:ss (a veces yyyy-MM-dd en daily)
                if (DateTime.TryParseExact(val.Datetime, 
                    new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd" }, 
                    CultureInfo.InvariantCulture, 
                    DateTimeStyles.AssumeUniversal, 
                    out var openTime))
                {
                    candles.Add(new Candle
                    {
                        Symbol = symbol,
                        Timeframe = timeframe,
                        Date = openTime,
                        Open = decimal.Parse(val.Open, CultureInfo.InvariantCulture),
                        High = decimal.Parse(val.High, CultureInfo.InvariantCulture),
                        Low = decimal.Parse(val.Low, CultureInfo.InvariantCulture),
                        Close = decimal.Parse(val.Close, CultureInfo.InvariantCulture),
                        Volume = decimal.Parse(val.Volume ?? "0", CultureInfo.InvariantCulture)
                    });
                }
            }

            // Ordenamos de antiguo a reciente para Skender.Stock.Indicators
            candles.Sort((x, y) => x.Date.CompareTo(y.Date));
            return candles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener velas históricas de Twelve Data. Usando velas mock como plan de contingencia.");
            return GetMockCandles(symbol, timeframe, size);
        }
    }

    // Método fallback para pruebas rápidas sin API Key
    private List<Candle> GetMockCandles(string symbol, string timeframe, int size)
    {
        var list = new List<Candle>();
        var random = new Random();
        
        decimal basePrice = 1.1680m;
        decimal priceStep = 0.0005m;
        
        string s = symbol.ToUpperInvariant();
        if (s == "AAPL") { basePrice = 220.50m; priceStep = 0.45m; }
        else if (s == "TSLA") { basePrice = 210.00m; priceStep = 0.85m; }
        else if (s == "NVDA") { basePrice = 125.30m; priceStep = 0.50m; }
        else if (s == "MSFT") { basePrice = 415.80m; priceStep = 0.70m; }
        else if (s == "AMZN") { basePrice = 175.20m; priceStep = 0.40m; }
        else if (s == "XAU/USD" || s == "XAUUSD") { basePrice = 2512.80m; priceStep = 3.50m; }
        else if (s == "XAG/USD" || s == "XAGUSD") { basePrice = 29.85m; priceStep = 0.15m; }
        else if (s == "BTC/USD" || s == "BTCUSD") { basePrice = 64250.00m; priceStep = 120.00m; }
        else if (s == "ETH/USD" || s == "ETHUSD") { basePrice = 2755.00m; priceStep = 15.00m; }
        else if (s == "SOL/USD" || s == "SOLUSD") { basePrice = 145.80m; priceStep = 1.20m; }
        else if (s == "XRP/USD" || s == "XRPUSD") { basePrice = 0.5850m; priceStep = 0.008m; }
        else if (s == "ADA/USD" || s == "ADAUSD") { basePrice = 0.3650m; priceStep = 0.005m; }
        else if (s.Contains("GBP")) { basePrice = 1.3100m; priceStep = 0.0005m; }
        else if (s.Contains("JPY")) { basePrice = 145.20m; priceStep = 0.05m; }

        var now = DateTime.UtcNow;
        var intervalMinutes = timeframe.ToLower().Contains("h") ? 60 : 15;

        for (int i = size; i >= 0; i--)
        {
            var date = now.AddMinutes(-i * intervalMinutes);
            var change = (decimal)(random.NextDouble() - 0.49) * priceStep;
            var open = basePrice;
            var close = basePrice + change;
            var high = Math.Max(open, close) + (decimal)random.NextDouble() * (priceStep * 0.5m);
            var low = Math.Min(open, close) - (decimal)random.NextDouble() * (priceStep * 0.5m);
            
            list.Add(new Candle
            {
                Symbol = symbol,
                Timeframe = timeframe,
                Date = date,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = random.Next(1000, 50000)
            });

            basePrice = close;
        }

        return list;
    }
}

public class TwelveDataTimeSeriesResponse
{
    [JsonPropertyName("meta")]
    public object? Meta { get; set; }

    [JsonPropertyName("values")]
    public List<TwelveDataValue>? Values { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "error";
}

public class TwelveDataValue
{
    [JsonPropertyName("datetime")]
    public string Datetime { get; set; } = string.Empty;

    [JsonPropertyName("open")]
    public string Open { get; set; } = string.Empty;

    [JsonPropertyName("high")]
    public string High { get; set; } = string.Empty;

    [JsonPropertyName("low")]
    public string Low { get; set; } = string.Empty;

    [JsonPropertyName("close")]
    public string Close { get; set; } = string.Empty;

    [JsonPropertyName("volume")]
    public string? Volume { get; set; }
}
