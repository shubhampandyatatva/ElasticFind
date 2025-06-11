using Elasticsearch.Net;
using Nest;

namespace ElasticFind.Web.Connector;

public class ConnectionToEs
{
    public ElasticClient EsClient()
    {
        // Uri[] nodes = new Uri[]
        // {
        //         new("https://localhost:9200/")
        // };

        // var connectionPool = new StaticConnectionPool(nodes);
        // var connectionSettings = new ConnectionSettings(pool).DisableDirectStreaming();

        var connectionPool = new SingleNodeConnectionPool(new Uri("https://localhost:9200"));
        var connectionSettings = new ConnectionSettings(connectionPool)
            .ServerCertificateValidationCallback((sender, cert, chain, errors) => true) // Ignore cert errors
            .BasicAuthentication("elastic", "158xkDDd9Qn1fajXw0K1")
            .DefaultIndex("jobs");

        var elasticClient = new ElasticClient(connectionSettings);

        return elasticClient;
    }
}
