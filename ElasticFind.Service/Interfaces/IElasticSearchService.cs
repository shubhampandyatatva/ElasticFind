using ElasticFind.Repository.ViewModels;

namespace ElasticFind.Service.Interfaces;

public interface IElasticSearchService
{
    Task<bool> CreateDocumentIndexAsync(string indexName);
    Task<bool> IndexAsync(Humanresources hr);
    Task<List<Humanresources>> SearchByJobTitleAsync(string keyword);
    Task<bool> UpdateAsync(Humanresources hr);
    Task<bool> UpdateFieldAsync(int id, string newJobTitle);
    Task<bool> DeleteAsync(string id);
    Task<List<GroupedSearchResults>> SearchDocumentsAsync(string searchType, string keyword, string? fileTypeFilter = null, DateTime? startDate = null, DateTime? endDate = null, string? sortBy = null, string? searchInput = null);
    Task<DisplayFilesViewModel> GetFilesAsync(PaginationViewModel paginationViewModel);
}
