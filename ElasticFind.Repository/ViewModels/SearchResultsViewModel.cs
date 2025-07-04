namespace ElasticFind.Repository.ViewModels;

public class SearchResultsViewModel
{
    public int TotalDocuments { get; set; }
    public List<GroupedSearchResults> SearchResults { get; set; } = new List<GroupedSearchResults>();
}
