namespace AhuErp.Core.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddVehicleTripDriverName : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.VehicleTrips", "DriverName", c => c.String(maxLength: 128));
        }
        
        public override void Down()
        {
            DropColumn("dbo.VehicleTrips", "DriverName");
        }
    }
}
