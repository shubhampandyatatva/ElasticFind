namespace ElasticFind.Repository.ViewModels;

public class ExportResultViewModel
{
    public string Keyword { get; set; } = null!;
    public List<SearchResultItem>? Results { get; set; }
}
