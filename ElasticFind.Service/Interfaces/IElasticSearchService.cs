using ElasticFind.Repository.ViewModels;

namespace ElasticFind.Service.Interfaces;

public interface IElasticSearchService
{
    Task<bool> CreateDocumentIndexAsync(string indexName);
    Task<bool> IndexAsync(Humanresources hr);
    Task<List<Humanresources>> SearchByJobTitleAsync(string keyword);
    Task<bool> UpdateAsync(Humanresources hr);
    Task<bool> UpdateFieldAsync(int id, string newJobTitle);
    Task<bool> DeleteAsync(int id);
    Task<List<GroupedSearchResults>> SearchDocumentsAsync(string keyword);
}
