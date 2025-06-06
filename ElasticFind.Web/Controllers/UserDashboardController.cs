using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElasticFind.Web.Controllers;

public class UserDashboardController : Controller
{
    [Authorize(Roles = "2")]
    public IActionResult Index()
    {
        return View();
    }
}
