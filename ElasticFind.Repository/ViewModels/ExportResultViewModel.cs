namespace ElasticFind.Repository.ViewModels;

public class ExportResultViewModel
{
    public string SearchType { get; set; } = null!;
    public string Keyword { get; set; } = null!;
    public string? FileType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? SortBy { get; set; }
    public string? SearchString { get; set; }
    public List<SearchResultItem>? Results { get; set; }
}
