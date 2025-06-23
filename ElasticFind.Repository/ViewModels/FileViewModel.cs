namespace ElasticFind.Repository.ViewModels;

public class FileViewModel
{
    public required string Id { get; set; }
    public required string FileName { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime? UploadedDate { get; set; }
}
