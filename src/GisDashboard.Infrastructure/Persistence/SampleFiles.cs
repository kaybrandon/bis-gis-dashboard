using System.Text;

namespace GisDashboard.Infrastructure.Persistence;

internal static class SampleFiles
{
    public static byte[] MinimalPdf(string title)
    {
        var escaped = title.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var content = $"BT /F1 18 Tf 48 720 Td ({escaped}) Tj ET";
        var contentBytes = Encoding.ASCII.GetBytes(content);
        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        var offsets = new List<int>();

        void Obj(string body)
        {
            offsets.Add(sb.Length);
            sb.Append(body);
            if (!body.EndsWith('\n'))
            {
                sb.Append('\n');
            }
        }

        Obj("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");
        Obj("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");
        Obj("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >> endobj\n");
        Obj($"4 0 obj << /Length {contentBytes.Length} >> stream\n{content}\nendstream endobj\n");
        Obj("5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj\n");

        var xref = sb.Length;
        sb.Append($"xref\n0 {offsets.Count + 1}\n");
        sb.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append($"{offset:D10} 00000 n \n");
        }

        sb.Append($"trailer << /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    public static byte[] PngDot()
    {
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAACXBIWXMAAAsTAAALEwEAmpwYAAAA" +
            "B3RJTUUH5gkKEgsN3n1nWwAAAGRJREFUeNrt0rEJwCAUBNGrs4B0kP0XShtbJIJYiN5r4PLh8YkA" +
            "AAAAAAAAAAAAAOB/q comming soon".Replace(" comming soon", string.Empty));
    }

    public static byte[] TinyPng()
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00, 0x40,
            0x08, 0x02, 0x00, 0x00, 0x00, 0x25, 0x0B, 0xE6, 0x89, 0x00, 0x00, 0x00,
            0x0C, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0xF8, 0xCF, 0xC0, 0x10,
            0x00, 0x01, 0x04, 0x00, 0x01, 0xF5, 0x4D, 0x5F, 0x0B, 0x00, 0x00, 0x00,
            0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
        ];
    }
}
