namespace ElasticFind.Repository.ViewModels;

public class ExportResultViewModel
{
    public string? SortBy { get; set; }
    public int TotalDocumentsCount { get; set; }
    public string? SelectedCategory { get; set; }
    public List<SearchResultItem>? Results { get; set; }
}
