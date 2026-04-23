using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using AhuErp.Core.Models;

namespace AhuErp.Core.Data
{
    /// <summary>
    /// Корневой EF6 Code-First контекст системы АХУ.
    /// TPH-иерархия документов: <see cref="Document"/> → <see cref="ArchiveRequest"/>.
    /// </summary>
    public class AhuDbContext : DbContext
    {
        public AhuDbContext()
            : base("name=AhuErpDb")
        {
        }

        public AhuDbContext(string nameOrConnectionString)
            : base(nameOrConnectionString)
        {
        }

        public virtual DbSet<Employee> Employees { get; set; }
        public virtual DbSet<Document> Documents { get; set; }
        public virtual DbSet<ArchiveRequest> ArchiveRequests { get; set; }
        public virtual DbSet<ItTicket> ItTickets { get; set; }
        public virtual DbSet<Vehicle> Vehicles { get; set; }
        public virtual DbSet<VehicleTrip> VehicleTrips { get; set; }
        public virtual DbSet<InventoryItem> InventoryItems { get; set; }
        public virtual DbSet<InventoryTransaction> InventoryTransactions { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

            modelBuilder.Entity<Employee>()
                .ToTable("Employees");

            modelBuilder.Entity<Document>()
                .Map<Document>(m =>
                {
                    m.Requires("DocumentKind").HasValue("Document");
                    m.ToTable("Documents");
                })
                .Map<ArchiveRequest>(m =>
                {
                    m.Requires("DocumentKind").HasValue("ArchiveRequest");
                    m.ToTable("Documents");
                })
                .Map<ItTicket>(m =>
                {
                    m.Requires("DocumentKind").HasValue("ItTicket");
                    m.ToTable("Documents");
                });

            modelBuilder.Entity<Document>()
                .HasOptional(d => d.AssignedEmployee)
                .WithMany(e => e.AssignedDocuments)
                .HasForeignKey(d => d.AssignedEmployeeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Vehicle>()
                .ToTable("Vehicles");

            modelBuilder.Entity<VehicleTrip>()
                .ToTable("VehicleTrips");

            modelBuilder.Entity<VehicleTrip>()
                .HasRequired(t => t.Vehicle)
                .WithMany(v => v.Trips)
                .HasForeignKey(t => t.VehicleId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<VehicleTrip>()
                .HasOptional(t => t.Document)
                .WithMany(d => d.VehicleTrips)
                .HasForeignKey(t => t.DocumentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<InventoryItem>()
                .ToTable("InventoryItems");

            modelBuilder.Entity<InventoryTransaction>()
                .ToTable("InventoryTransactions");

            modelBuilder.Entity<InventoryTransaction>()
                .HasRequired(t => t.InventoryItem)
                .WithMany(i => i.Transactions)
                .HasForeignKey(t => t.InventoryItemId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<InventoryTransaction>()
                .HasOptional(t => t.Document)
                .WithMany()
                .HasForeignKey(t => t.DocumentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<InventoryTransaction>()
                .HasRequired(t => t.Initiator)
                .WithMany()
                .HasForeignKey(t => t.InitiatorId)
                .WillCascadeOnDelete(false);

            base.OnModelCreating(modelBuilder);
        }
    }
}
