using Microsoft.EntityFrameworkCore;
using pos_backend.Models;

namespace pos_backend.Data;

public class PosDbContext : DbContext
{
    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<SyncOutbox> SyncOutbox => Set<SyncOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Category config
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        });

        // Product config
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.CostPrice).HasPrecision(18, 2);
            entity.HasIndex(e => e.Barcode);
            entity.HasIndex(e => e.Sku);

            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Order config
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
            entity.Property(e => e.DiscountTotal).HasPrecision(18, 2);
            entity.Property(e => e.TaxTotal).HasPrecision(18, 2);
            entity.Property(e => e.GrandTotal).HasPrecision(18, 2);

            entity.HasOne(e => e.Shift)
                  .WithMany(s => s.Orders)
                  .HasForeignKey(e => e.ShiftId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // OrderItem config
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.TotalPrice).HasPrecision(18, 2);
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);

            entity.HasOne(e => e.Order)
                  .WithMany(o => o.Items)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Product)
                  .WithMany()
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Payment config
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AmountTendered).HasPrecision(18, 2);
            entity.Property(e => e.ChangeGiven).HasPrecision(18, 2);
            entity.Property(e => e.TotalPaid).HasPrecision(18, 2);

            entity.HasOne(e => e.Order)
                  .WithMany(o => o.Payments)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Shift config
        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StartingFloat).HasPrecision(18, 2);
            entity.Property(e => e.CashSales).HasPrecision(18, 2);
            entity.Property(e => e.NonCashSales).HasPrecision(18, 2);
            entity.Property(e => e.ExpectedCash).HasPrecision(18, 2);
            entity.Property(e => e.ActualCash).HasPrecision(18, 2);
            entity.Property(e => e.Discrepancy).HasPrecision(18, 2);
        });

        // SyncOutbox config
        modelBuilder.Entity<SyncOutbox>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IsSynced);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}

