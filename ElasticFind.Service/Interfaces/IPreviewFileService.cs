namespace ElasticFind.Service.Interfaces;

public interface IPreviewFileService
{
    public string GetPreviewHtml(string fileName, byte[] fileBytes);
}
