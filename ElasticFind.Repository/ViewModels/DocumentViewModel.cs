namespace ElasticFind.Repository.ViewModels;

public class DocumentViewModel
{
    public required string Id { get; set; }
    public required string FileName { get; set; }
    public string? Data { get; set; }
}
