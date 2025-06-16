using ElasticFind.Service.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using OfficeOpenXml;
using System.Text;

namespace ElasticFind.Service.Implementations;

public class PreviewFileService : IPreviewFileService
{
    public string GetPreviewHtml(string fileName, byte[] fileBytes)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        return ext switch
        {
            ".pdf" or ".txt" or ".html" => "",
            ".xlsx" => PreviewExcel(fileBytes),
            ".csv" => PreviewCsv(fileBytes),
            ".docx" => PreviewDocx(fileBytes),
            _ => "<p class='text-danger'>Preview not supported for this file type.</p>"
        };
    }

    private string PreviewExcel(byte[] fileBytes)
    {
        using var stream = new MemoryStream(fileBytes);
        using var package = new ExcelPackage(stream);

        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        if (worksheet == null) return "<p>No sheets found in Excel file.</p>";

        var sb = new StringBuilder();
        sb.Append(HtmlHeader("Excel Preview"));
        sb.Append("<div class='table-responsive'><table class='table table-bordered table-sm'>");

        int rowCount = worksheet.Dimension.End.Row;
        int colCount = worksheet.Dimension.End.Column;

        for (int row = 1; row <= rowCount; row++)
        {
            sb.Append("<tr>");
            for (int col = 1; col <= colCount; col++)
            {
                var cell = worksheet.Cells[row, col].Text;
                sb.AppendFormat("<td>{0}</td>", System.Net.WebUtility.HtmlEncode(cell));
            }
            sb.Append("</tr>");
        }

        sb.Append("</table></div></body></html>");
        return sb.ToString();
    }

    private string PreviewCsv(byte[] fileBytes)
    {
        var content = Encoding.UTF8.GetString(fileBytes);
        var lines = content.Split('\n');
        var sb = new StringBuilder();

        sb.Append(HtmlHeader("CSV Preview"));
        sb.Append("<div class='table-responsive'><table class='table table-bordered table-sm'>");

        foreach (var line in lines)
        {
            sb.Append("<tr>");
            foreach (var cell in line.Split(','))
            {
                sb.AppendFormat("<td>{0}</td>", System.Net.WebUtility.HtmlEncode(cell));
            }
            sb.Append("</tr>");
        }

        sb.Append("</table></div></body></html>");
        return sb.ToString();
    }

    private string PreviewDocx(byte[] fileBytes)
    {
        using var stream = new MemoryStream(fileBytes);
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart.Document.Body;
        var text = body.InnerText;

        var sb = new StringBuilder();
        sb.Append(HtmlHeader("DOCX Preview"));

        foreach (var line in text.Split('\n'))
        {
            sb.AppendFormat("<p>{0}</p>", System.Net.WebUtility.HtmlEncode(line));
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private string HtmlHeader(string title)
    {
        return $"<html><head><title>{title}</title>" +
               "<link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css'>" +
               "</head><body class='p-3'>";
    }
}
