using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="IAuditService"/> с hash-цепочкой целостности.
    /// Алгоритм:
    /// <list type="number">
    ///   <item><description>Сериализуем значимые поля записи в каноническую строку.</description></item>
    ///   <item><description>Конкатенируем с <see cref="AuditLog.PreviousHash"/>.</description></item>
    ///   <item><description>Считаем SHA-256 → hex и сохраняем в <see cref="AuditLog.Hash"/>.</description></item>
    /// </list>
    /// Это даёт быструю детекцию подмены любой записи: пересчёт по цепочке
    /// расходится с сохранёнными значениями. Сервис не предоставляет API для
    /// удаления/изменения записей.
    /// </summary>
    public sealed class AuditService : IAuditService
    {
        private readonly IAuditLogRepository _repository;
        private readonly object _sync = new object();

        public AuditService(IAuditLogRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public AuditLog Record(
            AuditActionType actionType,
            string entityType,
            int? entityId,
            int? userId,
            string oldValues = null,
            string newValues = null,
            string details = null)
        {
            // Запись аудита потенциально вызывается из разных сервисов и UI-потока,
            // поэтому защищаем чтение «последняя запись → новая запись» одним
            // монитором: иначе возможна гонка с расхождением hash-цепочки.
            lock (_sync)
            {
                var previous = _repository.GetLast();
                // SQL Server DATETIME имеет точность ~3.33 мс и округляет
                // значение при сохранении. Если бы hash считался от точного
                // DateTime.UtcNow (с тиковой точностью), после round-trip
                // через БД пересчёт hash расходился бы со сохранённым, и
                // VerifyChain() сообщал бы ложную «коррупцию» первой записи.
                // Поэтому усекаем timestamp до целых секунд ДО вычисления
                // хеша — это значение SQL Server возвращает без потерь.
                var ticksPerSecond = TimeSpan.TicksPerSecond;
                var truncated = new DateTime(
                    DateTime.UtcNow.Ticks / ticksPerSecond * ticksPerSecond,
                    DateTimeKind.Utc);
                var log = new AuditLog
                {
                    Timestamp = truncated,
                    UserId = userId,
                    ActionType = actionType,
                    EntityType = entityType,
                    EntityId = entityId,
                    OldValues = Truncate(oldValues, 4000),
                    NewValues = Truncate(newValues, 4000),
                    Details = Truncate(details, 1024),
                    PreviousHash = previous?.Hash
                };
                log.Hash = ComputeHash(log);
                return _repository.Add(log);
            }
        }

        public IReadOnlyList<AuditLog> Query(AuditQueryFilter filter)
            => _repository.Query(filter ?? new AuditQueryFilter());

        public AuditLog VerifyChain()
        {
            string previousHash = null;
            foreach (var entry in _repository.ListAllOrdered())
            {
                if (!string.Equals(entry.PreviousHash, previousHash, StringComparison.Ordinal))
                {
                    return entry;
                }

                var expected = ComputeHash(entry);
                if (!string.Equals(entry.Hash, expected, StringComparison.Ordinal))
                {
                    return entry;
                }

                previousHash = entry.Hash;
            }
            return null;
        }

        private static string ComputeHash(AuditLog log)
        {
            var canonical = string.Join("|", new[]
            {
                // Используем фиксированный формат без timezone-суффикса:
                // "O" с DateTimeKind.Utc даёт "...Z", а после round-trip
                // через SQL Server DATETIME (Kind становится Unspecified)
                // суффикс пропадает — и хеш расходится с сохранённым.
                log.Timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
                log.UserId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                ((int)log.ActionType).ToString(CultureInfo.InvariantCulture),
                log.EntityType ?? string.Empty,
                log.EntityId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                log.OldValues ?? string.Empty,
                log.NewValues ?? string.Empty,
                log.Details ?? string.Empty,
                log.PreviousHash ?? string.Empty
            });

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= max ? value : value.Substring(0, max);
        }
    }
}
