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
        // worksheet.Cells["A8:E8"].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0, 96, 152));
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

                // worksheet.Cells[$"A{resultStartRow}:H{resultStartRow}"].Merge = true;
                // var richText = worksheet.Cells[$"A{resultStartRow}"].RichText;

                // var parts = Regex.Split(snippet, $"({Regex.Escape(keyword)})", RegexOptions.IgnoreCase);
                // foreach (var part in parts)
                // {
                //     var text = richText.Add(part);
                //     if (part.Equals(keyword, StringComparison.OrdinalIgnoreCase))
                //     {
                //         text.Bold = true;
                //         text.Color = Color.DarkOrange;
                //         text.UnderLine = true;
                //         text.Italic = true;
                //     }
                // }
                // worksheet.Cells[$"A{resultStartRow}"].Style.WrapText = true;
                // worksheet.Row(resultStartRow).Height = 30;
            }

            // Add a blank row after each document's fragments
            resultStartRow += 2;
        }


        worksheet.Cells.AutoFitColumns();

        return package.GetAsByteArray();
    }

    // public byte[] ExportCustomersDataToExcel(string searchString, string time, DateTime FromDate, DateTime ToDate, List<DisplayCustomerViewModel> customers, int totalCustomers)
    // {
    //     using (var package = new ExcelPackage())
    //     {
    //         var worksheet = package.Workbook.Worksheets.Add("OrderList");
    //         var fromDate = string.IsNullOrEmpty(FromDate.ToString()) ? "" : DateOnly.FromDateTime(FromDate).ToString();
    //         var toDate = string.IsNullOrEmpty(FromDate.ToString()) ? "" : DateOnly.FromDateTime(ToDate.AddDays(-1)).ToString();

    //         worksheet.Cells[9, 1, 9, 16].Style.Fill.PatternType = ExcelFillStyle.Solid;
    //         worksheet.Cells[9, 1, 9, 16].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0, 96, 152));

    //         worksheet.Cells[2, 1, 3, 2].Merge = true;
    //         worksheet.Cells[2, 1, 3, 2].Value = "Account";
    //         worksheet.Cells[2, 1, 3, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //         worksheet.Cells[2, 1, 3, 2].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
    //         worksheet.Cells[2, 1, 3, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
    //         worksheet.Cells[2, 1, 3, 2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0, 96, 152));
    //         worksheet.Cells[2, 1, 3, 2].Style.Font.Color.SetColor(System.Drawing.Color.White);

    //         worksheet.Cells[2, 3, 3, 6].Merge = true;
    //         worksheet.Cells[2, 3, 3, 6].Value = "PIZZASHOP";
    //         worksheet.Cells[2, 3, 3, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //         worksheet.Cells[2, 3, 3, 6].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
    //         worksheet.Cells[2, 3, 3, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
    //         worksheet.Cells[2, 3, 3, 6].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.White);
    //         worksheet.Cells[2, 3, 3, 6].Style.Font.Color.SetColor(System.Drawing.Color.Black);
    //         worksheet.Cells[2, 3, 3, 6].Style.Border.BorderAround(ExcelBorderStyle.Thin);

    //         worksheet.Cells[2, 8, 3, 9].Merge = true;
    //         worksheet.Cells[2, 8, 3, 9].Value = "Search Text:";
    //         worksheet.Cells[2, 8, 3, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //         worksheet.Cells[2, 8, 3, 9].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
    //         worksheet.Cells[2, 8, 3, 9].Style.Fill.PatternType = ExcelFillStyle.Solid;
    //         worksheet.Cells[2, 8, 3, 9].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0, 96, 152));
    //         worksheet.Cells[2, 8, 3, 9].Style.Font.Color.SetColor(System.Drawing.Color.White);

    //         worksheet.Cells[2, 10, 3, 13].Merge = true;
    //         worksheet.Cells[2, 10, 3, 13].Value = searchString;
    //         worksheet.Cells[2, 10, 3, 13].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //         worksheet.Cells[2, 10, 3, 13].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
    //         worksheet.Cells[2, 10, 3, 13].Style.Fill.PatternType = ExcelFillStyle.Solid;
    //         worksheet.Cells[2, 10, 3, 13].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.White);
    //         worksheet.Cells[2, 10, 3, 13].Style.Font.Color.SetColor(System.Drawing.Color.Black);
    //         worksheet.Cells[2, 10, 3, 13].Style.Border.BorderAround(ExcelBorderStyle.Thin);

    //         worksheet.Cells[5, 1, 6, 2].Merge = true;
    //         worksheet.Cells[5, 1, 6, 2].Value = "Date:";
    //         worksheet.Cells[5, 1, 6, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //         worksheet.Cells[5, 1, 6, 2].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
    //         worksheet.Cells[5, 1, 6, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
    //         worksheet.Cells[5, 1, 6, 2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0, 96, 152));
    //         worksheet.Cells[5, 1, 6, 2].Style.Font.Color.SetColor(System.Drawing.Color.White);

    //         worksheet.Cells[5, 3, 6, 6].Merge = true;
    //         worksheet.Cells[5, 3, 6, 6].Value = time == "Select Date Range" ? "From: " + fromDate + " To: " + toDate : time;
    //         worksheet.Cells[5, 3, 6, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //         worksheet.Cells[5, 3, 6, 6].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
    //         worksheet.Cells[5, 3, 6, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
    //         worksheet.Cells[5, 3, 6, 6].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.White);
    //         worksheet.Cells[5, 3, 6, 6].Style.Font.Color.SetColor(System.Drawing.Color.Black);
    //         worksheet.Cells[5, 3, 6, 6].Style.Border.BorderAround(ExcelBorderStyle.Thin);

    //         worksheet.Cells[5, 8, 6, 9].Merge = true;
    //         worksheet.Cells[5, 8, 6, 9].Value = "No of Records:";
    //         worksheet.Cells[5, 8, 6, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //         worksheet.Cells[5, 8, 6, 9].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
    //         worksheet.Cells[5, 8, 6, 9].Style.Fill.PatternType = ExcelFillStyle.Solid;
    //         worksheet.Cells[5, 8, 6, 9].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0, 96, 152));
    //         worksheet.Cells[5, 8, 6, 9].Style.Font.Color.SetColor(System.Drawing.Color.White);

    //         worksheet.Cells[5, 10, 6, 13].Merge = true;
    //         worksheet.Cells[5, 10, 6, 13].Value = totalCustomers;
    //         worksheet.Cells[5, 10, 6, 13].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //         worksheet.Cells[5, 10, 6, 13].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
    //         worksheet.Cells[5, 10, 6, 13].Style.Fill.PatternType = ExcelFillStyle.Solid;
    //         worksheet.Cells[5, 10, 6, 13].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.White);
    //         worksheet.Cells[5, 10, 6, 13].Style.Font.Color.SetColor(System.Drawing.Color.Black);
    //         worksheet.Cells[5, 10, 6, 13].Style.Border.BorderAround(ExcelBorderStyle.Thin);

    //         string path = _webHostEnvironment.WebRootPath;
    //         string imagePath = Path.Combine(path, "images", "logos", "pizzashop_logo.png");
    //         if (System.IO.File.Exists(imagePath))
    //         {
    //             using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
    //             {
    //                 var ExcelImage = worksheet.Drawings.AddPicture("logo", stream);
    //                 ExcelImage.SetPosition(1, 0, 14, 0);
    //                 ExcelImage.SetSize(150, 100);
    //             }
    //         }



    //         // worksheet.Cells[9, 1, 9, 1].Merge = true;
    //         worksheet.Cells[9, 1].Value = "Id";
    //         worksheet.Cells[9, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

    //         worksheet.Cells[9, 2, 9, 4].Merge = true;
    //         worksheet.Cells[9, 2, 9, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //         worksheet.Cells[9, 2, 9, 4].Value = "Name";

    //         worksheet.Cells[9, 5, 9, 8].Merge = true;
    //         worksheet.Cells[9, 5, 9, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //         worksheet.Cells[9, 5, 9, 8].Value = "Email";

    //         worksheet.Cells[9, 9, 9, 11].Merge = true;
    //         worksheet.Cells[9, 9, 9, 11].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //         worksheet.Cells[9, 9, 9, 11].Value = "Date";

    //         worksheet.Cells[9, 12, 9, 14].Merge = true;
    //         worksheet.Cells[9, 12, 9, 14].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //         worksheet.Cells[9, 12, 9, 14].Value = "Mobile Number";

    //         worksheet.Cells[9, 15, 9, 16].Merge = true;
    //         worksheet.Cells[9, 15, 9, 16].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //         worksheet.Cells[9, 15, 9, 16].Value = "Total Order";


    //         int row = 10;
    //         if (customers.Count > 0)
    //         {
    //             foreach (var c in customers)
    //             {
    //                 worksheet.Cells[row, 1, row, 1].Value = c.Id;
    //                 worksheet.Cells[row, 1, row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

    //                 worksheet.Cells[row, 2, row, 4].Merge = true;
    //                 worksheet.Cells[row, 2, row, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //                 worksheet.Cells[row, 2, row, 4].Value = c.Name;

    //                 worksheet.Cells[row, 5, row, 8].Merge = true;
    //                 worksheet.Cells[row, 5, row, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //                 worksheet.Cells[row, 5, row, 8].Value = c.Email;

    //                 worksheet.Cells[row, 9, row, 11].Merge = true;
    //                 worksheet.Cells[row, 9, row, 11].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //                 worksheet.Cells[row, 9, row, 11].Value = DateOnly.FromDateTime(FromDate).ToString();

    //                 worksheet.Cells[row, 12, row, 14].Merge = true;
    //                 worksheet.Cells[row, 12, row, 14].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //                 worksheet.Cells[row, 12, row, 14].Value = c.Phone;


    //                 worksheet.Cells[row, 15, row, 16].Merge = true;
    //                 worksheet.Cells[row, 15, row, 16].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    //                 worksheet.Cells[row, 15, row, 16].Value = c.TotalOrders;
    //                 row++;
    //             }
    //         }
    //         else
    //         {
    //             worksheet.Cells[10, 1, 10, 16].Merge = true;
    //             worksheet.Cells[10, 1, 10, 16].Value = "No Record Found";
    //             worksheet.Cells[10, 1, 10, 16].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

    //         }

    //         // Auto-fit columns for better readability
    //         worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

    //         return package.GetAsByteArray();
    //     }
    // }
}
