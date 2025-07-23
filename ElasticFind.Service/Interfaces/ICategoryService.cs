using ElasticFind.Repository.ViewModels;

namespace ElasticFind.Service.Interfaces;

public interface ICategoryService
{
    Task<DisplayCategoriesViewModel> GetCategories(PaginationViewModel pagination);
}
