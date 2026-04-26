using System.IO;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Извлечение текста из <c>.pdf</c> через PdfPig 0.1.9.
    /// При сбое (запароленный/повреждённый файл) возвращает <c>string.Empty</c>.
    /// </summary>
    public sealed class PdfTextExtractor : ITextExtractor
    {
        public bool CanHandle(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            return Path.GetExtension(fileName).Equals(".pdf", System.StringComparison.OrdinalIgnoreCase);
        }

        public string Extract(Stream stream)
        {
            if (stream == null) return string.Empty;
            try
            {
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    ms.Position = 0;
                    using (var pdf = PdfDocument.Open(ms))
                    {
                        var sb = new StringBuilder();
                        foreach (Page page in pdf.GetPages())
                        {
                            sb.AppendLine(page.Text);
                        }
                        return sb.ToString();
                    }
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
