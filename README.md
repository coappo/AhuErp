# AhuErp — ERP/EDMS для МКУ АХУ БМР

Phase 1 (Foundation) комплексной системы управления документооборотом и административно-хозяйственной деятельностью. Заложен архитектурный фундамент: строгий MVVM, чистое разделение доменной модели, EF6 Code-First и покрытие бизнес-логики unit-тестами.

## Архитектура

```
AhuErp.sln
├── src/
│   ├── AhuErp.Core/     ← .NET Framework 4.8, SDK-style class library
│   │   ├── Models/      ← Employee, Document, ArchiveRequest (TPH), Vehicle, VehicleTrip
│   │   ├── Data/        ← AhuDbContext (EF6)
│   │   ├── Migrations/  ← EF6 Code-First миграции (+ .resx snapshot)
│   │   └── Services/    ← ArchiveService, FleetService, DashboardService
│   └── AhuErp.UI/       ← .NET Framework 4.8, SDK-style WPF Application (UseWPF)
│       ├── App.xaml/.cs
│       ├── MainWindow.xaml/.cs   ← только InitializeComponent()
│       ├── ViewModels/  ← MVVM на CommunityToolkit.Mvvm
│       ├── Views/       ← DashboardView, PlaceholderView
│       └── Converters/  ← OverdueRowColorConverter
└── tests/
    └── AhuErp.Tests/    ← xUnit, 21 тест
```

Все три проекта — SDK-style `.csproj`, `TargetFramework=net48`, что позволяет
`dotnet build` / `dotnet test` / `dotnet format` работать без Visual Studio
(в том числе на CI под Linux через `Microsoft.NETFramework.ReferenceAssemblies`).

## Бизнес-логика (Phase 1)

### Архив
- `ArchiveService.CreateRequest(...)` создаёт запрос и выставляет `Deadline = CreationDate + 30 дней`.
- `ArchiveRequest.CanCompleteRequest()` возвращает `true` только при наличии обеих скан-копий (`HasPassportScan && HasWorkBookScan`).
- `ArchiveService.CompleteRequest(...)` бросает `InvalidOperationException`, если предварительные условия не соблюдены.

### Автопарк
- `FleetService.BookVehicle(vehicle, start, end, existingTrips?)` создаёт путевой лист.
- Бросает `VehicleBookingException`, если:
  - ТС в статусе `Maintenance`;
  - интервал пересекается с уже существующей поездкой (`Allen-overlap`);
  - `end <= start`.

### Дашборд
- `DashboardService.CountOverdue(docs, now)` — документы с `Deadline < now` и `Status ∉ {Completed, Cancelled}`.
- `DashboardService.CountDueSoon(docs, now, daysThreshold=3)` — активные документы с дедлайном в ближайшие N суток.

### UI / MVVM
- `MainViewModel` держит `ObservableCollection<NavigationItem>` и свойство `CurrentViewModel`.
- Сайдбар: «Дашборд / Канцелярия / Архив / IT-служба / Автопарк», каждая кнопка через `RelayCommand` переключает `SelectedNavigationItem`.
- `OverdueRowColorConverter` — `IValueConverter`, возвращающий `Red` / `Yellow` / `Transparent` в зависимости от `Document.Deadline` и `Status`.

## Быстрый старт

### Сборка и тесты

```bash
dotnet restore AhuErp.sln
dotnet build   AhuErp.sln -c Debug
dotnet test    AhuErp.sln -c Debug
dotnet format  AhuErp.sln --verify-no-changes --exclude src/AhuErp.Core/Migrations
```

Ожидаемый результат: **0 errors, 0 warnings, 21/21 passed**.

### Запуск WPF приложения

WPF-приложение рассчитано на Windows (net48). На Windows-машине достаточно:

```powershell
dotnet run --project src\AhuErp.UI\AhuErp.UI.csproj
```

## Применение EF6 миграции к SQL Server

Миграция `20260423121238_InitialCreate` разворачивает полную схему первой фазы.

### Вариант 1. Через `Update-Database` в Package Manager Console (Visual Studio)

1. Убедитесь, что `App.config` в `AhuErp.UI` (или свой config под ваш стенд) содержит connection string `AhuErpDb`. По умолчанию задано:
   ```
   Server=(localdb)\MSSQLLocalDB;Database=AhuErpDb;Integrated Security=true
   ```
2. В PMC выберите Default project = `AhuErp.Core`, StartUp project = `AhuErp.UI`.
3. Выполните:
   ```powershell
   Update-Database -Verbose
   ```

### Вариант 2. Через `migrate.exe` (без Visual Studio)

`migrate.exe` поставляется в NuGet-пакете `EntityFramework` в папке `tools`. После `dotnet build`:

```powershell
# Windows
cp $env:USERPROFILE\.nuget\packages\entityframework\6.4.4\tools\migrate.exe `
   src\AhuErp.Core\bin\Debug\
cd src\AhuErp.Core\bin\Debug
.\migrate.exe AhuErp.Core.dll /connectionStringName="AhuErpDb" /startUpConfigurationFile="..\..\..\AhuErp.UI\App.config" /verbose
```

### Вариант 3. Сгенерировать идемпотентный T-SQL скрипт

```powershell
Update-Database -Script -SourceMigration $InitialDatabase -TargetMigration InitialCreate -Verbose
```

Полученный `.sql` можно применить через `sqlcmd`, SSMS или любой CI/CD-пайплайн.

## Регенерация миграции в Linux / CI

Вспомогательный проект `tools/MigrationGenerator` позволяет скаффолдить EF6
миграции в среде без Visual Studio (в том числе в Linux через `mono`):

```bash
dotnet build tools/MigrationGenerator/MigrationGenerator.csproj
mono tools/MigrationGenerator/bin/Debug/MigrationGenerator.exe \
     src/AhuErp.Core/Migrations InitialCreate
```

## Стек

- .NET Framework 4.8 (SDK-style `.csproj`)
- WPF + MVVM (`CommunityToolkit.Mvvm` 8.3)
- Entity Framework 6.4.4 (Code-First + Migrations)
- xUnit 2.9
- Ref. assemblies: `Microsoft.NETFramework.ReferenceAssemblies 1.0.3`

## Roadmap (следующие фазы)

- Phase 2: CRUD экранов «Канцелярия» и «Архив», привязка к `AhuDbContext` через DI-контейнер.
- Phase 3: Подсистема «Автопарк» с календарным представлением и бронированиями.
- Phase 4: IT-service (Help Desk), импорт/экспорт документов.
- Phase 5: Ролевой доступ, аудит, отчётность.
