using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Тестовое хранилище вложений: байты содержатся в памяти, ключи аналогичны
    /// файловой системе (<c>{Year}/{RegNumber}/v{Version}/{FileName}</c>).
    /// </summary>
    public sealed class InMemoryFileStorageService : IFileStorageService
    {
        // ёЁ нужно перечислить явно — они вне диапазонов А-Я/а-я.
        private static readonly Regex UnsafeChars = new Regex(@"[^A-Za-zА-Яа-яЁё0-9._-]+", RegexOptions.Compiled);
        private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        public string Store(Stream content, string registrationNumber, int version, string fileName)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Имя файла обязательно.", nameof(fileName));
            var year = DateTime.Now.Year.ToString("0000");
            var key = $"{year}/{Sanitize(registrationNumber ?? "Unregistered")}/v{version}/{Sanitize(fileName)}";
            using (var ms = new MemoryStream())
            {
                content.CopyTo(ms);
                _files[key] = ms.ToArray();
            }
            return key;
        }

        public Stream Open(string storagePath)
        {
            if (!_files.TryGetValue(storagePath, out var bytes))
                throw new FileNotFoundException("Вложение не найдено в in-memory хранилище.", storagePath);
            return new MemoryStream(bytes, writable: false);
        }

        public bool Delete(string storagePath) => _files.Remove(storagePath);

        public bool Exists(string storagePath) => _files.ContainsKey(storagePath);

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "_";
            return UnsafeChars.Replace(value.Trim(), "_");
        }
    }
}
