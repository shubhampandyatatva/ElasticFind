using Microsoft.AspNetCore.Mvc;

namespace ElasticFind.Web.Controllers;

public class StatusCodeController : Controller
{
    [HttpGet]
    [Route("StatusCode/{statusCode}")]
    public IActionResult Index(int statusCode)
    {
        if(statusCode == 401)
        return View("Error401");

        if(statusCode == 403)
        return View("Error403"); 

        if(statusCode == 404)
        return View("Error404");

        else
        return View("Error500");
    }
}
