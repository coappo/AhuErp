using System.IO;
using System.Text;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Извлечение текста из <c>.txt</c> / <c>.md</c> / <c>.csv</c> /
    /// <c>.log</c>. Декодирует UTF-8 (с BOM-определением).
    /// </summary>
    public sealed class PlainTextExtractor : ITextExtractor
    {
        public bool CanHandle(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".txt" || ext == ".md" || ext == ".csv" || ext == ".log";
        }

        public string Extract(Stream stream)
        {
            if (stream == null) return string.Empty;
            using (var reader = new StreamReader(stream, Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
