using ElasticFind.Repository.ViewModels;
using ElasticFind.Repository.Data;

namespace ElasticFind.Service.Interfaces;

public interface ICategoryService
{
    Task<JsonResponse> AddCategory(string name, string? description);
    Task<JsonResponse> DeleteCategory(string name);
    Task<JsonResponse> EditCategory(string name, string oldCategoryName, string? description);
    Task<DisplayCategoriesViewModel> GetCategories(PaginationViewModel pagination);
    Task<Category?> GetCategoryByName(string name);

}
