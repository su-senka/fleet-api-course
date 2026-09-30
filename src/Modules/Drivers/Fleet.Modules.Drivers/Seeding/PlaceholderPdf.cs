using System.Text;

namespace Fleet.Modules.Drivers.Seeding;

/// <summary>
/// Builds a minimal, valid single-page PDF, so seeded certificates that claim a scan have real
/// bytes behind them in blob storage rather than a blob id that resolves to nothing.
/// </summary>
internal static class PlaceholderPdf
{
    public static byte[] Build(string label)
    {
        var content = $"BT /F1 18 Tf 40 100 Td ({Escape(label)}) Tj ET";
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 4 0 R >> >> /MediaBox [0 0 300 150] /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
        };

        var buffer = new StringBuilder();
        buffer.Append("%PDF-1.4\n");

        // Offsets are computed as the file is built up, rather than hard-coded, so the xref table
        // is always correct regardless of how long the label makes the content stream.
        var offsets = new int[objects.Count + 1];

        for (var i = 0; i < objects.Count; i++)
        {
            offsets[i + 1] = Encoding.ASCII.GetByteCount(buffer.ToString());
            buffer.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(buffer.ToString());

        buffer.Append($"xref\n0 {objects.Count + 1}\n");
        buffer.Append("0000000000 65535 f \n");
        for (var i = 1; i <= objects.Count; i++)
        {
            buffer.Append($"{offsets[i]:D10} 00000 n \n");
        }

        buffer.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");

        return Encoding.ASCII.GetBytes(buffer.ToString());
    }

    private static string Escape(string text) => text.Replace("(", "\\(").Replace(")", "\\)");
}
