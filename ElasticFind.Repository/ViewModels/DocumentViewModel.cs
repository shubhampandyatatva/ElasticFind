namespace ElasticFind.Repository.ViewModels;

public class DocumentViewModel
{
    public required string Id { get; set; }
    public required string FileName { get; set; }
    public required string FileType { get; set; }
    public DateTime? UploadedDate { get; set; }
    public AttachmentData? Attachment { get; set; }
    public string? Data { get; set; }
}
