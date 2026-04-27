/* ============================================================================
 * AhuErp — наполнение демо-БД «живой» рабочей средой.
 *
 * Назначение:
 *   После накатки `scripts/create-db.sql` (Phase 1–7) и EF6-миграций Phase 8–12
 *   (`Update-Database` или `dotnet ef database update`) выполнить этот скрипт,
 *   чтобы в WPF-приложении было ощущение, что в учреждении кипит работа:
 *   несколько отделов с руководителями, активные задачи (в т.ч. с горящим
 *   сроком и просроченные), документы в разных статусах, переписка по
 *   входящим/исходящим, ИТО-тикеты, движение ТМЦ, путевые листы, подписи
 *   ПЭП/КЭП (включая один заблокированный КЭП документ), уведомления
 *   (часть прочитана, часть нет), активное замещение, полнотекстовый индекс
 *   с реальным содержимым нескольких вложений, сохранённые поиски и
 *   непрерывная цепочка аудит-журнала.
 *
 * Запуск:
 *   1. SSMS / Azure Data Studio → подключиться к серверу с БД AhuErpDb.
 *   2. File → Open → seed-demo-life.sql → F5.
 *   3. Скрипт идемпотентен: повторный запуск увидит существующих сотрудников
 *      и завершится `RAISERROR(N'Already seeded', 0, 1) WITH NOWAIT`.
 *
 * Учётные записи (пароль везде `password`, PBKDF2-SHA256, 100k итераций):
 *   admin / sterlikov / dorofeev / burdina / zaychenko / volkov / petrova
 *
 * Хэш «password» зафиксирован в `tests/AhuErp.Tests/SeedHashVerify.cs` —
 * если при апгрейде Pbkdf2PasswordHasher изменится формат хэша, тест
 * упадёт первым, и эту строку нужно будет пересгенерировать через
 * `Pbkdf2PasswordHasher.Hash("password")`.
 *
 * Зависимости от приложения:
 *   - Файлы вложений физически НЕ создаются (нужны только метаданные в БД +
 *     заранее извлечённый текст в AttachmentTextIndices). Если хочется
 *     открыть файл из UI «Скачать вложение», создайте файлы вручную в
 *     каталоге, который указан в `AppServices.cs → IFileStorageService`.
 *   - Hash-цепочка AuditLogs (Hash / PreviousHash) вычисляется приложением
 *     через `IAuditService.Append`. В seed обе колонки оставлены NULL —
 *     при следующей реальной операции пересчёт пойдёт «с нуля». Это
 *     безопасно и согласовано с поведением сервиса при пустой таблице.
 * ========================================================================== */

USE [AhuErpDb];
GO

SET NOCOUNT ON;
GO

/* ---------- 0. Идемпотентность: уже посеяно — выходим ----------------------- */
IF EXISTS (SELECT 1 FROM dbo.Employees WHERE FullName LIKE N'%Стерликов%')
BEGIN
    RAISERROR(N'AhuErp: demo-life seed already applied — пропускаем.', 0, 1) WITH NOWAIT;
    RETURN;
END

DECLARE @now           DATETIME = SYSUTCDATETIME();
DECLARE @today         DATETIME = CAST(GETDATE() AS DATE);
DECLARE @yearStart     DATETIME = DATEFROMPARTS(YEAR(GETDATE()), 1, 1);
DECLARE @pwHash        NVARCHAR(512) = N'100000.AQIDBAUGBwgJCgsMDQ4PEA==.7cyBZDaG9OlWsUaYsNJGCHei/cERxR/FFPfRr1R4A9M=';

/* ============================================================================
 * 1. ОТДЕЛЫ (Phase 11) — иерархия и руководители заполнятся ниже после
 *    вставки сотрудников. Здесь только создаём сами строки.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.Departments ON;
INSERT INTO dbo.Departments (Id, Name, ShortCode, IsActive) VALUES
    (1, N'МКУ АХУ Балашовского муниципального района',           N'АХУ',   1),
    (2, N'Администрация',                                         N'АДМ',   1),
    (3, N'Канцелярия',                                            N'КАН',   1),
    (4, N'Служба информационно-технического обеспечения',         N'СИТО',  1),
    (5, N'Архивный отдел',                                        N'АРХ',   1),
    (6, N'Склад и ТМЦ',                                           N'СКЛАД', 1),
    (7, N'Транспортная служба',                                   N'ТР',    1);
SET IDENTITY_INSERT dbo.Departments OFF;

/* ============================================================================
 * 2. СОТРУДНИКИ
 * ========================================================================== */
SET IDENTITY_INSERT dbo.Employees ON;
INSERT INTO dbo.Employees (Id, FullName, [Position], [Role], PasswordHash, Email, DepartmentId, IsActive, TerminatedAt) VALUES
    (1, N'Администратор информационной системы',                  N'Системный администратор АИС «АХУ»',           0, @pwHash, N'admin@ahu.local',       2, 1, NULL),
    (2, N'Иванова Ольга Викторовна',                              N'Заместитель директора',                       1, @pwHash, N'ivanova@ahu.local',     2, 1, NULL),
    (3, N'Петрова Анна Сергеевна',                                N'Делопроизводитель',                           1, @pwHash, N'petrova@ahu.local',     3, 1, NULL),
    (4, N'Стерликов Дмитрий Николаевич',                          N'Руководитель службы по ИТО',                  1, @pwHash, N'sterlikov@ahu.local',   4, 1, NULL),
    (5, N'Дорофеев Артём Валерьевич',                             N'Специалист по компьютерным сетям',            3, @pwHash, N'dorofeev@ahu.local',    4, 1, NULL),
    (6, N'Королёв Никита Александрович',                          N'Инженер 1 категории',                         3, @pwHash, N'korolev@ahu.local',     4, 1, NULL),
    (7, N'Бурдина Галина Николаевна',                             N'Начальник архивного отдела',                  2, @pwHash, N'burdina@ahu.local',     5, 1, NULL),
    (8, N'Сёмина Елена Владимировна',                             N'Архивист',                                    2, @pwHash, N'semina@ahu.local',      5, 1, NULL),
    (9, N'Зайченко Татьяна Александровна',                        N'Заведующая складом',                          4, @pwHash, N'zaychenko@ahu.local',   6, 1, NULL),
    (10, N'Волков Сергей Игоревич',                               N'Водитель',                                    4, @pwHash, N'volkov@ahu.local',      7, 1, NULL),
    (11, N'Сидоров Павел Иванович (уволен)',                      N'Бывший делопроизводитель',                    1, NULL,    N'sidorov@ahu.local',     3, 0, DATEADD(MONTH, -3, GETDATE()));
SET IDENTITY_INSERT dbo.Employees OFF;

/* ---------- 2a. Отделы → руководители (Phase 11 HeadEmployeeId) ------------ */
UPDATE dbo.Departments SET ParentDepartmentId = NULL, HeadEmployeeId = 1  WHERE Id = 1;
UPDATE dbo.Departments SET ParentDepartmentId = 1,    HeadEmployeeId = 2  WHERE Id = 2;
UPDATE dbo.Departments SET ParentDepartmentId = 1,    HeadEmployeeId = 3  WHERE Id = 3;
UPDATE dbo.Departments SET ParentDepartmentId = 1,    HeadEmployeeId = 4  WHERE Id = 4;
UPDATE dbo.Departments SET ParentDepartmentId = 1,    HeadEmployeeId = 7  WHERE Id = 5;
UPDATE dbo.Departments SET ParentDepartmentId = 1,    HeadEmployeeId = 9  WHERE Id = 6;
UPDATE dbo.Departments SET ParentDepartmentId = 1,    HeadEmployeeId = 10 WHERE Id = 7;

