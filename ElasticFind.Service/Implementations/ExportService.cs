using System.Drawing;
using System.Text.RegularExpressions;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Microsoft.AspNetCore.Hosting;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace ElasticFind.Service.Implementations;

public class ExportService : IExportService
{
    private readonly IWebHostEnvironment _webHostEnvironment;

    public ExportService(IWebHostEnvironment webHostEnvironment)
    {
        _webHostEnvironment = webHostEnvironment;
    }

    public byte[] ExportSearchResultsToExcel(List<GroupedSearchResults> results, string keyword, string fileType, DateTime? startDate, DateTime? endDate, string sortedBy, string searchString, int totalRecords)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Search Results");

        string webRootPath = _webHostEnvironment.WebRootPath;

        string imagePath = Path.Combine(webRootPath, "images", "elasticsearch-logo.png");

        if (File.Exists(imagePath))
        {
            using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
            {
                var picture = worksheet.Drawings.AddPicture("ElasticFindLogo", stream);
                picture.SetPosition(1, 0, 14, 0);
                picture.SetSize(150, 100);
            }
        }

        //Set headers with merging of cells

        worksheet.Cells["A2:B3"].Merge = true;
        worksheet.Cells["C2:F3"].Merge = true;
        worksheet.Cells["A5:B6"].Merge = true;
        worksheet.Cells["C5:F6"].Merge = true;

        worksheet.Cells["H2:I3"].Merge = true;
        worksheet.Cells["J2:M3"].Merge = true;
        worksheet.Cells["H5:I6"].Merge = true;
        worksheet.Cells["J5:M6"].Merge = true;

        //Set alignation of filters names

        worksheet.Cells["A2:B3"].Style.Font.Color.SetColor(Color.White);
        worksheet.Cells["A2:B3"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells["A2:B3"].Style.Fill.BackgroundColor.SetColor(Color.Blue);
        worksheet.Cells["A2:B3"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        worksheet.Cells["A2:B3"].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

        worksheet.Cells["A5:B6"].Style.Font.Color.SetColor(Color.White);
        worksheet.Cells["A5:B6"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells["A5:B6"].Style.Fill.BackgroundColor.SetColor(Color.Blue);
        worksheet.Cells["A5:B6"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        worksheet.Cells["A5:B6"].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

        worksheet.Cells["H2:I3"].Style.Font.Color.SetColor(Color.White);
        worksheet.Cells["H2:I3"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells["H2:I3"].Style.Fill.BackgroundColor.SetColor(Color.Blue);
        worksheet.Cells["H2:I3"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        worksheet.Cells["H2:I3"].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

        worksheet.Cells["H5:I6"].Style.Font.Color.SetColor(Color.White);
        worksheet.Cells["H5:I6"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells["H5:I6"].Style.Fill.BackgroundColor.SetColor(Color.Blue);
        worksheet.Cells["H5:I6"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        worksheet.Cells["H5:I6"].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

        //Set alignment of filter texts

        worksheet.Cells["C2:F3"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        worksheet.Cells["C2:F3"].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

        worksheet.Cells["C5:F6"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        worksheet.Cells["C5:F6"].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

        worksheet.Cells["J2:M3"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        worksheet.Cells["J2:M3"].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

        worksheet.Cells["J5:M6"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        worksheet.Cells["J5:M6"].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

        // Set values in filter Names

        worksheet.Cells["A2"].Value = "File Type:";
        worksheet.Cells["A5"].Value = "Date Range:";
        worksheet.Cells["H2"].Value = "Sorted By:";
        worksheet.Cells["H5"].Value = "Search Keyword:";

        //Set values in filters

        worksheet.Cells["C2"].Value = fileType;
        worksheet.Cells["C5"].Value = (startDate != null && endDate != null) ? (startDate?.ToString("dd-MM-yyyy") + " to " + endDate?.ToString("dd-MM-yyyy")) : "All Time";
        worksheet.Cells["J2"].Value = sortedBy;
        worksheet.Cells["J5"].Value = searchString;

        worksheet.Cells["A8:E8"].Merge = true;
        worksheet.Cells["A8:E8"].Value = $"{totalRecords} matching documents found for \"{keyword}\".";
        worksheet.Cells["A8:E8"].Style.Font.Bold = true;
        worksheet.Cells["A8:E8"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        worksheet.Cells["A8:E8"].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        worksheet.Cells["A8:E8"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells["A8:E8"].Style.Fill.BackgroundColor.SetColor(Color.Blue);
        worksheet.Cells["A8:E8"].Style.Font.Color.SetColor(Color.White);

        //Start printing the search results

        int resultStartRow = 10;

        foreach (var doc in results)
        {
            // Print the document name with match count
            string headerText = $"{doc.FileName} - Found {doc.Snippets.Count} matches";
            worksheet.Cells[$"A{resultStartRow}:M{resultStartRow}"].Merge = true;
            worksheet.Cells[$"A{resultStartRow}"].Value = headerText;
            worksheet.Cells[$"A{resultStartRow}"].Style.Font.Bold = true;
            worksheet.Cells[$"A{resultStartRow}"].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[$"A{resultStartRow}"].Style.Fill.BackgroundColor.SetColor(Color.SkyBlue);
            worksheet.Cells[$"A{resultStartRow}"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
            resultStartRow++;

            // Print each matched fragment
            foreach (var snippet in doc.Snippets)
            {
                worksheet.Cells[$"A{resultStartRow}:M{resultStartRow}"].Merge = true;
                worksheet.Cells[$"A{resultStartRow}"].Value = snippet;
                worksheet.Cells[$"A{resultStartRow}"].Style.WrapText = true;
                worksheet.Cells[$"A{resultStartRow}"].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
                resultStartRow++;
            }

            // Add a blank row after each document's fragments
            resultStartRow += 2;
        }


        worksheet.Cells.AutoFitColumns();

        return package.GetAsByteArray();
    }
}
