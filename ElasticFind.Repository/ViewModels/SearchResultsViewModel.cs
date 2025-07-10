namespace ElasticFind.Repository.ViewModels;

public class SearchResultsViewModel
{
    public string? ResultType { get; set; }
    public int TotalDocuments { get; set; }
    public List<GroupedSearchResults> SearchResults { get; set; } = new List<GroupedSearchResults>();
}
