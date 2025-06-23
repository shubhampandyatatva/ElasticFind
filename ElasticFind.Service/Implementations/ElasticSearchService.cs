using System.Text;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Elasticsearch.Net;
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

    public async Task<bool> DeleteAsync(string id)
    {
        var response = await _elasticClient.DeleteAsync<DocumentViewModel>(id, d => d.Index("documents").Refresh(Elasticsearch.Net.Refresh.WaitFor));
        Console.WriteLine($"Delete response: {response.DebugInformation}");
        return response.IsValid;
    }

    public async Task<List<GroupedSearchResults>> SearchDocumentsAsync(
    string keyword, string? fileTypeFilter = null, DateTime? startDate = null,
    DateTime? endDate = null, string? sortBy = null, string? searchInput = null)
    {
        var mustQueries = new List<Func<QueryContainerDescriptor<DocumentViewModel>, QueryContainer>>();

        // Full-text search on content
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // mustQueries.Add(q => q.Match(m => m
            //     .Field(f => f.Attachment.Content)
            //     .Query(keyword)
            // ));
            mustQueries.Add(q => q.Match(m => m
                .Field(f => f.Attachment.Content)
                .Query(keyword)
            ));
        }

        // Filter by file type
        if (!string.IsNullOrWhiteSpace(fileTypeFilter) && fileTypeFilter != "File Type")
        {
            mustQueries.Add(q => q.Term(t => t
                .Field(f => f.FileType.Suffix("keyword"))  // keyword for exact match
                .Value(fileTypeFilter)
            ));
        }

        if (!string.IsNullOrWhiteSpace(searchInput))
        {
            mustQueries.Add(q => q.Wildcard(w => w
                .Field(f => f.FileName.Suffix("keyword"))
                .Value($"*{searchInput.ToLowerInvariant()}*")
            ));
        }

        // Normalize to date only
        var start = startDate?.Date; //Sets time component to 00:00:00
        var end = endDate?.Date.AddDays(1).AddTicks(-1); // End of the day, Sets time component to 23:59:59
        // Filter by date range
        if (startDate.HasValue || endDate.HasValue)
        {
            mustQueries.Add(q => q.DateRange(dr => dr
                .Field(f => f.UploadedDate)
                .GreaterThanOrEquals(start)
                .LessThanOrEquals(end)
            ));
        }

        // Sorting
        Func<SortDescriptor<DocumentViewModel>, IPromise<IList<ISort>>>? sort = null;
        if (!string.IsNullOrWhiteSpace(sortBy) && sortBy != "Sort By")
        {
            sort = s =>
            {
                switch (sortBy)
                {
                    case "1": // Sort by date
                        s.Descending(f => f.UploadedDate);
                        break;
                    case "2": // Sort by file type
                        s.Ascending(f => f.FileType.Suffix("keyword"));
                        break;
                }
                return s;
            };
        }

        var response = await _elasticClient.SearchAsync<DocumentViewModel>(s => s
            .Index("documents")
            .Query(q => q.Bool(b => b.Must(mustQueries)))
            .Highlight(h => h
                .Fields(f => f
                    .Field("attachment.content")
                    .PreTags("<mark>")
                    .PostTags("</mark>")
                    .FragmentSize(200)
                    .NumberOfFragments(50)
                    .NoMatchSize(150)
                )
            )
            .Sort(sort)
        );

        var decoded = Encoding.UTF8.GetString(response.ApiCall.RequestBodyInBytes);
        Console.WriteLine("ElasticClient Response Decoded: " + decoded);

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


    public async Task<DisplayFilesViewModel> GetFilesAsync(PaginationViewModel paginationViewModel)
    {
        var searchResponse = await _elasticClient.SearchAsync<DocumentViewModel>(s => s
            .Index("documents")
            .From((paginationViewModel.Page - 1) * paginationViewModel.PageSize)
            .Size(paginationViewModel.PageSize)
            .Query(q =>
            string.IsNullOrEmpty(paginationViewModel.SearchString)
            ? q.MatchAll()
            : q.Wildcard(w => w
                    .Field(f => f.FileName.Suffix("keyword"))
                    .Value($"*{paginationViewModel.SearchString.ToLowerInvariant()}*")
                ))
            .Sort(st => string.IsNullOrEmpty(paginationViewModel.SortOrder) ? null : st.Field(f => f.FileName, paginationViewModel.SortOrder == "Asc" ? SortOrder.Ascending : SortOrder.Descending))
        );

        List<FileViewModel> files = searchResponse.Documents.Select(doc => new FileViewModel
        {
            Id = doc.Id,
            FileName = doc.FileName
        }).ToList();

        paginationViewModel.TotalRecords = paginationViewModel.SearchString == null ?
                (int)searchResponse.Total :
                (int)(await _elasticClient.CountAsync<DocumentViewModel>(c => c
                .Index("documents")
                .Query(q => q.Wildcard(w => w
                    .Field(f => f.FileName.Suffix("keyword"))
                    .Value($"*{paginationViewModel.SearchString.ToLowerInvariant()}*")
                ))
            )).Count;

        DisplayFilesViewModel displayFilesViewModel = new()
        {
            Pagination = paginationViewModel,
            Files = files
        };

        return displayFilesViewModel;
    }
}
