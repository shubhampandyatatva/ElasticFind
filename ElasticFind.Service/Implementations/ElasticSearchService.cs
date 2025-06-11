using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Nest;

namespace ElasticFind.Service.Implementations;

public class ElasticSearchService : IElasticSearchService
{
    private readonly IElasticClient _elasticClient;

    public ElasticSearchService(IElasticClient elasticClient)
    {
        _elasticClient = elasticClient;
    }

    public async Task<bool> IndexAsync(Humanresources hr)
    {
        var response = await _elasticClient.IndexDocumentAsync(hr);
        return response.IsValid;
    }

    public async Task<List<Humanresources>> SearchByJobTitleAsync(string keyword)
    {
        var response = await _elasticClient.SearchAsync<Humanresources>(s => s
            .Query(q => q
                .Match(m => m
                    .Field(f => f.Jobtitle)
                    .Query(keyword)
                )
            )
        );

        return response.Documents.ToList();
    }

    public async Task<bool> UpdateAsync(Humanresources hr)
    {
        var response = await _elasticClient.IndexAsync(hr, i => i.Id(hr.Nationalidnumber));
        return response.IsValid;
    }

    public async Task<bool> UpdateFieldAsync(int id, string newJobTitle)
    {
        var response = await _elasticClient.UpdateAsync<Humanresources>(id, u => u
            .Doc(new Humanresources { Jobtitle = newJobTitle })
        );
        return response.IsValid;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var response = await _elasticClient.DeleteAsync<Humanresources>(id);
        return response.IsValid;
    }
}
