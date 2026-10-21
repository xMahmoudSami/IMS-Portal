using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace Inventory_Management_System.Models
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext()
        {
        }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // DbSets for all 9 Entities
        public DbSet<Category> Categories { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<SupplierProduct> SupplierProducts { get; set; }
        public DbSet<Purchase> Purchases { get; set; }
        public DbSet<PurchaseItem> PurchaseItems { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<AIChatLog> AIChatLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Unique Indexes & Constraints
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.SKU)
                .IsUnique();

            modelBuilder.Entity<SupplierProduct>()
                .HasIndex(sp => new { sp.SupplierID, sp.ProductID })
                .IsUnique();

            // 2. Precision decimal(18,2)
            modelBuilder.Entity<Product>().Property(p => p.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<SupplierProduct>().Property(sp => sp.ContractPrice).HasPrecision(18, 2);
            modelBuilder.Entity<Purchase>().Property(p => p.TotalAmount).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseItem>().Property(pi => pi.UnitCost).HasPrecision(18, 2);
            modelBuilder.Entity<Sale>().Property(s => s.TotalAmount).HasPrecision(18, 2);
            modelBuilder.Entity<SaleItem>().Property(si => si.UnitPrice).HasPrecision(18, 2);

            // 3. Relationships & Delete Behaviors

            // Categories -> Products (Restrict)
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryID)
                .OnDelete(DeleteBehavior.Restrict);

            // Suppliers -> Purchases (Restrict)
            modelBuilder.Entity<Purchase>()
                .HasOne(p => p.Supplier)
                .WithMany(s => s.Purchases)
                .HasForeignKey(p => p.SupplierID)
                .OnDelete(DeleteBehavior.Restrict);

            // Suppliers -> SupplierProducts (Cascade)
            modelBuilder.Entity<SupplierProduct>()
                .HasOne(sp => sp.Supplier)
                .WithMany(s => s.SupplierProducts)
                .HasForeignKey(sp => sp.SupplierID)
                .OnDelete(DeleteBehavior.Cascade);

            // Products -> SupplierProducts (Cascade)
            modelBuilder.Entity<SupplierProduct>()
                .HasOne(sp => sp.Product)
                .WithMany(p => p.SupplierProducts)
                .HasForeignKey(sp => sp.ProductID)
                .OnDelete(DeleteBehavior.Cascade);

            // Purchases -> PurchaseItems (Cascade)
            modelBuilder.Entity<PurchaseItem>()
                .HasOne(pi => pi.Purchase)
                .WithMany(p => p.PurchaseItems)
                .HasForeignKey(pi => pi.PurchaseID)
                .OnDelete(DeleteBehavior.Cascade);

            // Products -> PurchaseItems (Restrict)
            modelBuilder.Entity<PurchaseItem>()
                .HasOne(pi => pi.Product)
                .WithMany(p => p.PurchaseItems)
                .HasForeignKey(pi => pi.ProductID)
                .OnDelete(DeleteBehavior.Restrict);

            // Sales -> SaleItems (Cascade)
            modelBuilder.Entity<SaleItem>()
                .HasOne(si => si.Sale)
                .WithMany(s => s.SaleItems)
                .HasForeignKey(si => si.SaleID)
                .OnDelete(DeleteBehavior.Cascade);

            // Products -> SaleItems (Restrict)
            modelBuilder.Entity<SaleItem>()
                .HasOne(si => si.Product)
                .WithMany(p => p.SaleItems)
                .HasForeignKey(si => si.ProductID)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}