/* ============================================================================
 * 3. СПРАВОЧНИКИ ДОКУМЕНТООБОРОТА (Phase 7)
 * ========================================================================== */
SET IDENTITY_INSERT dbo.DocumentTypeRefs ON;
INSERT INTO dbo.DocumentTypeRefs (Id, Name, ShortCode, DefaultDirection, DefaultRetentionYears, RegistrationNumberTemplate, IsActive) VALUES
    (1, N'Письмо входящее',     N'ВХ',   1, 5,  N'ВХ-{YYYY}-{NNNNN}', 1),
    (2, N'Письмо исходящее',    N'ИСХ',  2, 5,  N'ИСХ-{YYYY}-{NNNNN}', 1),
    (3, N'Служебная записка',   N'СЗ',   0, 3,  N'СЗ-{YYYY}-{NNNNN}', 1),
    (4, N'Приказ',              N'ПРК',  0, 75, N'ПРК-{YYYY}-{NNN}', 1),
    (5, N'Распоряжение',        N'РСП',  0, 5,  N'РСП-{YYYY}-{NNN}', 1),
    (6, N'Договор',             N'ДОГ',  0, 75, N'ДОГ-{YYYY}-{NNN}', 1),
    (7, N'Заявка ИТО',          N'ИТО',  0, 3,  N'ИТО-{YYYY}-{NNNN}', 1),
    (8, N'Архивный запрос',     N'АЗ',   1, 5,  N'АЗ-{YYYY}-{NNNN}', 1);
SET IDENTITY_INSERT dbo.DocumentTypeRefs OFF;

SET IDENTITY_INSERT dbo.NomenclatureCases ON;
INSERT INTO dbo.NomenclatureCases (Id, [Index], Title, DepartmentId, RetentionPeriodYears, Article, [Year], IsActive) VALUES
    (1, N'01-01', N'Приказы по основной деятельности',            2, 75, N'19а',  YEAR(GETDATE()), 1),
    (2, N'02-01', N'Переписка с органами местного самоуправления', 3, 5,  N'33',   YEAR(GETDATE()), 1),
    (3, N'02-02', N'Служебные записки',                            3, 3,  N'88',   YEAR(GETDATE()), 1),
    (4, N'03-01', N'Заявки и тикеты службы ИТО',                   4, 3,  N'255',  YEAR(GETDATE()), 1),
    (5, N'04-01', N'Договоры на поставку канцелярских товаров',    6, 5,  N'436',  YEAR(GETDATE()), 1),
    (6, N'04-02', N'Путевые листы',                                7, 5,  N'553',  YEAR(GETDATE()), 1),
    (7, N'05-01', N'Архивные запросы граждан',                     5, 5,  N'166',  YEAR(GETDATE()), 1);
SET IDENTITY_INSERT dbo.NomenclatureCases OFF;

/* ============================================================================
 * 4. ДОКУМЕНТЫ — палитра состояний:
 *      - Зарегистрированные несколько недель назад (закрыты)
 *      - В работе (срок завтра / послезавтра)
 *      - С горящим сроком < 24ч (для теста DeadlineSoon)
 *      - Просроченные (для теста Overdue)
 *      - Черновики
 *      - На согласовании
 *      - С наложенной ПЭП и одна с КЭП (заблокирован)
 *
 *  Маппинг колонок:
 *    Type:   General=0, Office=1, Archive=2, It=3, Fleet=4,
 *            Incoming=5, Internal=6, ArchiveRequest=7
 *    Direction: Internal=0, Incoming=1, Outgoing=2
 *    AccessLevel (Document): Public=0, Internal=1, Confidential=2
 *    Status: New=0, InProgress=1, OnHold=2, Completed=3, Cancelled=4
 *    ApprovalRouteStatus: Draft=0, InProgress=1, Completed=2, Rejected=3, Cancelled=4
 * ========================================================================== */
