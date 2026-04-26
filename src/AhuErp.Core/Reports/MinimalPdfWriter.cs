using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AhuErp.Core.Reports
{
    /// <summary>
    /// Минимальный самостоятельный PDF-писатель (PDF 1.4): один или несколько
    /// страниц A4 с текстом моноширинным шрифтом Courier (стандартный
    /// «Type 1» из 14 base fonts — не требует встраивания файла шрифта).
    /// Достаточно для регламентированного аудит-журнала; рендерится в любом
    /// PDF-ридере.
    ///
    /// Реализован вручную, без PdfSharp/MigraDoc, потому что эти пакеты
    /// зависят от Windows GDI+ и падают в mono/Linux-CI.
    /// </summary>
    public sealed class MinimalPdfWriter
    {
        private const int PageWidth = 595;   // A4 portrait
        private const int PageHeight = 842;
        private const int MarginLeft = 50;
        private const int MarginTop = 50;
        private const int LineHeight = 14;
        private const int FontSize = 10;
        private const int LinesPerPage = (PageHeight - 2 * MarginTop) / LineHeight;

        private readonly List<string> _lines = new List<string>();

        public void AddLine(string text)
        {
            _lines.Add(text ?? string.Empty);
        }

        public void AddBlank() => _lines.Add(string.Empty);

        public void Save(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));

            // Разбиваем строки на страницы.
            var pages = new List<List<string>>();
            for (int i = 0; i < _lines.Count; i += LinesPerPage)
            {
                pages.Add(_lines.GetRange(i, Math.Min(LinesPerPage, _lines.Count - i)));
            }
            if (pages.Count == 0) pages.Add(new List<string> { string.Empty });

            // Структура объектов:
            //   1: Catalog
            //   2: Pages (kids)
            //   3: Font
            //   4..: Page + ContentStream (по 2 объекта на страницу)
            int pagesObj = 2;
            int fontObj = 3;
            int firstPageObj = 4;

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs, Encoding.ASCII))
            {
                var offsets = new List<long>();
                void WriteAscii(string s) => bw.Write(Encoding.ASCII.GetBytes(s));

                WriteAscii("%PDF-1.4\n");
                WriteAscii("%\u00E2\u00E3\u00CF\u00D3\n");

                // 1: Catalog
                offsets.Add(bw.BaseStream.Position);
                WriteAscii($"1 0 obj\n<< /Type /Catalog /Pages {pagesObj} 0 R >>\nendobj\n");

                // 2: Pages
                offsets.Add(bw.BaseStream.Position);
                var kids = new StringBuilder();
                for (int i = 0; i < pages.Count; i++)
                {
                    if (i > 0) kids.Append(' ');
                    kids.Append((firstPageObj + i * 2).ToString(CultureInfo.InvariantCulture)).Append(" 0 R");
                }
                WriteAscii($"{pagesObj} 0 obj\n<< /Type /Pages /Count {pages.Count} /Kids [{kids}] >>\nendobj\n");

                // 3: Font (Courier — base14, метрики не нужны)
                offsets.Add(bw.BaseStream.Position);
                WriteAscii($"{fontObj} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>\nendobj\n");

                // Страницы и потоки.
                for (int i = 0; i < pages.Count; i++)
                {
                    int pageObj = firstPageObj + i * 2;
                    int contentObj = pageObj + 1;

                    // Поток содержимого.
                    var content = new StringBuilder();
                    content.Append("BT\n");
                    content.Append($"/F1 {FontSize} Tf\n");
                    content.Append($"{LineHeight} TL\n");
                    int y = PageHeight - MarginTop;
                    content.Append($"{MarginLeft} {y} Td\n");
                    bool firstLine = true;
                    foreach (var raw in pages[i])
                    {
                        if (!firstLine) content.Append("T*\n");
                        firstLine = false;
                        content.Append('(').Append(EscapePdfString(raw)).Append(") Tj\n");
                    }
                    content.Append("ET\n");

                    // WinAnsi-кодированный поток (чтобы кириллица отображалась).
                    var streamBytes = Win1251Encoding().GetBytes(content.ToString());

                    // Page object
                    offsets.Add(bw.BaseStream.Position);
                    WriteAscii($"{pageObj} 0 obj\n<< /Type /Page /Parent {pagesObj} 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 {fontObj} 0 R >> >> /Contents {contentObj} 0 R >>\nendobj\n");

                    // Content object
                    offsets.Add(bw.BaseStream.Position);
                    WriteAscii($"{contentObj} 0 obj\n<< /Length {streamBytes.Length} >>\nstream\n");
                    bw.Write(streamBytes);
                    WriteAscii("\nendstream\nendobj\n");
                }

                // xref
                long xrefStart = bw.BaseStream.Position;
                int objectCount = 1 + offsets.Count; // +1 потому что 0-й объект — free
                WriteAscii($"xref\n0 {objectCount}\n");
                WriteAscii("0000000000 65535 f \n");
                foreach (var off in offsets)
                {
                    WriteAscii($"{off.ToString("D10", CultureInfo.InvariantCulture)} 00000 n \n");
                }

                // trailer
                WriteAscii($"trailer\n<< /Size {objectCount} /Root 1 0 R >>\nstartxref\n{xrefStart}\n%%EOF\n");
            }
        }

        /// <summary>Экранируем спецсимволы в PDF-string-литерале.</summary>
        private static string EscapePdfString(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (ch == '\\') sb.Append("\\\\");
                else if (ch == '(') sb.Append("\\(");
                else if (ch == ')') sb.Append("\\)");
                else if (ch == '\r') sb.Append(' ');
                else if (ch == '\n') sb.Append(' ');
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Кодировка для PDF-потока: предпочитаем Windows-1251, если доступна
        /// (на mono без CodePagesEncodingProvider возможен fallback на ISO-8859-1
        /// — это нормально для регламентированных «латинских» отчётов).
        /// </summary>
        private static Encoding Win1251Encoding()
        {
            try { return Encoding.GetEncoding(1251); }
            catch { return Encoding.GetEncoding("ISO-8859-1"); }
        }
    }
}
