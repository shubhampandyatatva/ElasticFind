namespace ElasticFind.Repository.ViewModels;

public class SearchResultItem
{
    public string? FileName { get; set; }
    public string? FileType { get; set; }
    public int MatchCount { get; set; }
    public string? HighlightedUploadedDate { get; set; }
    public List<string>? Snippets { get; set; }
}
