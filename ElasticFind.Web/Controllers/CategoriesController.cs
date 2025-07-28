using System.Threading.Tasks;
using ElasticFind.Repository.Data;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Constants;
using ElasticFind.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nest;

namespace ElasticFind.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
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

    public async Task<JsonResponse> AddEditCategory(int? id, string name, string oldCategoryName, string? description)
    {
        if (id != null)
        {
            return await _categoryService.EditCategory((int)id, name, oldCategoryName, description);
        }
        else
        {
            return await _categoryService.AddCategory(name, description);
        }
    }

    public async Task<JsonResponse> DeleteCategory(string name)
    {
        return await _categoryService.DeleteCategory(name);
    }

    public async Task<JsonResult> GetCategoryByName(string name)
    {
        Category? category = await _categoryService.GetCategoryByName(name);
        if (category == null)
        {
            return Json(new { success = false, message = "Category not found." });
        }
        return Json(new { success = true, category });
    }
}
