using ElasticFind.Service.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ElasticFind.Service.Implementations;

public class UploadImageService : IUploadImageService
{
    public async Task<string?> UploadImage(IFormFile? image)
    {
        string? imagePath;
        if (image == null)
        {
            Console.WriteLine("Error in Upload Service: Provided image is null.");
            return null;
        }
        string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        imagePath = Path.Combine("uploads", Guid.NewGuid() + Path.GetExtension(image.FileName));
        using (var fileStream = new FileStream(Path.Combine("wwwroot", imagePath), FileMode.Create))
        {
            await image.CopyToAsync(fileStream);
        }

        return imagePath;
    }
}
