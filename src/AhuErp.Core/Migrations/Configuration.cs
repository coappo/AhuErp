using System.Data.Entity.Migrations;
using AhuErp.Core.Data;

namespace AhuErp.Core.Migrations
{
    /// <summary>
    /// Конфигурация EF6 Code-First миграций. Автоматические миграции отключены —
    /// изменения схемы оформляются явными миграциями через <c>Add-Migration</c> /
    /// <c>migrate.exe</c>.
    /// </summary>
    public sealed class Configuration : DbMigrationsConfiguration<AhuDbContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
            MigrationsDirectory = "Migrations";
            ContextKey = "AhuErp.Core.Data.AhuDbContext";
        }

        protected override void Seed(AhuDbContext context)
        {
            // Начальное наполнение справочников при первой миграции.
            // Реальные seed-данные появятся на следующих фазах.
        }
    }
}
