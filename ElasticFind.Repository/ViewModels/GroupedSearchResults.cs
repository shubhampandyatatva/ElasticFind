namespace ElasticFind.Repository.ViewModels;

public class GroupedSearchResults
{
    public string? Id { get; set; }
    public string? FileName { get; set; }
    public List<string>? Snippets { get; set; }
}
