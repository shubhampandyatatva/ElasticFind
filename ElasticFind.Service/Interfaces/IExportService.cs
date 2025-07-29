using ElasticFind.Repository.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ElasticFind.Service.Interfaces;

public interface IExportService
{
    byte[] ExportSearchResultsToExcel(List<GroupedSearchResults> results, string keyword, string fileType, DateTime? startDate, DateTime? endDate, string sortedBy, string searchString, int totalRecords);
    Task<ZipDownloadResult> ExportAllDocumentsToZip(string selectedCategory, string? sortBy, string? esBoolQuery);
}
