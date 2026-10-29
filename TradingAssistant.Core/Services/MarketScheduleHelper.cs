using System;

namespace TradingAssistant.Core.Services;

public static class MarketScheduleHelper
{
    public static bool IsMarketOpen(string symbol, out string scheduleDetails)
    {
        var utcNow = DateTime.UtcNow;
        bool isStock = !symbol.Contains("/");

        // Permitir pruebas unitarias y de simulación
        if (symbol.Contains("MOCK", StringComparison.OrdinalIgnoreCase) || symbol.Contains("TEST", StringComparison.OrdinalIgnoreCase))
        {
            scheduleDetails = "Modo de Prueba / Simulación.";
            return true;
        }

        string cleanSymbol = symbol.ToUpperInvariant();
        bool isCrypto = cleanSymbol.StartsWith("BTC") || cleanSymbol.StartsWith("ETH") || 
                       cleanSymbol.StartsWith("SOL") || cleanSymbol.StartsWith("XRP") || 
                       cleanSymbol.StartsWith("ADA");

        if (isCrypto)
        {
            scheduleDetails = "Mercado Cripto ABIERTO 24/7 (365 días al año).";
            return true;
        }

        try
        {
            TimeZoneInfo estZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime estNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, estZone);

            if (isStock)
            {
                // Mercado de Acciones EE. UU. (NYSE/NASDAQ)
                // Horario: Lunes a Viernes de 9:30 AM a 4:00 PM EST
                if (estNow.DayOfWeek == DayOfWeek.Saturday || estNow.DayOfWeek == DayOfWeek.Sunday)
                {
                    scheduleDetails = "El mercado bursátil de EE. UU. (NYSE/NASDAQ) está cerrado los fines de semana.";
                    return false;
                }

                var openTime = new TimeSpan(9, 30, 0);
                var closeTime = new TimeSpan(16, 0, 0);

                if (estNow.TimeOfDay < openTime || estNow.TimeOfDay >= closeTime)
                {
                    scheduleDetails = $"El mercado de acciones está cerrado. Opera de Lunes a Viernes de 9:30 AM a 4:00 PM EST (Hora actual en Nueva York: {estNow:hh:mm tt}).";
                    return false;
                }

                scheduleDetails = "Mercado bursátil ABIERTO.";
                return true;
            }
            else
            {
                // Mercado Forex (EUR/USD, GBP/USD, etc.)
                // Abre Domingo a las 5:00 PM EST y Cierra Viernes a las 5:00 PM EST.
                if (estNow.DayOfWeek == DayOfWeek.Saturday)
                {
                    scheduleDetails = "El mercado Forex está cerrado los sábados.";
                    return false;
                }

                if (estNow.DayOfWeek == DayOfWeek.Friday && estNow.TimeOfDay >= new TimeSpan(17, 0, 0))
                {
                    scheduleDetails = "El mercado Forex cerró hoy viernes a las 5:00 PM EST y reabre el domingo.";
                    return false;
                }

                if (estNow.DayOfWeek == DayOfWeek.Sunday && estNow.TimeOfDay < new TimeSpan(17, 0, 0))
                {
                    scheduleDetails = "El mercado Forex está cerrado. Reabre hoy domingo a las 5:00 PM EST (Sesión de Sídney).";
                    return false;
                }

                scheduleDetails = "Mercado Forex ABIERTO.";
                return true;
            }
        }
        catch
        {
            // Fallback en caso de incompatibilidad de TimeZoneInfo
            if (utcNow.DayOfWeek == DayOfWeek.Saturday || utcNow.DayOfWeek == DayOfWeek.Sunday)
            {
                scheduleDetails = "Mercado cerrado durante el fin de semana.";
                return false;
            }

            scheduleDetails = "Mercado ABIERTO.";
            return true;
        }
    }
}
