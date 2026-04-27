using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AhuErp.Core.Reports
{
    /// <summary>
    /// Минимальный самостоятельный PDF-писатель (PDF 1.4). Пишет одну или
    /// несколько страниц A4 моноширинным шрифтом Courier (стандартный
    /// «Type 1» из 14 base fonts — не требует встраивания файла шрифта).
    /// Реализован вручную, без PdfSharp/MigraDoc, потому что эти пакеты
    /// зависят от Windows GDI+ и падают в mono/Linux-CI.
    ///
    /// <para>
    /// <b>Ограничение по кириллице.</b> Стандартные base14-шрифты
    /// (Courier/Helvetica/Times) не содержат кириллических глифов: даже с
    /// <c>/Differences</c> или <c>/Encoding /WinAnsiEncoding</c> символы 0x80–0xFF
    /// отрисовываются «пробелом» или мусором. Поэтому весь входной текст
    /// транслитерируется в латиницу (ГОСТ 7.79-2000 system A) перед записью
    /// в поток. Для регламентированной аудит-выгрузки документа это
    /// приемлемо: hash-цепочка, рег-номер, временные метки и тип действия
    /// читаются однозначно. Для полноценного UI-PDF лучше использовать
    /// MigraDoc на Windows-стенде.
    /// </para>
    ///
    /// <para>
    /// Кодировки выровнены: декларация шрифта <c>/WinAnsiEncoding</c> (CP-1252)
    /// и поток содержимого пишется тем же CP-1252 — несоответствия нет.
    /// </para>
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

            // CP-1252 (WinAnsi) — соответствует декларации /Encoding /WinAnsiEncoding
            // в объекте шрифта. После транслитерации все символы — ASCII, так что
            // CP-1252 покрывает их без потерь.
            var streamEncoding = WinAnsiEncoding();

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs, Encoding.ASCII))
            {
                var offsets = new List<long>();
                void WriteAscii(string s) => bw.Write(Encoding.ASCII.GetBytes(s));

                WriteAscii("%PDF-1.4\n");
                // PDF spec §7.5.2: бинарный маркер — хотя бы 4 байта со значениями ≥128,
                // чтобы файл-передача не определила PDF как text/ascii. Пишем как сырые
                // байты; через Encoding.ASCII символы 0x80+ заменились бы на '?'.
                bw.Write(new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A });

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

                // 3: Font (Courier — base14, без встраивания)
                offsets.Add(bw.BaseStream.Position);
                WriteAscii($"{fontObj} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>\nendobj\n");

                // Страницы и потоки.
                for (int i = 0; i < pages.Count; i++)
                {
                    int pageObj = firstPageObj + i * 2;
                    int contentObj = pageObj + 1;

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
                        // Транслитерация: Courier base14 не содержит кириллических глифов,
                        // см. док-комментарий класса.
                        var safe = TransliterateToLatin(raw);
                        content.Append('(').Append(EscapePdfString(safe)).Append(") Tj\n");
                    }
                    content.Append("ET\n");

                    var streamBytes = streamEncoding.GetBytes(content.ToString());

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
        /// Кодировка для PDF-потока: Windows-1252 (WinAnsi). Соответствует
        /// декларации <c>/Encoding /WinAnsiEncoding</c> на объекте шрифта.
        /// На mono/Linux без CodePagesEncodingProvider 1252 может быть
        /// недоступна — fallback на ISO-8859-1 (для ASCII-only содержимого
        /// после транслитерации это эквивалентно).
        /// </summary>
        private static Encoding WinAnsiEncoding()
        {
            try { return Encoding.GetEncoding(1252); }
            catch { return Encoding.GetEncoding("ISO-8859-1"); }
        }

        /// <summary>
        /// Простая транслитерация русской кириллицы в латиницу
        /// (ГОСТ 7.79-2000 system A, упрощённо). Все остальные символы
        /// проходят как есть.
        /// </summary>
        private static string TransliterateToLatin(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case 'А': sb.Append("A"); break;
                    case 'Б': sb.Append("B"); break;
                    case 'В': sb.Append("V"); break;
                    case 'Г': sb.Append("G"); break;
                    case 'Д': sb.Append("D"); break;
                    case 'Е': sb.Append("E"); break;
                    case 'Ё': sb.Append("Yo"); break;
                    case 'Ж': sb.Append("Zh"); break;
                    case 'З': sb.Append("Z"); break;
                    case 'И': sb.Append("I"); break;
                    case 'Й': sb.Append("J"); break;
                    case 'К': sb.Append("K"); break;
                    case 'Л': sb.Append("L"); break;
                    case 'М': sb.Append("M"); break;
                    case 'Н': sb.Append("N"); break;
                    case 'О': sb.Append("O"); break;
                    case 'П': sb.Append("P"); break;
                    case 'Р': sb.Append("R"); break;
                    case 'С': sb.Append("S"); break;
                    case 'Т': sb.Append("T"); break;
                    case 'У': sb.Append("U"); break;
                    case 'Ф': sb.Append("F"); break;
                    case 'Х': sb.Append("Kh"); break;
                    case 'Ц': sb.Append("Ts"); break;
                    case 'Ч': sb.Append("Ch"); break;
                    case 'Ш': sb.Append("Sh"); break;
                    case 'Щ': sb.Append("Shch"); break;
                    case 'Ъ': sb.Append("\""); break;
                    case 'Ы': sb.Append("Y"); break;
                    case 'Ь': sb.Append("'"); break;
                    case 'Э': sb.Append("E"); break;
                    case 'Ю': sb.Append("Yu"); break;
                    case 'Я': sb.Append("Ya"); break;
                    case 'а': sb.Append("a"); break;
                    case 'б': sb.Append("b"); break;
                    case 'в': sb.Append("v"); break;
                    case 'г': sb.Append("g"); break;
                    case 'д': sb.Append("d"); break;
                    case 'е': sb.Append("e"); break;
                    case 'ё': sb.Append("yo"); break;
                    case 'ж': sb.Append("zh"); break;
                    case 'з': sb.Append("z"); break;
                    case 'и': sb.Append("i"); break;
                    case 'й': sb.Append("j"); break;
                    case 'к': sb.Append("k"); break;
                    case 'л': sb.Append("l"); break;
                    case 'м': sb.Append("m"); break;
                    case 'н': sb.Append("n"); break;
                    case 'о': sb.Append("o"); break;
                    case 'п': sb.Append("p"); break;
                    case 'р': sb.Append("r"); break;
                    case 'с': sb.Append("s"); break;
                    case 'т': sb.Append("t"); break;
                    case 'у': sb.Append("u"); break;
                    case 'ф': sb.Append("f"); break;
                    case 'х': sb.Append("kh"); break;
                    case 'ц': sb.Append("ts"); break;
                    case 'ч': sb.Append("ch"); break;
                    case 'ш': sb.Append("sh"); break;
                    case 'щ': sb.Append("shch"); break;
                    case 'ъ': sb.Append("\""); break;
                    case 'ы': sb.Append("y"); break;
                    case 'ь': sb.Append("'"); break;
                    case 'э': sb.Append("e"); break;
                    case 'ю': sb.Append("yu"); break;
                    case 'я': sb.Append("ya"); break;
                    case '—': sb.Append("-"); break;
                    case '–': sb.Append("-"); break;
                    case '«': sb.Append("\""); break;
                    case '»': sb.Append("\""); break;
                    case '…': sb.Append("..."); break;
                    case '№': sb.Append("No."); break;
                    default:
                        // ASCII или совместимое — пропускаем; иначе ставим '?'.
                        if (ch < 128) sb.Append(ch);
                        else sb.Append('?');
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
