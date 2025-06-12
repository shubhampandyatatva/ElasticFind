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

    public async Task<bool> CreateDocumentIndexAsync(string indexName)
    {
        indexName = indexName.ToLowerInvariant(); // Outputs lowercase index names irrespective of any culture (to maintain similarity of lowercase index names everywhere in our elasticsearch)

        var existsResponse = await _elasticClient.Indices.ExistsAsync(indexName);
        if (existsResponse.Exists)
            return true; // Already exists

        var createIndexResponse = await _elasticClient.Indices.CreateAsync(indexName, c => c
            .Map<DocumentViewModel>(m => m
                .AutoMap()
            )
        );

        return createIndexResponse.IsValid;
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

    public async Task<List<GroupedSearchResults>> SearchDocumentsAsync(string keyword)
    {
        var response = await _elasticClient.SearchAsync<DocumentViewModel>(s => s
            .Index("documents")
            .Query(q => q
                .Match(m => m
                    .Field("attachment.content")
                    .Query(keyword)
                )
            )
            .Highlight(h => h
                .Fields(f => f
                    .Field("attachment.content")
                    .PreTags("<mark>")     // Highlight start
                    .PostTags("</mark>")   // Highlight end
                    .FragmentSize(300)           // increase fragment length
                    .NumberOfFragments(50)       // allow more fragments to be returned
                    .NoMatchSize(150)           // return snippet even if keyword is not matched
                )
            )
        );

        var results = new List<GroupedSearchResults>();

        foreach (var hit in response.Hits)
        {
            if (hit.Highlight.TryGetValue("attachment.content", out var highlights))
            {
                results.Add(new GroupedSearchResults
                {
                    Id = hit.Id,
                    FileName = hit.Source.FileName,
                    Snippets = highlights.ToList()
                });
            }
        }

        return results;
    }
}