SET IDENTITY_INSERT dbo.Documents ON;
INSERT INTO dbo.Documents (
    Id, [Type], Direction, AccessLevel, RegistrationNumber, RegistrationDate,
    DocumentTypeRefId, NomenclatureCaseId, AuthorId, Title, Summary, Correspondent,
    IncomingNumber, IncomingDate, CreationDate, Deadline, [Status], AssignedEmployeeId,
    BasisDocumentId, ApprovalStatus, HasPassportScan, HasWorkBookScan, ArchiveRequestKind,
    AffectedEquipment, ResolutionNotes, DocumentDiscriminator, IsLocked, CurrentVersionAttachmentId
) VALUES
    -- 1. Закрытое исходящее письмо (3 недели назад)
    (1, 1, 2, 0, N'ИСХ-2026-00012', DATEADD(DAY, -22, GETDATE()),
     2, 2, 3, N'Ответ на запрос Министерства финансов о бюджете 2026', N'Подготовлены пояснения по статье расходов 02-04.', N'Министерство финансов СО',
     NULL, NULL, DATEADD(DAY, -25, GETDATE()), DATEADD(DAY, -20, GETDATE()), 3, 2,
     NULL, 2, NULL, NULL, NULL, NULL, N'Подписано и отправлено почтой РФ', N'Document', 0, NULL),

    -- 2. Входящее письмо (на исполнении, срок завтра)
    (2, 1, 1, 0, N'ВХ-2026-00037', DATEADD(DAY, -5, GETDATE()),
     1, 2, 3, N'О предоставлении сведений о работе архивного отдела за 1 квартал', N'Запрашиваются количественные показатели обработки запросов.', N'Управление по делам архивов СО',
     N'04-12/345', DATEADD(DAY, -7, GETDATE()), DATEADD(DAY, -5, GETDATE()), DATEADD(DAY, 1, GETDATE()), 1, 7,
     NULL, 0, NULL, NULL, NULL, NULL, N'Бурдиной — подготовить ответ', N'Document', 0, NULL),

    -- 3. Внутренняя СЗ — на согласовании, скоро будет подписана и заблокирована (Шаг E2E из чек-листа)
    (3, 6, 0, 1, N'СЗ-2026-00018', DATEADD(DAY, -3, GETDATE()),
     3, 3, 5, N'О замене картриджей в принтерах Canon LBP6030 кабинета 305', N'Прошу заменить тонер-картриджи Canon 725 в количестве 4 шт. для бесперебойной работы кабинета.', NULL,
     NULL, NULL, DATEADD(DAY, -3, GETDATE()), DATEADD(DAY, 2, GETDATE()), 1, 9,
     NULL, 1, NULL, NULL, NULL, N'Принтеры Canon LBP6030 (3 шт.)', N'Прошу выдать со склада 4 шт.', N'Document', 0, NULL),

    -- 4. Приказ — подписан КЭП, заблокирован
    (4, 1, 0, 1, N'ПРК-2026-00007', DATEADD(DAY, -10, GETDATE()),
     4, 1, 1, N'Об утверждении графика отпусков на 2026 год', N'Утверждается график очередных отпусков сотрудников учреждения на 2026 год согласно приложению.', NULL,
     NULL, NULL, DATEADD(DAY, -12, GETDATE()), DATEADD(DAY, -8, GETDATE()), 3, 1,
     NULL, 2, NULL, NULL, NULL, NULL, N'К исполнению', N'Document', 1, NULL),

    -- 5. Тикет ИТО (просрочен — для теста Overdue)
    (5, 3, 0, 0, N'ИТО-2026-00091', DATEADD(DAY, -8, GETDATE()),
     7, 4, 3, N'Не работает Wi-Fi в кабинете 207', N'Сотрудники жалуются на нестабильную работу Wi-Fi после выходных.', NULL,
     NULL, NULL, DATEADD(DAY, -8, GETDATE()), DATEADD(DAY, -2, GETDATE()), 1, 5,
     NULL, 0, NULL, NULL, NULL, N'Точка доступа TP-Link EAP245 (каб. 207)', NULL, N'ItTicket', 0, NULL),

    -- 6. Заявка на ТМЦ — закрыта
    (6, 1, 0, 0, N'СЗ-2026-00015', DATEADD(DAY, -14, GETDATE()),
     3, 3, 5, N'О выдаче бумаги формата A4 на отдел ИТО (10 пачек)', N'Бумага требуется для печати квартальной отчётности.', NULL,
     NULL, NULL, DATEADD(DAY, -16, GETDATE()), DATEADD(DAY, -10, GETDATE()), 3, 5,
     NULL, 2, NULL, NULL, NULL, NULL, N'Выдано полностью', N'Document', 0, NULL),

    -- 7. Архивный запрос — на исполнении, срок < 24ч (для DeadlineSoon)
    (7, 7, 1, 0, N'АЗ-2026-00043', DATEADD(DAY, -6, GETDATE()),
     8, 7, 7, N'Запрос Иванова И.И. о подтверждении трудового стажа за 1995-1998 гг.', N'Гражданин просит выдать архивную справку о работе на муниципальном предприятии.', N'Иванов Иван Иванович',
     NULL, NULL, DATEADD(DAY, -6, GETDATE()), DATEADD(HOUR, 18, GETDATE()), 1, 8,
     NULL, 0, 0, 0, 1, NULL, NULL, N'ArchiveRequest', 0, NULL),

    -- 8. Договор — подписан ПЭП руководителем, не заблокирован (можно ещё допподписать КЭП)
    (8, 1, 0, 2, N'ДОГ-2026-00004', DATEADD(DAY, -2, GETDATE()),
     6, 5, 9, N'Договор поставки канцелярских товаров № 04-2026', N'Договор поставки бумаги, ручек, маркеров и прочих расходных материалов на 2 квартал 2026 г.', N'ООО «Канцоптторг»',
     NULL, NULL, DATEADD(DAY, -4, GETDATE()), DATEADD(DAY, 5, GETDATE()), 1, 9,
     NULL, 1, NULL, NULL, NULL, NULL, NULL, N'Document', 0, NULL),

    -- 9. Черновик СЗ — Дорофеев пишет, ещё не зарегистрировано
    (9, 6, 0, 0, NULL, NULL,
     3, 3, 5, N'О необходимости приобретения дополнительного коммутатора', N'Текущий коммутатор Cisco SG250-26 не справляется с нагрузкой в часы пик.', NULL,
     NULL, NULL, DATEADD(DAY, -1, GETDATE()), DATEADD(DAY, 7, GETDATE()), 0, 4,
     NULL, 0, NULL, NULL, NULL, NULL, NULL, N'Document', 0, NULL),

    -- 10. Письмо отправлено в ответ на №2 (basis)
    (10, 1, 2, 0, N'ИСХ-2026-00021', DATEADD(DAY, -1, GETDATE()),
     2, 2, 7, N'Ответ на запрос ВХ-2026-00037 (квартальный отчёт)', N'Подготовлена сводка по обработанным запросам граждан за 1 квартал 2026 года.', N'Управление по делам архивов СО',
     NULL, NULL, DATEADD(DAY, -2, GETDATE()), DATEADD(DAY, -1, GETDATE()), 3, 7,
     2, 2, NULL, NULL, NULL, NULL, NULL, N'Document', 0, NULL),

    -- 11. Распоряжение — на согласовании
    (11, 1, 0, 1, N'РСП-2026-00009', DATEADD(DAY, -1, GETDATE()),
     5, 1, 2, N'О проведении инвентаризации ТМЦ во 2 квартале', N'Назначить комиссию для проведения инвентаризации.', NULL,
     NULL, NULL, DATEADD(DAY, -2, GETDATE()), DATEADD(DAY, 14, GETDATE()), 1, 9,
     NULL, 1, NULL, NULL, NULL, NULL, NULL, N'Document', 0, NULL),

    -- 12. Внутренний документ Fleet — путевой лист
    (12, 4, 0, 0, N'СЗ-2026-00020', DATEADD(DAY, -1, GETDATE()),
     3, 6, 10, N'Заявка на выезд автомобиля Lada Largus 25.04.2026', N'Доставка корреспонденции в районную администрацию.', NULL,
     NULL, NULL, DATEADD(DAY, -1, GETDATE()), DATEADD(DAY, 0, GETDATE()), 3, 10,
     NULL, 2, NULL, NULL, NULL, NULL, NULL, N'Document', 0, NULL);
SET IDENTITY_INSERT dbo.Documents OFF;

/* ============================================================================
 * 5. ВЛОЖЕНИЯ (Phase 7) — несколько с реальным текстом для индексирования.
 *    StoragePath намеренно ссылается на путь, которого может не быть на диске —
 *    UI «Скачать вложение» в этом случае покажет ошибку. Для демо это не
 *    критично, потому что текст уже извлечён в AttachmentTextIndices.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.DocumentAttachments ON;
INSERT INTO dbo.DocumentAttachments (Id, DocumentId, AttachmentGroupId, FileName, StoragePath, VersionNumber, IsCurrentVersion, UploadedAt, UploadedById, Comment, Hash, FileType, SizeBytes) VALUES
    (1, 1, 1, N'ISH-2026-00012.docx',  N'demo-storage/ISH-2026-00012/v1_ISH-2026-00012.docx', 1, 1, DATEADD(DAY, -22, GETDATE()), 3, NULL, N'h-c1a1', 0, 18432),
    (2, 2, 2, N'VH-2026-00037-skan.pdf', N'demo-storage/VH-2026-00037/v1_VH-2026-00037-skan.pdf', 1, 1, DATEADD(DAY, -5, GETDATE()), 3, NULL, N'h-c2a2', 1, 220300),
    (3, 3, 3, N'SZ-cartridge.docx', N'demo-storage/SZ-2026-00018/v1_SZ-cartridge.docx', 1, 1, DATEADD(DAY, -3, GETDATE()), 5, NULL, N'h-c3a3', 0, 12200),
    (4, 4, 4, N'PRK-otpuska-2026.docx', N'demo-storage/PRK-2026-00007/v1_PRK-otpuska-2026.docx', 1, 1, DATEADD(DAY, -10, GETDATE()), 1, NULL, N'h-c4a4', 0, 28100),
    (5, 4, 5, N'PRK-otpuska-2026.docx.sig', N'demo-storage/PRK-2026-00007/v1_PRK-otpuska-2026.docx.sig', 1, 1, DATEADD(DAY, -8, GETDATE()), 1, N'Открепленная КЭП', N'h-c5a5', 2, 7700),
    (6, 6, 6, N'SZ-bumaga.txt', N'demo-storage/SZ-2026-00015/v1_SZ-bumaga.txt', 1, 1, DATEADD(DAY, -16, GETDATE()), 5, NULL, N'h-c6a6', 0, 480),
    (7, 8, 7, N'DOG-2026-00004-poso.docx', N'demo-storage/DOG-2026-00004/v1_DOG-2026-00004-poso.docx', 1, 1, DATEADD(DAY, -4, GETDATE()), 9, NULL, N'h-c7a7', 0, 84200),
    (8, 8, 7, N'DOG-2026-00004-poso.docx', N'demo-storage/DOG-2026-00004/v2_DOG-2026-00004-poso.docx', 2, 1, DATEADD(DAY, -2, GETDATE()), 9, N'Версия после правок юриста', N'h-c8a8', 0, 86400),
    (9, 10, 8, N'ISH-2026-00021.docx', N'demo-storage/ISH-2026-00021/v1_ISH-2026-00021.docx', 1, 1, DATEADD(DAY, -2, GETDATE()), 7, NULL, N'h-c9a9', 0, 16700);
SET IDENTITY_INSERT dbo.DocumentAttachments OFF;

/* Текущая версия для документов с несколькими версиями + lock на КЭП-документе */
UPDATE dbo.Documents SET CurrentVersionAttachmentId = 1 WHERE Id = 1;
UPDATE dbo.Documents SET CurrentVersionAttachmentId = 2 WHERE Id = 2;
UPDATE dbo.Documents SET CurrentVersionAttachmentId = 3 WHERE Id = 3;
UPDATE dbo.Documents SET CurrentVersionAttachmentId = 4 WHERE Id = 4;
UPDATE dbo.Documents SET CurrentVersionAttachmentId = 6 WHERE Id = 6;
UPDATE dbo.Documents SET CurrentVersionAttachmentId = 8 WHERE Id = 8;  -- v2
UPDATE dbo.Documents SET CurrentVersionAttachmentId = 9 WHERE Id = 10;
UPDATE dbo.DocumentAttachments SET IsCurrentVersion = 0 WHERE Id = 7;

