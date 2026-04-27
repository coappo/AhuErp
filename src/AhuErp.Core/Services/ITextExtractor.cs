using System.IO;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Strategy-интерфейс извлечения текста из бинарного вложения.
    /// Реализации: <see cref="PdfTextExtractor"/>, <see cref="DocxTextExtractor"/>,
    /// <see cref="PlainTextExtractor"/>.
    /// </summary>
    public interface ITextExtractor
    {
        bool CanHandle(string fileName);

        /// <summary>
        /// Извлекает plain-text из потока. Возвращает пустую строку при сбое
        /// (не бросает исключение, чтобы не блокировать индексацию пакетом).
        /// </summary>
        string Extract(Stream stream);
    }
}
