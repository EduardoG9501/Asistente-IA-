using System;
using System.Collections.Generic;
using System.Linq;
using TradingAssistant.Core.Interfaces;
using TradingAssistant.Core.Models;

namespace TradingAssistant.Core.Services;

public class SignalService : ISignalService
{
    public SignalType DetermineSignal(TechnicalIndicatorsResult technicals, List<Candle> candles, out string technicalJustification, out OrderType orderType, out string strategyName, RiskMode riskMode = RiskMode.Safe)
    {
        orderType = OrderType.BuyMarket;
        strategyName = string.Empty;

        if (technicals == null)
        {
            technicalJustification = "No se proporcionaron datos técnicos válidos.";
            return SignalType.NO_TRADE;
        }

        // 0. Validar Horario de Mercado (Mercado Cerrado -> No emitir señales)
        if (!MarketScheduleHelper.IsMarketOpen(technicals.Symbol, out string scheduleReason))
        {
            technicalJustification = $"NO OPERAR: MERCADO CERRADO. {scheduleReason}";
            strategyName = "MERCADO CERRADO";
            orderType = OrderType.BuyMarket;
            return SignalType.NO_TRADE;
        }

        bool isUpTrend = technicals.Ema50GreaterThanEma200;
        bool isDownTrend = !isUpTrend;

        if (riskMode == RiskMode.Safe)
        {
            // ----------------------------------------------------
            // MODO SEGURO: Reglas estrictas (Pocas señales, alta precisión)
            // ----------------------------------------------------
            bool pullBackBuy = technicals.CurrentPrice >= technicals.Ema50 && technicals.CurrentPrice <= (technicals.Ema20 + (technicals.Atr * 0.2m));
            bool rsiBuyPullback = technicals.Rsi >= 30m && technicals.Rsi <= 48m;

            bool pullBackSell = technicals.CurrentPrice <= technicals.Ema50 && technicals.CurrentPrice >= (technicals.Ema20 - (technicals.Atr * 0.2m));
            bool rsiSellPullback = technicals.Rsi >= 52m && technicals.Rsi <= 70m;

            if (isUpTrend && pullBackBuy && rsiBuyPullback)
            {
                strategyName = "RETROCESO (SEGURO)";
                orderType = OrderType.BuyLimit;
                technicalJustification = $"COMPRA EN RETROCESO [SEGURO]. Tendencia mayor alcista. " +
                                         $"Precio en soporte dinámico (Precio: {technicals.CurrentPrice:F5}, EMA 20: {technicals.Ema20:F5}, EMA 50: {technicals.Ema50:F5}). " +
                                         $"RSI en sobreventa relativa ({technicals.Rsi:F2}).";
                return SignalType.BUY;
            }

            if (isDownTrend && pullBackSell && rsiSellPullback)
            {
                strategyName = "RETROCESO (SEGURO)";
                orderType = OrderType.SellLimit;
                technicalJustification = $"VENTA EN RETROCESO [SEGURO]. Tendencia mayor bajista. " +
                                         $"Precio en resistencia dinámica (Precio: {technicals.CurrentPrice:F5}, EMA 20: {technicals.Ema20:F5}, EMA 50: {technicals.Ema50:F5}). " +
                                         $"RSI en sobrecompra relativa ({technicals.Rsi:F2}).";
                return SignalType.SELL;
            }

            // Momentum seguro requiere ruptura de 5 velas
            bool breakoutBuy = false;
            bool breakoutSell = false;
            decimal maxClosePrice = 0;
            decimal minClosePrice = 0;

            if (candles != null && candles.Count >= 6)
            {
                var sorted = candles.OrderBy(c => c.Date).ToList();
                var lastIndex = sorted.Count - 1;
                var recent5 = sorted.Skip(lastIndex - 5).Take(5).ToList();
                maxClosePrice = recent5.Max(c => c.Close);
                minClosePrice = recent5.Min(c => c.Close);
                breakoutBuy = technicals.CurrentPrice > maxClosePrice;
                breakoutSell = technicals.CurrentPrice < minClosePrice;
            }

            bool isStrongUpTrend = isUpTrend && (technicals.Ema20 > technicals.Ema50) && (technicals.CurrentPrice > technicals.Ema20);
            bool rsiBuyMomentum = technicals.Rsi >= 55m && technicals.Rsi <= 75m;

            if (isStrongUpTrend && rsiBuyMomentum && breakoutBuy)
            {
                strategyName = "CONTINUACIÓN (SEGURO)";
                orderType = OrderType.BuyStop;
                technicalJustification = $"COMPRA POR RUPTURA [SEGURO]. Tendencia alcista fuerte. " +
                                         $"Ruptura de máximo de 5 velas ({maxClosePrice:F5}). " +
                                         $"RSI con impulso alcista ({technicals.Rsi:F2}, requerido [55-75]).";
                return SignalType.BUY;
            }

            bool isStrongDownTrend = isDownTrend && (technicals.Ema20 < technicals.Ema50) && (technicals.CurrentPrice < technicals.Ema20);
            bool rsiSellMomentum = technicals.Rsi >= 25m && technicals.Rsi <= 45m;

            if (isStrongDownTrend && rsiSellMomentum && breakoutSell)
            {
                strategyName = "CONTINUACIÓN (SEGURO)";
                orderType = OrderType.SellStop;
                technicalJustification = $"VENTA POR RUPTURA [SEGURO]. Tendencia bajista fuerte. " +
                                         $"Ruptura de mínimo de 5 velas ({minClosePrice:F5}). " +
                                         $"RSI con impulso bajista ({technicals.Rsi:F2}, requerido [25-45]).";
                return SignalType.SELL;
            }

            var reasons = new List<string>();
            reasons.Add("[Modo Seguro] Tendencia: " + (isUpTrend ? "ALCISTA" : "BAJISTA"));
            reasons.Add($"RSI actual: {technicals.Rsi:F2}");
            if (isUpTrend)
            {
                if (!pullBackBuy && !isStrongUpTrend)
                    reasons.Add("Precio fuera de zona de valor");
                else if (pullBackBuy && !rsiBuyPullback)
                    reasons.Add("Filtro pullback falló por RSI (requerido [30-48])");
                else if (isStrongUpTrend && !rsiBuyMomentum)
                    reasons.Add("Filtro momentum falló por RSI (requerido [55-75])");
                else if (isStrongUpTrend && !breakoutBuy)
                    reasons.Add($"Filtro momentum falló por no romper máximo previo ({maxClosePrice:F5})");
            }
            else
            {
                if (!pullBackSell && !isStrongDownTrend)
                    reasons.Add("Precio fuera de zona de valor bajista");
                else if (pullBackSell && !rsiSellPullback)
                    reasons.Add("Filtro pullback bajista falló por RSI (requerido [52-70])");
                else if (isStrongDownTrend && !rsiSellMomentum)
                    reasons.Add("Filtro momentum bajista falló por RSI (requerido [25-45])");
                else if (isStrongDownTrend && !breakoutSell)
                    reasons.Add($"Filtro momentum bajista falló por no romper mínimo previo ({minClosePrice:F5})");
            }

            technicalJustification = "NO OPERAR: " + string.Join(" | ", reasons);
            return SignalType.NO_TRADE;
        }
        else
        {
            // ----------------------------------------------------
            // MODO AGRESIVO: Reglas más flexibles (Más señales, mayor riesgo)
            // ----------------------------------------------------
            // Pullback compra agresivo: Precio cerca de EMA50/EMA20 (rango +- 0.5 ATR), RSI entre 25 y 55
            bool pullBackBuyAggressive = technicals.CurrentPrice >= (technicals.Ema50 - (technicals.Atr * 0.5m)) && technicals.CurrentPrice <= (technicals.Ema20 + (technicals.Atr * 0.5m));
            bool rsiBuyPullbackAggressive = technicals.Rsi >= 25m && technicals.Rsi <= 55m;

            // Pullback venta agresivo: Precio cerca de EMA50/EMA20 (rango +- 0.5 ATR), RSI entre 45 y 75
            bool pullBackSellAggressive = technicals.CurrentPrice <= (technicals.Ema50 + (technicals.Atr * 0.5m)) && technicals.CurrentPrice >= (technicals.Ema20 - (technicals.Atr * 0.5m));
            bool rsiSellPullbackAggressive = technicals.Rsi >= 45m && technicals.Rsi <= 75m;

            if (isUpTrend && pullBackBuyAggressive && rsiBuyPullbackAggressive)
            {
                strategyName = "RETROCESO (AGRESIVO)";
                orderType = OrderType.BuyLimit;
                technicalJustification = $"COMPRA EN RETROCESO [AGRESIVO]. Tendencia alcista. " +
                                         $"Precio en zona amplia de soporte (Precio: {technicals.CurrentPrice:F5}). " +
                                         $"RSI flexible ({technicals.Rsi:F2}, rango agresivo [25-55]).";
                return SignalType.BUY;
            }

            if (isDownTrend && pullBackSellAggressive && rsiSellPullbackAggressive)
            {
                strategyName = "RETROCESO (AGRESIVO)";
                orderType = OrderType.SellLimit;
                technicalJustification = $"VENTA EN RETROCESO [AGRESIVO]. Tendencia bajista. " +
                                         $"Precio en zona amplia de resistencia (Precio: {technicals.CurrentPrice:F5}). " +
                                         $"RSI flexible ({technicals.Rsi:F2}, rango agresivo [45-75]).";
                return SignalType.SELL;
            }

            // Momentum agresivo: Impulso a favor de tendencia (no requiere ruptura estricta de 5 velas)
            bool momentumBuyAggressive = isUpTrend && technicals.CurrentPrice > technicals.Ema20 && technicals.Rsi >= 48m && technicals.Rsi <= 80m;
            bool momentumSellAggressive = isDownTrend && technicals.CurrentPrice < technicals.Ema20 && technicals.Rsi >= 20m && technicals.Rsi <= 52m;

            if (momentumBuyAggressive)
            {
                strategyName = "IMPULSO (AGRESIVO)";
                orderType = OrderType.BuyMarket;
                technicalJustification = $"COMPRA POR IMPULSO [AGRESIVO]. Precio sobre EMA20 ({technicals.CurrentPrice:F5} > {technicals.Ema20:F5}). " +
                                         $"RSI en zona de impulso ({technicals.Rsi:F2}, rango agresivo [48-80]).";
                return SignalType.BUY;
            }

            if (momentumSellAggressive)
            {
                strategyName = "IMPULSO (AGRESIVO)";
                orderType = OrderType.SellMarket;
                technicalJustification = $"VENTA POR IMPULSO [AGRESIVO]. Precio bajo EMA20 ({technicals.CurrentPrice:F5} < {technicals.Ema20:F5}). " +
                                         $"RSI en zona de impulso ({technicals.Rsi:F2}, rango agresivo [20-52]).";
                return SignalType.SELL;
            }

            var reasons = new List<string>();
            reasons.Add("[Modo Agresivo] Tendencia: " + (isUpTrend ? "ALCISTA" : "BAJISTA"));
            reasons.Add($"RSI actual: {technicals.Rsi:F2}");
            reasons.Add($"Precio actual: {technicals.CurrentPrice:F5}, EMA20: {technicals.Ema20:F5}, EMA50: {technicals.Ema50:F5}");
            reasons.Add("No se cumplieron parámetros ni de retroceso amplio ni de impulso directo.");

            technicalJustification = "NO OPERAR (AGRESIVO): " + string.Join(" | ", reasons);
            return SignalType.NO_TRADE;
        }
    }
}
