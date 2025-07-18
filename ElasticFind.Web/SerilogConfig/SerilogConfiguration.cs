using Serilog;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;

namespace ElasticFind.Web.SerilogConfig;
    public static class SerilogConfiguration
    {
    public static void ConfigureSerilog(IConfiguration configuration)
    {
        Dictionary<string, ColumnWriterBase> columnWriters = new()
        {
            ["environment_name"] = new SinglePropertyColumnWriter("EnvironmentName", PropertyWriteMethod.Raw),
            ["exception"] = new ExceptionColumnWriter(),
            ["file_path"] = new SinglePropertyColumnWriter("FilePath", PropertyWriteMethod.Raw),
            ["ip_address"] = new SinglePropertyColumnWriter("IPAddress", PropertyWriteMethod.Raw),
            ["level"] = new LevelColumnWriter(true, NpgsqlTypes.NpgsqlDbType.Varchar),
            ["line_number"] = new SinglePropertyColumnWriter("LineNumber", PropertyWriteMethod.Raw),
            ["machine_name"] = new SinglePropertyColumnWriter("MachineName"),
            ["message"] = new RenderedMessageColumnWriter(),
            ["message_template"] = new MessageTemplateColumnWriter(),
            ["method_name"] = new SinglePropertyColumnWriter("MethodName", PropertyWriteMethod.Raw),
            ["process_info"] = new SinglePropertyColumnWriter("ProcessInfo", PropertyWriteMethod.Raw),
            ["properties"] = new LogEventSerializedColumnWriter(),
            ["props_test"] = new SinglePropertyColumnWriter("PropsTest", PropertyWriteMethod.Raw),
            ["raise_date"] = new TimestampColumnWriter(),
            ["thread_id"] = new SinglePropertyColumnWriter("ThreadId"),
            ["user_agent"] = new SinglePropertyColumnWriter("UserAgent", PropertyWriteMethod.Raw),
            ["user_name"] = new SinglePropertyColumnWriter("UserName", PropertyWriteMethod.Raw)
        };

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .WriteTo.PostgreSQL(
                connectionString: configuration.GetConnectionString("DefaultConnection"),
                tableName: "logs",
                columnOptions: columnWriters,
                needAutoCreateTable: true
            )
            .CreateLogger();
        }
    }
