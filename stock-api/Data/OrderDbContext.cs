using Microsoft.EntityFrameworkCore;
using stock_api.Models;

namespace stock_api.Data;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLineItem> OrderLineItems => Set<OrderLineItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(customer => customer.Name).HasMaxLength(120).IsRequired();
            entity.Property(customer => customer.Email).HasMaxLength(254);
            entity.Property(customer => customer.Phone).HasMaxLength(40);
            entity.Property<string?>("NormalizedEmail")
                .HasComputedColumnSql("lower(btrim(\"Email\"))", stored: true);
            entity.HasIndex("NormalizedEmail")
                .IsUnique()
                .HasFilter("\"NormalizedEmail\" IS NOT NULL AND \"NormalizedEmail\" <> ''");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(order => order.CustomerNameSnapshot).HasMaxLength(120).IsRequired();
            entity.Property(order => order.Status).HasMaxLength(20).IsRequired();
            entity.Property(order => order.DuplicateKey).HasMaxLength(64).IsFixedLength();
            entity.Property(order => order.Total).HasPrecision(12, 2);
            entity.HasIndex(order => new { order.DuplicateKey, order.CreatedAt });
            entity.HasOne(order => order.Customer)
                .WithMany(customer => customer.Orders)
                .HasForeignKey(order => order.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(order => order.Items)
                .WithOne(item => item.Order)
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderLineItem>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Sku).HasMaxLength(64);
            entity.Property(item => item.NormalizedSku)
                .HasComputedColumnSql("upper(btrim(\"Sku\"))", stored: true);
            entity.HasIndex(item => new { item.OrderId, item.NormalizedSku })
                .IsUnique()
                .HasFilter("\"NormalizedSku\" IS NOT NULL");
            entity.Property(item => item.UnitPrice).HasPrecision(12, 2);
            entity.Property(item => item.LineTotal).HasPrecision(12, 2);
        });
    }
}