/* ============================================================================
 * 6. РЕЗОЛЮЦИИ И ЗАДАЧИ (Phase 7)
 *    DocumentTaskStatus: New=0, InProgress=1, Completed=2, Cancelled=3 — но
 *    на схеме это просто INT. Используем 0=New, 1=InProgress, 2=Completed,
 *    3=Cancelled, согласно DocumentTask.cs.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.DocumentResolutions ON;
INSERT INTO dbo.DocumentResolutions (Id, DocumentId, AuthorId, Text, IssuedAt) VALUES
    (1, 2, 1, N'Бурдиной Г.Н. — подготовить ответ в срок до 28.04. Контролирует Иванова О.В.', DATEADD(DAY, -5, GETDATE())),
    (2, 3, 4, N'Зайченко Т.А. — выдать со склада 4 картриджа Canon 725. Дорофееву А.В. — установить.', DATEADD(DAY, -3, GETDATE())),
    (3, 7, 7, N'Сёминой Е.В. — поднять архивные дела БМР-1995-Л за 1995-1998 и подготовить справку.', DATEADD(DAY, -6, GETDATE()));
SET IDENTITY_INSERT dbo.DocumentResolutions OFF;

SET IDENTITY_INSERT dbo.DocumentTasks ON;
INSERT INTO dbo.DocumentTasks (Id, DocumentId, ResolutionId, ParentTaskId, AuthorId, ExecutorId, ControllerId, CoExecutors, Description, CreatedAt, Deadline, [Status], CompletedAt, ReportText, IsCritical) VALUES
    -- Просроченная задача (DocumentTaskStatus.Overdue=5 — для теста Overdue/timer)
    (1, 5, NULL, NULL, 1, 5, 4, NULL, N'Восстановить работу Wi-Fi в кабинете 207, проверить точку доступа.',
     DATEADD(DAY, -8, GETDATE()), DATEADD(DAY, -2, GETDATE()), 5, NULL, NULL, 1),
    -- Горящий срок < 24ч (для DeadlineSoon)
    (2, 7, 3, NULL, 7, 8, 7, NULL, N'Поднять архивные дела за 1995-1998 и подготовить справку для гражданина Иванова И.И.',
     DATEADD(DAY, -6, GETDATE()), DATEADD(HOUR, 18, GETDATE()), 1, NULL, NULL, 0),
    -- Задача в работе с разумным сроком — назначена Стерликову (см. замещение!)
    (3, 3, 2, NULL, 4, 4, 1, NULL, N'Принять служебную записку, назначить исполнителя на склад/ИТО.',
     DATEADD(DAY, -3, GETDATE()), DATEADD(DAY, 2, GETDATE()), 1, NULL, NULL, 0),
    -- Закрытая задача (DocumentTaskStatus.Completed=3)
    (4, 6, NULL, NULL, 5, 9, 4, NULL, N'Выдать со склада 10 пачек бумаги A4.',
     DATEADD(DAY, -16, GETDATE()), DATEADD(DAY, -10, GETDATE()), 3, DATEADD(DAY, -11, GETDATE()), N'Выдано полностью, расписка в журнале.', 0),
    -- Закрытая задача
    (5, 1, NULL, 1, 3, 3, 1, NULL, N'Подготовить и направить ответ в Минфин.',
     DATEADD(DAY, -25, GETDATE()), DATEADD(DAY, -22, GETDATE()), 3, DATEADD(DAY, -22, GETDATE()), N'Ответ направлен почтой РФ, трек 80012345.', 1),
    -- Подзадача под задачу 3
    (6, 3, 2, 3, 4, 9, 4, NULL, N'Выдать со склада 4 картриджа Canon 725.',
     DATEADD(DAY, -3, GETDATE()), DATEADD(DAY, 1, GETDATE()), 0, NULL, NULL, 0);
SET IDENTITY_INSERT dbo.DocumentTasks OFF;

/* ============================================================================
 * 7. МАРШРУТЫ СОГЛАСОВАНИЯ (Phase 7)
 * ========================================================================== */
SET IDENTITY_INSERT dbo.ApprovalRouteTemplates ON;
INSERT INTO dbo.ApprovalRouteTemplates (Id, Name, [Description], DocumentTypeRefId, IsActive) VALUES
    (1, N'Стандартное согласование внутренних СЗ', N'Руководитель отдела → Заместитель директора → Директор', 3, 1),
    (2, N'Согласование договоров', N'Юрист → Бухгалтер → Зам. директора → Директор', 6, 1);
SET IDENTITY_INSERT dbo.ApprovalRouteTemplates OFF;

SET IDENTITY_INSERT dbo.ApprovalStages ON;
INSERT INTO dbo.ApprovalStages (Id, RouteTemplateId, [Order], IsParallel, ApproverEmployeeId, ApproverRole, [Description]) VALUES
    (1, 1, 1, 0, 4, NULL, N'Руководитель ИТО'),
    (2, 1, 2, 0, 2, NULL, N'Заместитель директора'),
    (3, 1, 3, 0, 1, NULL, N'Директор'),
    (4, 2, 1, 0, 3, NULL, N'Делопроизводитель / Юрист'),
    (5, 2, 2, 0, 9, NULL, N'Зав. складом'),
    (6, 2, 3, 0, 2, NULL, N'Заместитель директора'),
    (7, 2, 4, 0, 1, NULL, N'Директор');
SET IDENTITY_INSERT dbo.ApprovalStages OFF;

/* Активный маршрут на документ #3 (СЗ о картриджах) — первая стадия согласована.
 * ApprovalDecision: Pending=0, Approved=1, Rejected=2, Comments=3 */
