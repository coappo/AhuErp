using System;
using System.IO;
using System.Text.RegularExpressions;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Хранение вложений в локальной файловой системе. Корневая папка задаётся
    /// в конструкторе (по умолчанию — <c>%LocalAppData%/AhuErp/Documents</c>).
    /// Структура: <c>{root}/{Year}/{SafeRegNumber}/v{Version}/{SafeFileName}</c>.
    /// </summary>
    public sealed class FileSystemStorageService : IFileStorageService
    {
        // Запрещённые в Windows-имени файла символы плюс пробел/двоеточие/слэши,
        // которые встречаются в регистрационных номерах вида АХУ-01/2026-00037.
        // Включаем ёЁ явно: они вне А-Я/а-я и иначе схлопывались бы в '_',
        // искажая имена вроде «Отчёт», «Учёт», «Алёна».
        private static readonly Regex UnsafeChars = new Regex(@"[^A-Za-zА-Яа-яЁё0-9._-]+", RegexOptions.Compiled);

        private readonly string _root;

        public FileSystemStorageService(string root = null)
        {
            _root = root ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AhuErp", "Documents");
            Directory.CreateDirectory(_root);
        }

        public string Store(Stream content, string registrationNumber, int version, string fileName)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Имя файла обязательно.", nameof(fileName));

            var year = DateTime.Now.Year.ToString("0000");
            var safeReg = Sanitize(string.IsNullOrWhiteSpace(registrationNumber)
                ? "Unregistered"
                : registrationNumber);
            var safeName = Sanitize(Path.GetFileName(fileName));
            var relative = Path.Combine(year, safeReg, "v" + version, safeName);
            var absolute = Path.Combine(_root, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            using (var fs = new FileStream(absolute, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                content.CopyTo(fs);
            }
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        public Stream Open(string storagePath)
        {
            var full = ToAbsolute(storagePath);
            return new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public bool Delete(string storagePath)
        {
            var full = ToAbsolute(storagePath);
            if (!File.Exists(full)) return false;
            File.Delete(full);
            return true;
        }

        public bool Exists(string storagePath)
        {
            var full = ToAbsolute(storagePath);
            return File.Exists(full);
        }

        private string ToAbsolute(string storagePath)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
                throw new ArgumentException("Путь хранения обязателен.", nameof(storagePath));
            // storagePath приходит относительным (форвардслеши); приводим к платформенному.
            var rel = storagePath.Replace('/', Path.DirectorySeparatorChar);
            // Защита от выхода за корень хранилища (path traversal).
            var combined = Path.GetFullPath(Path.Combine(_root, rel));
            var rootFull = Path.GetFullPath(_root);
            // Гарантируем границу директории: иначе путь
            // C:\AhuErp\Documents_Evil\... прошёл бы префикс-проверку
            // как сосед каталога C:\AhuErp\Documents.
            if (!rootFull.EndsWith(Path.DirectorySeparatorChar.ToString()))
                rootFull += Path.DirectorySeparatorChar;
            if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Недопустимый путь хранения.");
            return combined;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "_";
            var trimmed = value.Trim();
            return UnsafeChars.Replace(trimmed, "_");
        }
    }
}
