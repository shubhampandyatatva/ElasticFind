using ElasticFind.Repository.ViewModels;

namespace ElasticFind.Service.Interfaces;

public interface IExportService
{
    byte[] ExportSearchResultsToExcel(List<GroupedSearchResults> results, string keyword, string fileType, DateTime? startDate, DateTime? endDate, string sortedBy, string searchString, int totalRecords);
}
