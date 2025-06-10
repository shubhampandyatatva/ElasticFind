using Microsoft.AspNetCore.Http;

namespace ElasticFind.Service.Interfaces;

public interface IUploadImageService
{
    Task<string?> UploadImage(IFormFile? image);
}
