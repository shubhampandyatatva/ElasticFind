namespace ElasticFind.Repository.ViewModels;

public class DisplayFilesViewModel
{
    public PaginationViewModel Pagination { get; set; } = new PaginationViewModel
    {
        Page = 1,
        PageSize = 5,
        SortOrder = "Asc",
        TotalRecords = 0
    };
    public required List<FileViewModel> Files { get; set; }
}