SET IDENTITY_INSERT dbo.DocumentApprovals ON;
INSERT INTO dbo.DocumentApprovals (Id, DocumentId, StageId, [Order], IsParallel, ApproverId, Decision, Comment, DecisionDate) VALUES
    (1, 3,  1,    1, 0, 4, 1, N'Согласовано без замечаний.', DATEADD(DAY, -2, GETDATE())),
    (2, 3,  2,    2, 0, 2, 0, NULL,                          NULL),
    (3, 11, NULL, 1, 0, 2, 0, N'На рассмотрении.',           NULL);
SET IDENTITY_INSERT dbo.DocumentApprovals OFF;

/* ============================================================================
 * 8. ВЛОЖЕНИЯ → НОМЕНКЛАТУРА (Phase 7)
 * ========================================================================== */
SET IDENTITY_INSERT dbo.DocumentCaseLinks ON;
INSERT INTO dbo.DocumentCaseLinks (Id, DocumentId, NomenclatureCaseId, LinkedAt, LinkedById, IsPrimary) VALUES
    (1, 1, 2, DATEADD(DAY, -22, GETDATE()), 3, 1),
    (2, 2, 2, DATEADD(DAY, -5, GETDATE()),  3, 1),
    (3, 3, 3, DATEADD(DAY, -3, GETDATE()),  5, 1),
    (4, 4, 1, DATEADD(DAY, -10, GETDATE()), 1, 1),
    (5, 5, 4, DATEADD(DAY, -8, GETDATE()),  3, 1),
    (6, 6, 3, DATEADD(DAY, -16, GETDATE()), 5, 1),
    (7, 7, 7, DATEADD(DAY, -6, GETDATE()),  7, 1),
    (8, 8, 5, DATEADD(DAY, -2, GETDATE()),  9, 1),
    (9, 10, 2, DATEADD(DAY, -1, GETDATE()), 7, 1);
SET IDENTITY_INSERT dbo.DocumentCaseLinks OFF;

/* ============================================================================
 * 9. ТМЦ И СКЛАД
 * ========================================================================== */
SET IDENTITY_INSERT dbo.InventoryItems ON;
INSERT INTO dbo.InventoryItems (Id, [Name], Category, TotalQuantity) VALUES
    (1, N'Бумага A4 «Снегурочка», 500 листов',  0,  84),
    (2, N'Картридж Canon 725 (для LBP6030)',   1,  17),
    (3, N'Тонер HP CF283A',                     1,   8),
    (4, N'Ручка шариковая синяя BIC',           0, 240),
    (5, N'Папка-регистратор A4 70мм',           0,  46),
    (6, N'Жидкое мыло для рук, 5л',             2,   9),
    (7, N'Перчатки одноразовые, упак. 100 шт.', 2,  22);
SET IDENTITY_INSERT dbo.InventoryItems OFF;

SET IDENTITY_INSERT dbo.InventoryTransactions ON;
INSERT INTO dbo.InventoryTransactions (Id, InventoryItemId, DocumentId, QuantityChanged, TransactionDate, InitiatorId, BasisDocumentId) VALUES
    -- Приходы
    (1, 1, NULL,  100, DATEADD(DAY, -25, GETDATE()), 9, NULL),
    (2, 2, NULL,   24, DATEADD(DAY, -20, GETDATE()), 9, NULL),
    (3, 3, NULL,   12, DATEADD(DAY, -20, GETDATE()), 9, NULL),
    (4, 4, NULL,  300, DATEADD(DAY, -25, GETDATE()), 9, NULL),
    (5, 5, NULL,   50, DATEADD(DAY, -25, GETDATE()), 9, NULL),
    -- Расходы
    (6, 1,  6,  -10, DATEADD(DAY, -10, GETDATE()), 9, 6),
    (7, 1,  6,   -3, DATEADD(DAY, -8, GETDATE()),  9, 6),
    (8, 4,  6,  -50, DATEADD(DAY, -7, GETDATE()),  9, 6),
    (9, 5,  6,   -4, DATEADD(DAY, -7, GETDATE()),  9, 6),
    (10, 6, NULL,  -1, DATEADD(DAY, -3, GETDATE()),  9, NULL),
    (11, 2, NULL,  -7, DATEADD(DAY, -2, GETDATE()),  9, NULL),
    (12, 3, NULL,  -4, DATEADD(DAY, -1, GETDATE()),  9, NULL);
SET IDENTITY_INSERT dbo.InventoryTransactions OFF;

/* ============================================================================
 * 10. ТРАНСПОРТ
 * ========================================================================== */
SET IDENTITY_INSERT dbo.Vehicles ON;
INSERT INTO dbo.Vehicles (Id, Model, LicensePlate, CurrentStatus) VALUES
    (1, N'Lada Largus',   N'А123БВ 64',  0),
    (2, N'ГАЗель NEXT',   N'В777ТТ 64',  1),
    (3, N'УАЗ Патриот',   N'Е111КХ 64',  2),
    (4, N'Renault Logan', N'К234АА 64',  0);
SET IDENTITY_INSERT dbo.Vehicles OFF;

SET IDENTITY_INSERT dbo.VehicleTrips ON;
INSERT INTO dbo.VehicleTrips (Id, VehicleId, StartDate, EndDate, DocumentId, DriverName, BasisDocumentId) VALUES
    (1, 1, DATEADD(DAY, -10, GETDATE()), DATEADD(DAY, -10, GETDATE()) + CAST('06:00:00' AS DATETIME), NULL, N'Волков С.И.', NULL),
    (2, 2, DATEADD(DAY, -5,  GETDATE()), DATEADD(DAY, -5,  GETDATE()) + CAST('08:00:00' AS DATETIME), NULL, N'Волков С.И.', NULL),
    (3, 1, DATEADD(DAY, -1,  GETDATE()), DATEADD(DAY, -1,  GETDATE()) + CAST('04:30:00' AS DATETIME), 12,   N'Волков С.И.', 12),
    (4, 4, DATEADD(HOUR, -3, GETDATE()), DATEADD(HOUR,  5, GETDATE()),                                NULL, N'Волков С.И.', NULL);
SET IDENTITY_INSERT dbo.VehicleTrips OFF;

/* ============================================================================
 * 11. PHASE 11 — ЗАМЕЩЕНИЯ И ДЕЛЕГИРОВАНИЕ
 *    Стерликов в отпуске 7 дней с сегодня — задачи перенаправляются
 *    Дорофееву (см. также DocumentTasks #3 → подзадачу #6 Зайченко).
 * ========================================================================== */
SET IDENTITY_INSERT dbo.Substitutions ON;
INSERT INTO dbo.Substitutions (Id, OriginalEmployeeId, SubstituteEmployeeId, [From], [To], Scope, Reason, IsActive, CreatedById) VALUES
    -- Активное замещение
    (1, 4, 5, DATEADD(DAY, -1, GETDATE()), DATEADD(DAY, 6, GETDATE()), 2, N'Очередной отпуск', 1, 1),
    -- Историческое (закрытое) замещение
    (2, 7, 8, DATEADD(DAY, -30, GETDATE()), DATEADD(DAY, -23, GETDATE()), 0, N'Больничный', 0, 1);
SET IDENTITY_INSERT dbo.Substitutions OFF;

