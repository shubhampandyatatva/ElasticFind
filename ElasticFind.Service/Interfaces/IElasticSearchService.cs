using ElasticFind.Repository.ViewModels;
using Nest;

namespace ElasticFind.Service.Interfaces;

public interface IElasticSearchService
{
    Task<bool> CreateDocumentIndexAsync(string indexName);
    Task<bool> IndexAsync(Humanresources hr);
    Task<List<Humanresources>> SearchByJobTitleAsync(string keyword);
    Task<bool> UpdateAsync(Humanresources hr);
    Task<bool> UpdateFieldAsync(int id, string newJobTitle);
    Task<bool> DeleteAsync(string id);
    Task<SearchResultsViewModel> SearchDocumentsAsync(string searchType, bool matchAllTerms, string keyword, string? fileTypeFilter = null, DateTime? startDate = null, DateTime? endDate = null, string? sortBy = null, string? searchInput = null, int currentPage = 1, int pageSize = 5, string? esBoolQuery = null);
    Task<DisplayFilesViewModel> GetFilesAsync(PaginationViewModel paginationViewModel);
    Task<List<string>> GetAllFileIdsAsync();
    Task<bool> DeleteMultipleFilesAsync(string id);
    QueryContainer ConvertRulesToElasticsearchQuery(QueryBuilderRule rules);
    // Task<SearchResultsViewModel> QueryBuilderSearch(QueryBuilderRule rules);
}
