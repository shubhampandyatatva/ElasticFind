using ElasticFind.Repository.ViewModels;
using ElasticFind.Web.Connector;
using Microsoft.AspNetCore.Mvc;

namespace ElasticFind.Web.Controllers;

public class ElasticSearchController : Controller
{
    private readonly ConnectionToEs _connectionToEs;
    public ElasticSearchController()
    {
        _connectionToEs = new ConnectionToEs();
    }

    [HttpGet]
    public ActionResult Search()
    {
        return View();
    }

    public JsonResult DataSearch(string jobtitle, string nationalIDNumber)
    {
        var responsedata = _connectionToEs.EsClient().Search<Humanresources>(s => s
                                .Index("humanresources")
                                .Size(50)
                                .Query(q => q
                                    .Match(m => m
                                        .Field(f => f.Jobtitle)
                                        .Query(jobtitle)
                                    )
                                )
                            );

        var datasend = (from hits in responsedata.Hits
                        select hits.Source).ToList();

        return Json(new { datasend, responsedata.Took });
    }
}
