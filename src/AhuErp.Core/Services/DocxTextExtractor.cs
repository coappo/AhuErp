using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Извлечение текста из <c>.docx</c> через DocumentFormat.OpenXml.
    /// </summary>
    public sealed class DocxTextExtractor : ITextExtractor
    {
        public bool CanHandle(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            return Path.GetExtension(fileName).Equals(".docx", System.StringComparison.OrdinalIgnoreCase);
        }

        public string Extract(Stream stream)
        {
            if (stream == null) return string.Empty;
            try
            {
                // OpenXml требует seekable stream — копируем в MemoryStream.
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    ms.Position = 0;
                    using (var doc = WordprocessingDocument.Open(ms, isEditable: false))
                    {
                        var body = doc.MainDocumentPart?.Document?.Body;
                        if (body == null) return string.Empty;
                        var sb = new StringBuilder();
                        foreach (var t in body.Descendants<Text>())
                        {
                            sb.Append(t.Text);
                            sb.Append(' ');
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
