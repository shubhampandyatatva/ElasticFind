using ElasticFind.Repository.Interfaces;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Repository.Data;
using ElasticFind.Service.Interfaces;
using Nest;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ElasticFind.Service.Implementations;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IElasticClient _elasticClient;
    private readonly IUserService _userService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    public CategoryService(ICategoryRepository categoryRepository, IElasticClient elasticClient, IUserService userService, IHttpContextAccessor httpContextAccessor)
    {
        _categoryRepository = categoryRepository;
        _elasticClient = elasticClient;
        _userService = userService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<DisplayCategoriesViewModel> GetCategories(PaginationViewModel pagination)
    {
        List<CategoryViewModel> categoryList = _categoryRepository.GetCategoryList(pagination);
        int totalRecords = pagination.SearchString == null ? await _categoryRepository.GetTotalCategories() : await _categoryRepository.GetTotalSearchedCategories(pagination.SearchString);
        pagination.TotalRecords = totalRecords;

        DisplayCategoriesViewModel viewModel = new()
        {
            PaginationViewModel = pagination,
            CategoryList = categoryList
        };

        return viewModel;
    }

    public async Task<JsonResponse> AddCategory(string name, string? description)
    {
        try
        {
            Category? existingCategory = await _categoryRepository.GetCategoryByName(name);
            if (existingCategory != null)
            {
                return new JsonResponse { Success = false, Message = "This category already exists!" };
            }

            string indexName = name.ToLowerInvariant();
            var existsResponse = await _elasticClient.Indices.ExistsAsync(indexName);
            if (existsResponse.ApiCall.HttpStatusCode == 404) 
            {
                var createIndexResponse = await _elasticClient.Indices.CreateAsync(indexName, c => c
                    .Map<DocumentViewModel>(m => m
                        .AutoMap()
                    )
                );

                if (!createIndexResponse.IsValid)
                {
                    return new JsonResponse { Success = false, Message = "There was an error creating the category! Please try again." };
                }
            }

            string? jwtToken = _httpContextAccessor.HttpContext?.Request.Cookies["JwtToken"];

            Category category = new()
            {
                Name = name,
                Description = description,
                ModifiedAt = DateTime.UtcNow,
                CreatedBy = _userService.GetClaimValue(jwtToken, ClaimTypes.Name),
            };

            await _categoryRepository.AddCategory(category);

            return new JsonResponse { Success = true, Message = "Category created successfully!" };
        }
        catch (Exception)
        {
            return new JsonResponse { Success = false, Message = "There was an error creating the category! Please try again." };
        }
    }

    public async Task<JsonResponse> DeleteCategory(string name)
    {
        try
        {
            var response = await _elasticClient.Indices.DeleteAsync(name);
            if (response.IsValid)
            {
                await _categoryRepository.DeleteCategoryByName(name);

                return new JsonResponse { Success = true, Message = "Category deleted successfully!" };
            }
            else
            {
                return new JsonResponse { Success = false, Message = "There was an error deleting the category! Please try again." };
            }
        }
        catch (Exception)
        {
            return new JsonResponse { Success = false, Message = "There was an error deleting the category! Please try again." };
        }
    }

    public async Task<Category?> GetCategoryByName(string name)
    {
        return await _categoryRepository.GetCategoryByName(name);
    }

    public async Task<JsonResponse> EditCategory(int id, string newName, string oldName, string? description)
    {
        try
        {
            Category? existingCategory = await _categoryRepository.GetCategoryById(id);
            if (existingCategory == null)
            {
                return new JsonResponse { Success = false, Message = "Category not found!" };
            }

            oldName = oldName.ToLowerInvariant();
            string lowerCaseName = newName.ToLowerInvariant();
            string? jwtToken = _httpContextAccessor.HttpContext?.Request.Cookies["JwtToken"];
            string? currentUser = _userService.GetClaimValue(jwtToken, ClaimTypes.Name);

            if (lowerCaseName != oldName)
            {
                Category? category = await _categoryRepository.GetCategoryByName(lowerCaseName);
                if (category != null)
                {
                    return new JsonResponse { Success = false, Message = "Category already exists!" };
                }

                var reindexResponse = await _elasticClient.ReindexOnServerAsync(r => r
                    .Source(s => s.Index(oldName))
                    .Destination(d => d.Index(lowerCaseName))
                );

                if (!reindexResponse.IsValid)
                {
                    return new JsonResponse { Success = false, Message = "There was an error reindexing the category! Please try again." };
                }

                var deleteIndexResponse = await _elasticClient.Indices.DeleteAsync(oldName);
                if (!deleteIndexResponse.IsValid)
                {
                    return new JsonResponse { Success = false, Message = "There was an error deleting the existing category index! Please try again." };
                }
            }

            existingCategory.Name = newName;
            existingCategory.Description = description;
            existingCategory.ModifiedAt = DateTime.UtcNow;
            existingCategory.ModifiedBy = currentUser;

            await _categoryRepository.UpdateCategory(existingCategory);
            return new JsonResponse { Success = true, Message = "Category updated successfully!" };
        }
        catch (Exception)
        {
            return new JsonResponse { Success = false, Message = "There was an error creating the category! Please try again." };
        }
    }
}
