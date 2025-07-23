using System.Threading.Tasks;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ElasticFind.Web.Controllers;

public class CategoriesController : Controller
{
    private readonly ICategoryService _categoryService;
    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 5, string? searchString = null, string? sortOrder = null)
    {
        PaginationViewModel pagination = new()
        {
            Page = page,
            PageSize = pageSize,
            SearchString = searchString,
            SortOrder = sortOrder
        };

        DisplayCategoriesViewModel displayCategoriesViewModel = await _categoryService.GetCategories(pagination);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_CategoriesPartial", displayCategoriesViewModel);
        }

        return View(displayCategoriesViewModel);
    }
}
