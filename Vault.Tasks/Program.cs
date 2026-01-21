using Vault.Index.IServices;
using Vault.Index.Services;
using Vault.Tasks;

var builder = Host.CreateApplicationBuilder(args);
var elasticUri = new Uri(builder.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200");
builder.Services.AddSingleton<IElasticSearchService>(sp => new ElasticSearchService(elasticUri));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
