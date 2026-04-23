using System;
using System.Data.Entity.Migrations.Design;
using System.IO;
using AhuErp.Core.Migrations;

namespace AhuErp.Tools.MigrationGenerator
{
    /// <summary>
    /// Одноразовый генератор первой миграции EF6. Запускается через <c>mono</c>
    /// на Linux или <c>dotnet</c> на Windows. Выводит файлы
    /// <c>&lt;stamp&gt;_InitialCreate.cs / .Designer.cs / .resx</c> в указанную
    /// папку. Не входит в основной solution, используется только в CI/скриптах.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var migrationsDir = args.Length > 0
                ? args[0]
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations");
            var migrationName = args.Length > 1 ? args[1] : "InitialCreate";

            Directory.CreateDirectory(migrationsDir);

            var scaffolder = new MigrationScaffolder(new Configuration());
            var result = scaffolder.Scaffold(migrationName, ignoreChanges: false);

            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var baseName = $"{stamp}_{migrationName}";

            File.WriteAllText(Path.Combine(migrationsDir, baseName + ".cs"), result.UserCode);
            File.WriteAllText(Path.Combine(migrationsDir, baseName + ".Designer.cs"), result.DesignerCode);

            using (var fs = File.Create(Path.Combine(migrationsDir, baseName + ".resx")))
            using (var writer = new System.Resources.ResXResourceWriter(fs))
            {
                foreach (var kvp in result.Resources)
                {
                    writer.AddResource(kvp.Key, kvp.Value);
                }
                writer.Generate();
            }

            Console.WriteLine($"Migration '{baseName}' written to {migrationsDir}");
            return 0;
        }
    }
}
