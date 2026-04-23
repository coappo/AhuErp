namespace AhuErp.Core.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Documents",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Type = c.Int(nullable: false),
                        Title = c.String(nullable: false, maxLength: 512),
                        CreationDate = c.DateTime(nullable: false),
                        Deadline = c.DateTime(nullable: false),
                        Status = c.Int(nullable: false),
                        AssignedEmployeeId = c.Int(),
                        HasPassportScan = c.Boolean(),
                        HasWorkBookScan = c.Boolean(),
                        DocumentKind = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Employees", t => t.AssignedEmployeeId)
                .Index(t => t.AssignedEmployeeId);
            
            CreateTable(
                "dbo.Employees",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        FullName = c.String(nullable: false, maxLength: 256),
                        Position = c.String(maxLength: 256),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.VehicleTrips",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        VehicleId = c.Int(nullable: false),
                        StartDate = c.DateTime(nullable: false),
                        EndDate = c.DateTime(nullable: false),
                        DocumentId = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Documents", t => t.DocumentId)
                .ForeignKey("dbo.Vehicles", t => t.VehicleId, cascadeDelete: true)
                .Index(t => t.VehicleId)
                .Index(t => t.DocumentId);
            
            CreateTable(
                "dbo.Vehicles",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Model = c.String(nullable: false, maxLength: 128),
                        LicensePlate = c.String(nullable: false, maxLength: 32),
                        CurrentStatus = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.VehicleTrips", "VehicleId", "dbo.Vehicles");
            DropForeignKey("dbo.VehicleTrips", "DocumentId", "dbo.Documents");
            DropForeignKey("dbo.Documents", "AssignedEmployeeId", "dbo.Employees");
            DropIndex("dbo.VehicleTrips", new[] { "DocumentId" });
            DropIndex("dbo.VehicleTrips", new[] { "VehicleId" });
            DropIndex("dbo.Documents", new[] { "AssignedEmployeeId" });
            DropTable("dbo.Vehicles");
            DropTable("dbo.VehicleTrips");
            DropTable("dbo.Employees");
            DropTable("dbo.Documents");
        }
    }
}