SET IDENTITY_INSERT dbo.TaskDelegations ON;
INSERT INTO dbo.TaskDelegations (Id, TaskId, FromEmployeeId, ToEmployeeId, DelegatedAt, Comment) VALUES
    (1, 3, 4, 5, DATEADD(DAY, -3, GETDATE()), N'Авто-делегирование по замещению Стерликов → Дорофеев');
SET IDENTITY_INSERT dbo.TaskDelegations OFF;

/* ============================================================================
 * 12. PHASE 9 — УВЕДОМЛЕНИЯ
 *    Каналы: InApp=0, Email=1, Both=2.
 *    Виды (NotificationKind): TaskAssigned=0, TaskDeadlineSoon=1, TaskOverdue=2,
 *    ApprovalRequired=3, ApprovalDecided=4, ResolutionAdded=5,
 *    DocumentRegistered=6, DocumentSigned=7, System=99.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.Notifications ON;
INSERT INTO dbo.Notifications (Id, RecipientId, Kind, Title, Body, RelatedDocumentId, RelatedTaskId, RelatedApprovalId, CreatedAt, ReadAt, Channel, SentToEmailAt) VALUES
    -- admin (id=1) — 3 непрочитанных
    (1, 1, 6, N'Документ зарегистрирован: ВХ-2026-00037', N'Поступил входящий запрос от Управления по делам архивов СО.',
        2,    NULL, NULL, DATEADD(DAY, -5, GETDATE()),    NULL,                          0, NULL),
    (2, 1, 7, N'Документ подписан КЭП: ПРК-2026-00007',   N'Приказ об утверждении графика отпусков подписан и заблокирован.',
        4,    NULL, NULL, DATEADD(DAY, -8, GETDATE()),    NULL,                          0, NULL),
    (3, 1, 99, N'Запущена индексация полнотекстового поиска', N'Перестроено 7 записей AttachmentTextIndices.',
        NULL, NULL, NULL, DATEADD(MINUTE, -15, GETDATE()), NULL,                          0, NULL),
    (4, 1, 4, N'Решение по согласованию: ДОГ-2026-00004', N'Иванова О.В. согласовала договор поставки.',
        8,    NULL, NULL, DATEADD(DAY, -1, GETDATE()),    DATEADD(DAY, -1, GETDATE()),   2, DATEADD(DAY, -1, GETDATE())),
    -- Стерликов (id=4) — 1 непрочитанное (но он в отпуске; всё равно копится)
    (5, 4, 0, N'Назначено поручение по СЗ-2026-00018',    N'Принять служебную записку и распределить.',
        3, 3, NULL, DATEADD(DAY, -3, GETDATE()),    NULL,                          2, DATEADD(DAY, -3, GETDATE())),
    -- Дорофеев (id=5) — авто-уведомление по делегированию + DeadlineSoon на свою задачу
    (6, 5, 0, N'Поручение по СЗ-2026-00018 (по замещению)', N'Стерликов Д.Н. в отпуске — поручение пришло вам.',
        3, 3, NULL, DATEADD(DAY, -3, GETDATE()),    NULL,                          0, NULL),
    (7, 5, 2, N'Просрочена задача: «Не работает Wi-Fi в кабинете 207»', N'Срок: 23.04.2026, текущая дата: 26.04.2026.',
        5, 1, NULL, DATEADD(DAY, -1, GETDATE()),    NULL,                          2, DATEADD(DAY, -1, GETDATE())),
    -- Бурдина (id=7) — 1 прочитанное + 1 непрочитанное
    (8, 7, 0, N'Назначено поручение по ВХ-2026-00037',    N'Подготовить ответ в Управление по делам архивов СО.',
        2, NULL, NULL, DATEADD(DAY, -5, GETDATE()),  DATEADD(DAY, -4, GETDATE()),   2, DATEADD(DAY, -5, GETDATE())),
    (9, 7, 1, N'Скоро срок: ВХ-2026-00037',                N'Срок исполнения наступит через 24 часа.',
        2, NULL, NULL, DATEADD(HOUR, -2, GETDATE()), NULL,                          0, NULL),
    -- Сёмина (id=8) — DeadlineSoon на задачу 2
    (10, 8, 1, N'Скоро срок задачи: архивный запрос Иванова И.И.', N'Срок: ' + CONVERT(NVARCHAR(20), DATEADD(HOUR, 18, GETDATE()), 120),
        7, 2, NULL, DATEADD(MINUTE, -10, GETDATE()), NULL,                          2, DATEADD(MINUTE, -10, GETDATE())),
    -- Зайченко (id=9) — поручение по подзадаче 6 + согласование договора
    (11, 9, 0, N'Поручение: выдать 4 картриджа Canon 725', N'Получатель — Дорофеев А.В.',
        3, 6, NULL, DATEADD(DAY, -3, GETDATE()),    DATEADD(DAY, -2, GETDATE()),   0, NULL),
    (12, 9, 3, N'Запрос на согласование: ДОГ-2026-00004', N'Договор поставки — стадия «Зав. складом».',
        8, NULL, 2, DATEADD(DAY, -2, GETDATE()),    NULL,                          2, DATEADD(DAY, -2, GETDATE())),
    -- Иванова (id=2) — Approval pending
    (13, 2, 3, N'На согласование: РСП-2026-00009',         N'Распоряжение об инвентаризации ТМЦ.',
        11, NULL, 3, DATEADD(DAY, -1, GETDATE()),   NULL,                          2, DATEADD(DAY, -1, GETDATE())),
    (14, 2, 3, N'На согласование: СЗ-2026-00018',          N'Стадия «Заместитель директора».',
        3, NULL, 2, DATEADD(DAY, -2, GETDATE()),   NULL,                          0, NULL);
SET IDENTITY_INSERT dbo.Notifications OFF;

SET IDENTITY_INSERT dbo.NotificationPreferences ON;
INSERT INTO dbo.NotificationPreferences (Id, EmployeeId, Kind, Channel, IsEnabled, EmailOverride) VALUES
    (1, 1,  0, 0, 1, NULL),     -- admin: TaskAssigned only InApp
    (2, 1,  3, 2, 1, NULL),     -- admin: ApprovalRequired Both
    (3, 1, 99, 0, 0, NULL),     -- admin: System notifications выключены
    (4, 4,  0, 2, 1, N'sterlikov.alt@mail.ru'),   -- Стерликов: переопределение email
    (5, 5,  2, 2, 1, NULL),     -- Дорофеев: TaskOverdue Both
    (6, 7,  1, 2, 1, NULL),     -- Бурдина: DeadlineSoon Both
    (7, 9,  3, 2, 1, NULL),     -- Зайченко: ApprovalRequired Both
    (8, 2,  3, 2, 1, NULL);     -- Иванова: ApprovalRequired Both
SET IDENTITY_INSERT dbo.NotificationPreferences OFF;

/* ============================================================================
 * 13. PHASE 8 — ПОДПИСИ И БЛОКИРОВКА
 *    SignatureKind: Simple=0, Enhanced=1, Qualified=2
 *    Documents.IsLocked = 1 для документа #4 (КЭП-приказ).
 * ========================================================================== */
