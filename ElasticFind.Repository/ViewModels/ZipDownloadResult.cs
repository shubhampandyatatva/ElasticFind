namespace ElasticFind.Repository.ViewModels;

public class ZipDownloadResult
{
    public required byte[] FileBytes { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
}
