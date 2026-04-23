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

## Phase 2 — DI, Authentication & Office/Archive CRUD

- Добавлен DI-контейнер `Microsoft.Extensions.DependencyInjection` в `App.xaml.cs`, регистрирующий сервисы (`IAuthService`, `IPasswordHasher`, репозитории) и все ViewModel-и.
- `EmployeeRole` (Admin / Manager / Archivist / TechSupport / WarehouseManager) и `PasswordHash` добавлены к `Employee`. Миграция `AddEmployeeAuth` (`20260423125626`) добавляет соответствующие колонки.
- `IAuthService`/`AuthService` + PBKDF2-`Pbkdf2PasswordHasher` с константным сравнением. `LoginWindow` показывается первым при старте приложения; `MainWindow` открывается только после успешной аутентификации.
- RBAC: `RolePolicy` — декларативная таблица «роль → доступные модули»; `MainViewModel` фильтрует `NavigationItems` по текущему пользователю, `BooleanToVisibilityConverter` скрывает недоступные пункты меню.
- CRUD экраны «Канцелярия» (Incoming/Internal документы, `OfficeView`) и «Архив» (`ArchiveRequest` со скан-чекбоксами и действием «Завершить», `ArchiveView`) — работают поверх `IDocumentRepository` (in-memory на Phase 2, EF6 на Phase 3+).
- Демо-пользователи (пароль `password`): «Иванов Иван Иванович» (Admin), «Петров Пётр Петрович» (Manager), «Сидорова Анна Сергеевна» (Archivist), «Кузнецов Алексей Викторович» (TechSupport), «Орлова Мария Николаевна» (WarehouseManager).
- Тесты: **+38** — `AuthServiceTests`, `PasswordHasherTests`, `RolePolicyTests`, `InMemoryDocumentRepositoryTests`. Итого 59 зелёных.

## Phase 3 — Warehouse / ТМЦ + IT-Service (Help Desk)

- Модели: `InventoryItem` (Id, Name, `InventoryCategory`, TotalQuantity), `InventoryTransaction` (InventoryItemId, nullable `DocumentId`, QuantityChanged ±, TransactionDate, InitiatorId), `ItTicket` (наследник `Document` через TPH-дискриминатор — `AffectedEquipment`, `ResolutionNotes`).
- EF6 миграция `20260423131841_AddInventoryAndItTicket`: две новые таблицы + FK `InventoryTransactions.DocumentId → Documents`, `InitiatorId → Employees`, колонки `AffectedEquipment`/`ResolutionNotes` на `Documents` для TPH-подтипа `ItTicket`.
- `IInventoryService` / `InventoryService.ProcessTransaction(itemId, quantityChange, documentId?, userId)` — атомарно обновляет `TotalQuantity` и записывает движение. Правила: `quantityChange != 0`, списание требует `documentId`, при этом `TotalQuantity + quantityChange >= 0` (иначе `InvalidOperationException`).
- UI: `WarehouseView` — грид остатков + панель прихода/расхода (расход обязательно привязан к документу из `IDocumentRepository.ListInventoryEligibleDocuments()` — внутренние распоряжения + IT-заявки) + лента последних 20 движений.
- UI: `ItServiceView` — CRUD `ItTicket`; при закрытии заявки можно опционально списать расходник со склада — списание проходит через `IInventoryService` с `DocumentId = ticket.Id`, т.е. движение ТМЦ всегда связано с документом (IT-заявкой или приказом).
- Тесты: **+9** юнит-тестов (`InventoryServiceTests`): приход / расход / запрет овердрафта / обязательность документа при списании / нулевой/невалидный инициатор / отсутствующая позиция / граничный нулевой остаток / трассировка `DocumentId` по нескольким движениям. Итого **68/68**.

### Как реализована связка «движение ТМЦ → документ-основание»

```
InventoryTransaction.DocumentId? ──(FK, ON DELETE NO ACTION)──► Documents.Id
                                                                   │
                                                                   ├─ Document         (Incoming / Internal)
                                                                   ├─ ArchiveRequest   (TPH)
                                                                   └─ ItTicket         (TPH ← Phase 3)
```

Любое списание через `InventoryService.ProcessTransaction(..., documentId: X, ...)`:
- валидирует, что `X != null` и позиция имеет достаточный остаток,
- уменьшает `InventoryItem.TotalQuantity` на абсолютную величину,
- добавляет запись `InventoryTransaction { QuantityChanged < 0, DocumentId = X, InitiatorId = currentUser.Id, TransactionDate = now }`.

При закрытии `ItTicket` в UI `ItServiceViewModel.Resolve()` автоматически передаёт `documentId: SelectedTicket.Id`, поэтому любое списание из Help Desk прослеживается до конкретной заявки.

## Roadmap (следующие фазы)

- Phase 4: «Автопарк» с календарным представлением и бронированиями поверх `FleetService`.
- Phase 5: Дашборд-аналитика (LiveCharts), экспорт в Excel/Word, аудит и отчётность.