SET IDENTITY_INSERT dbo.DocumentSignatures ON;
INSERT INTO dbo.DocumentSignatures (Id, DocumentId, AttachmentId, SignerId, Kind, SignedAt, SignedHash, SignatureBlobBase64, CertificateThumbprint, CertificateSubject, CertificateNotAfter, Reason, IsRevoked, RevokedAt) VALUES
    -- Документ #1 (закрытое исходящее): ПЭП директора — историческая
    (1, 1, NULL, 1, 0, DATEADD(DAY, -22, GETDATE()), N'a1b2c3d4e5f607080910111213141516', N'demo-base64-bytes==', NULL, NULL, NULL, N'Согласовано', 0, NULL),
    -- Документ #4 (приказ): сначала ПЭП Ивановой, потом КЭП Иванова → блокировка
    (2, 4, NULL, 2, 0, DATEADD(DAY, -10, GETDATE()), N'b2c3d4e5f607080910111213141516a1', N'demo-base64-bytes==', NULL, NULL, NULL, N'Согласовано (зам. директора)', 0, NULL),
    (3, 4, 5,    1, 2, DATEADD(DAY,  -8, GETDATE()), N'c3d4e5f607080910111213141516a1b2', N'demo-cades-base64==', N'DEADBEEFDEADBEEFDEADBEEFDEADBEEFDEAD0001', N'CN=Администратор МКУ АХУ БМР, OU=Руководство, O=МКУ АХУ БМР, C=RU',
        DATEADD(YEAR, 1, GETDATE()), N'Утверждено директором', 0, NULL),
    -- Документ #6 (закрытая заявка): ПЭП — отозвана позже из-за загрузки новой версии
    (4, 6, NULL, 9, 0, DATEADD(DAY, -16, GETDATE()), N'd4e5f607080910111213141516a1b2c3', N'demo-base64-bytes==', NULL, NULL, NULL, N'Принято на склад',  1, DATEADD(DAY, -14, GETDATE())),
    -- Документ #8 (договор): ПЭП Зайченко (зав. складом, согласование 1 стадии)
    (5, 8, NULL, 9, 0, DATEADD(DAY, -2, GETDATE()),  N'e5f607080910111213141516a1b2c3d4', N'demo-base64-bytes==', NULL, NULL, NULL, N'Согласовано (зав. складом, 1-я стадия)', 0, NULL),
    -- Документ #10 (исх. ответ Бурдиной): ПЭП Бурдиной
    (6, 10, NULL, 7, 0, DATEADD(DAY, -1, GETDATE()), N'f607080910111213141516a1b2c3d4e5', N'demo-base64-bytes==', NULL, NULL, NULL, N'Согласовано', 0, NULL);
SET IDENTITY_INSERT dbo.DocumentSignatures OFF;

/* Документ #4 — заблокирован КЭП. Текущая версия = вложение 4 (.docx),
 * подпись приложена к открепленному 5 (.sig). */
UPDATE dbo.Documents SET IsLocked = 1 WHERE Id = 4;

/* ============================================================================
 * 14. PHASE 10 — ПОЛНОТЕКСТОВЫЙ ИНДЕКС И СОХРАНЁННЫЕ ПОИСКИ
 *    Реальный извлечённый текст для пары вложений — этого достаточно, чтобы
 *    «Поиск → Искать в текстах вложений» нашёл хотя бы что-то без ожидания
 *    тика DispatcherTimer.
 * ========================================================================== */
SET IDENTITY_INSERT dbo.AttachmentTextIndices ON;
INSERT INTO dbo.AttachmentTextIndices (Id, AttachmentId, DocumentId, ExtractedText, IndexedAt, SourceContentHash) VALUES
    (1, 1, 1, N'Уважаемые коллеги! Направляем пояснения по статье расходов 02-04 бюджета 2026 года. Расчёты выполнены в соответствии с Приказом Минфина № 209н. Сводная таблица приложена. С уважением, Петрова А.С.',
        DATEADD(DAY, -22, GETDATE()), N'h-c1a1'),
    (2, 2, 2, N'В целях контроля за работой архивных подразделений просим в срок до 28 апреля 2026 года направить в адрес Управления по делам архивов сведения по работе с обращениями граждан за 1 квартал 2026 года.',
        DATEADD(DAY, -5, GETDATE()),  N'h-c2a2'),
    (3, 3, 3, N'Прошу заменить тонер-картриджи Canon 725 в количестве 4 штук в принтерах Canon LBP6030, расположенных в кабинете 305 (служба ИТО). Картриджи израсходованы при печати квартальной отчётности.',
        DATEADD(DAY, -3, GETDATE()),  N'h-c3a3'),
    (4, 4, 4, N'Утвердить график очередных оплачиваемых отпусков сотрудников МКУ АХУ Балашовского муниципального района на 2026 год согласно приложению. Контроль исполнения возложить на отдел кадров.',
        DATEADD(DAY, -10, GETDATE()), N'h-c4a4'),
    (5, 6, 6, N'Прошу выдать на отдел службы информационно-технического обеспечения 10 пачек бумаги формата А4 для печати квартальной отчётности 2026 года. Бумага необходима срочно — текущий запас закончился.',
        DATEADD(DAY, -16, GETDATE()), N'h-c6a6'),
    (6, 8, 8, N'Договор поставки канцелярских товаров № 04-2026 заключён между МКУ АХУ Балашовского муниципального района и ООО «Канцоптторг». Предмет договора — поставка канцелярских товаров (бумага, ручки, маркеры, скрепки) на 2 квартал 2026 года. Цена договора — 124 500 рублей.',
        DATEADD(DAY, -2, GETDATE()),  N'h-c8a8'),
    (7, 9, 10, N'В ответ на ваш запрос ВХ-2026-00037 направляем сводку по обработанным архивным запросам граждан за 1 квартал 2026 года: всего 47 запросов, выдано 41 справка, отказано в 6 случаях.',
        DATEADD(DAY, -2, GETDATE()),  N'h-c9a9');
SET IDENTITY_INSERT dbo.AttachmentTextIndices OFF;

SET IDENTITY_INSERT dbo.SavedSearches ON;
INSERT INTO dbo.SavedSearches (Id, OwnerId, Name, FilterJson, IsShared, CreatedAt) VALUES
    (1, 1, N'Входящие письма за месяц',
        N'{"Direction":1,"PeriodFrom":"' + CONVERT(NVARCHAR(10), DATEADD(MONTH, -1, GETDATE()), 23) + N'","PeriodTo":"' + CONVERT(NVARCHAR(10), GETDATE(), 23) + N'"}',
        1, DATEADD(DAY, -10, GETDATE())),
    (2, 1, N'Просроченные задачи моего отдела',
        N'{"OnlyOverdue":true,"DepartmentId":2}',
        1, DATEADD(DAY, -5, GETDATE())),
    (3, 4, N'Мои подписанные договоры',
        N'{"DocumentTypeRefId":6,"AssignedEmployeeId":4,"OnlySigned":true}',
        0, DATEADD(DAY, -3, GETDATE())),
    (4, 7, N'Архивные запросы кв. 1 2026',
        N'{"DocumentTypeRefId":8,"PeriodFrom":"' + CONVERT(NVARCHAR(10), DATEADD(MONTH, -3, GETDATE()), 23) + N'"}',
        1, DATEADD(DAY, -7, GETDATE()));
SET IDENTITY_INSERT dbo.SavedSearches OFF;

/* ============================================================================
 * 15. ЖУРНАЛ АУДИТА (Phase 7)
 *    Hash / PreviousHash — NULL: пересчитает приложение при следующей операции.
 *    AuditActionType: см. AuditActionType.cs (Created=0, StatusChanged=10,
 *    Registered=11, AttachmentAdded=20, AttachmentVersioned=21,
 *    ResolutionIssued=30, TaskAssigned=31, TaskCompleted=32, TaskOverdue=33,
 *    TaskReassigned=34, ApprovalSent=40, ApprovalSigned=41,
 *    SignatureAdded=60, SignatureRevoked=61, DocumentLocked=62,
 *    NotificationSent=70, SubstitutionCreated=80, TaskDelegated=82,
 *    IndexRebuilt=85, ReportGenerated=86, UserLogin=90).
 * ========================================================================== */
