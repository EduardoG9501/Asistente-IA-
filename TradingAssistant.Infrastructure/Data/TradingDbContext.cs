using Microsoft.EntityFrameworkCore;
using TradingAssistant.Core.Models;

namespace TradingAssistant.Infrastructure.Data;

public class TradingDbContext : DbContext
{
    public TradingDbContext(DbContextOptions<TradingDbContext> options) : base(options)
    {
    }

    public DbSet<Candle> Candles => Set<Candle>();
    public DbSet<TradingSignal> TradingSignals => Set<TradingSignal>();
    public DbSet<AccountSettings> AccountSettings => Set<AccountSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Índices y restricciones para Candle
        modelBuilder.Entity<Candle>()
            .HasIndex(c => new { c.Symbol, c.Timeframe, c.Date })
            .IsUnique();

        modelBuilder.Entity<Candle>()
            .Property(c => c.Open)
            .HasConversion<double>(); // SQLite maneja mejor decimales como doubles si no se especifica tipo
        
        modelBuilder.Entity<Candle>()
            .Property(c => c.High)
            .HasConversion<double>();

        modelBuilder.Entity<Candle>()
            .Property(c => c.Low)
            .HasConversion<double>();

        modelBuilder.Entity<Candle>()
            .Property(c => c.Close)
            .HasConversion<double>();

        modelBuilder.Entity<Candle>()
            .Property(c => c.Volume)
            .HasConversion<double>();

        // Conversión de decimales para TradingSignal en SQLite
        modelBuilder.Entity<TradingSignal>()
            .Property(s => s.EntryPrice)
            .HasConversion<double>();

        modelBuilder.Entity<TradingSignal>()
            .Property(s => s.StopLoss)
            .HasConversion<double>();

        modelBuilder.Entity<TradingSignal>()
            .Property(s => s.TakeProfit1)
            .HasConversion<double>();

        modelBuilder.Entity<TradingSignal>()
            .Property(s => s.TakeProfit2)
            .HasConversion<double>();

        modelBuilder.Entity<TradingSignal>()
            .Property(s => s.TakeProfit3)
            .HasConversion<double>();

        modelBuilder.Entity<TradingSignal>()
            .Property(s => s.RiskRewardRatio)
            .HasConversion<double>();

        modelBuilder.Entity<TradingSignal>()
            .Property(s => s.PositionSize)
            .HasConversion<double>();

        modelBuilder.Entity<TradingSignal>()
            .Property(s => s.RiskAmount)
            .HasConversion<double>();

        // Conversión de decimales para AccountSettings en SQLite
        modelBuilder.Entity<AccountSettings>()
            .Property(a => a.Capital)
            .HasConversion<double>();

        modelBuilder.Entity<AccountSettings>()
            .Property(a => a.MaxRiskPercentage)
            .HasConversion<double>();
    }
}
