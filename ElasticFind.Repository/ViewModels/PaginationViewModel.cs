namespace ElasticFind.Repository.ViewModels;

public class PaginationViewModel
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string? SearchString { get; set; }
    public required string SortOrder { get; set; }
    public int TotalRecords { get; set; }
}