INSERT INTO dbo.AuditLogs (Timestamp, UserId, ActionType, EntityType, EntityId, OldValues, NewValues, Details, Hash, PreviousHash) VALUES
    (DATEADD(DAY, -25, GETDATE()), 3, 0,  N'Document', 1,  NULL, NULL, N'Создан исходящий документ ИСХ-2026-00012', NULL, NULL),
    (DATEADD(DAY, -22, GETDATE()), 1, 11, N'Document', 1,  NULL, N'{"RegistrationNumber":"ИСХ-2026-00012"}', N'Документ зарегистрирован', NULL, NULL),
    (DATEADD(DAY, -22, GETDATE()), 1, 60, N'DocumentSignature', 1, NULL, N'{"Kind":"Simple"}', N'ПЭП директора', NULL, NULL),
    (DATEADD(DAY, -10, GETDATE()), 1, 0,  N'Document', 4,  NULL, NULL, N'Создан приказ ПРК-2026-00007', NULL, NULL),
    (DATEADD(DAY, -10, GETDATE()), 1, 11, N'Document', 4,  NULL, N'{"RegistrationNumber":"ПРК-2026-00007"}', N'Приказ зарегистрирован', NULL, NULL),
    (DATEADD(DAY, -10, GETDATE()), 2, 60, N'DocumentSignature', 2, NULL, N'{"Kind":"Simple"}', N'ПЭП заместителя директора', NULL, NULL),
    (DATEADD(DAY,  -8, GETDATE()), 1, 60, N'DocumentSignature', 3, NULL, N'{"Kind":"Qualified"}', N'КЭП директора', NULL, NULL),
    (DATEADD(DAY,  -8, GETDATE()), 1, 62, N'Document', 4,  NULL, N'{"IsLocked":true}', N'Документ заблокирован после КЭП', NULL, NULL),
    (DATEADD(DAY,  -8, GETDATE()), 3, 0,  N'Document', 5,  NULL, NULL, N'Создан тикет ИТО-2026-00091', NULL, NULL),
    (DATEADD(DAY,  -7, GETDATE()), 1, 31, N'DocumentTask', 1, NULL, N'{"ExecutorId":5}', N'Назначено поручение Дорофееву', NULL, NULL),
    (DATEADD(DAY,  -5, GETDATE()), 3, 11, N'Document', 2,  NULL, N'{"RegistrationNumber":"ВХ-2026-00037"}', N'Зарегистрировано входящее письмо', NULL, NULL),
    (DATEADD(DAY,  -5, GETDATE()), 1, 30, N'DocumentResolution', 1, NULL, N'{"AuthorId":1}', N'Резолюция директора', NULL, NULL),
    (DATEADD(DAY,  -5, GETDATE()), 1, 31, N'DocumentTask', NULL, NULL, NULL, N'Поручение Бурдиной по ВХ-2026-00037', NULL, NULL),
    (DATEADD(DAY,  -5, GETDATE()), 1, 70, N'Notification', 8, NULL, NULL, N'Уведомление Бурдиной отправлено по in-app + email', NULL, NULL),
    (DATEADD(DAY,  -3, GETDATE()), 5, 0,  N'Document', 3,  NULL, NULL, N'Черновик СЗ о картриджах', NULL, NULL),
    (DATEADD(DAY,  -3, GETDATE()), 5, 11, N'Document', 3,  NULL, N'{"RegistrationNumber":"СЗ-2026-00018"}', N'СЗ зарегистрирована', NULL, NULL),
    (DATEADD(DAY,  -3, GETDATE()), 4, 31, N'DocumentTask', 3, NULL, N'{"ExecutorId":4}', N'Поручение Стерликову (в отпуске)', NULL, NULL),
    (DATEADD(DAY,  -3, GETDATE()), 1, 82, N'TaskDelegation', 1, NULL, N'{"To":5}', N'Авто-делегирование Стерликов→Дорофеев', NULL, NULL),
    (DATEADD(DAY,  -3, GETDATE()), 1, 80, N'Substitution', 1, NULL, N'{"Original":4,"Substitute":5}', N'Создано замещение на отпуск', NULL, NULL),
    (DATEADD(DAY,  -3, GETDATE()), 4, 40, N'DocumentApproval', 1, NULL, N'{"StageId":1}', N'Запущен маршрут согласования', NULL, NULL),
    (DATEADD(DAY,  -2, GETDATE()), 4, 41, N'DocumentApproval', 1, NULL, N'{"Decision":"Approved"}', N'Согласовано Стерликовым (1-я стадия)', NULL, NULL),
    (DATEADD(DAY,  -2, GETDATE()), 9, 60, N'DocumentSignature', 5, NULL, N'{"Kind":"Simple"}', N'ПЭП Зайченко по договору', NULL, NULL),
    (DATEADD(DAY,  -2, GETDATE()), 7, 0,  N'Document', 10, NULL, NULL, N'Создано исх. ИСХ-2026-00021 (ответ на ВХ-2026-00037)', NULL, NULL),
    (DATEADD(DAY,  -2, GETDATE()), 9, 21, N'DocumentAttachment', 8, NULL, N'{"VersionNumber":2}', N'Новая версия договора (юрист)', NULL, NULL),
    (DATEADD(DAY,  -1, GETDATE()), 7, 11, N'Document', 10, NULL, N'{"RegistrationNumber":"ИСХ-2026-00021"}', N'Зарегистрировано', NULL, NULL),
    (DATEADD(DAY,  -1, GETDATE()), 7, 60, N'DocumentSignature', 6, NULL, N'{"Kind":"Simple"}', N'ПЭП Бурдиной', NULL, NULL),
    (DATEADD(DAY,  -1, GETDATE()), 1, 33, N'DocumentTask', 1, NULL, NULL, N'Авто-просрочка задачи Wi-Fi', NULL, NULL),
    (DATEADD(DAY,  -1, GETDATE()), 5, 70, N'Notification', 7, NULL, NULL, N'Уведомление Дорофееву о просрочке', NULL, NULL),
    (DATEADD(MINUTE, -45, GETDATE()), 1, 86, N'Report', NULL, NULL, N'{"Type":"Registry","Format":"XLSX"}', N'Сгенерирован реестр исходящих за месяц', NULL, NULL),
    (DATEADD(MINUTE, -30, GETDATE()), 1, 86, N'Report', NULL, NULL, N'{"Type":"Audit","Format":"PDF"}', N'Аудит-выгрузка по ПРК-2026-00007', NULL, NULL),
    (DATEADD(MINUTE, -15, GETDATE()), 1, 85, N'AttachmentTextIndex', NULL, NULL, NULL, N'Перестроен полнотекстовый индекс (7 записей)', NULL, NULL),
    (DATEADD(MINUTE,  -5, GETDATE()), 1, 90, N'Employee', 1, NULL, NULL, N'Логин администратора', NULL, NULL);

PRINT N'AhuErp: demo-life seed применён.';
PRINT N'Аккаунты: admin / sterlikov / dorofeev / burdina / zaychenko / volkov / petrova';
PRINT N'Пароль везде: password';
GO
