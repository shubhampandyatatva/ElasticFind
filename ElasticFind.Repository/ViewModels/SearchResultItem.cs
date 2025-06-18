namespace ElasticFind.Repository.ViewModels;

public class SearchResultItem
{
    public string? FileName { get; set; }
    public int MatchCount { get; set; }
    public List<string>? Snippets { get; set; }
}
