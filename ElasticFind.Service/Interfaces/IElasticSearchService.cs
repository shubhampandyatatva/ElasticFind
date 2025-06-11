using ElasticFind.Repository.ViewModels;

namespace ElasticFind.Service.Interfaces;

public interface IElasticSearchService
{
    Task<bool> IndexAsync(Humanresources hr);
    Task<List<Humanresources>> SearchByJobTitleAsync(string keyword);
}
