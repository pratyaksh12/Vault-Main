using Microsoft.EntityFrameworkCore;
using Vault.Data.Context;
using Vault.Index.IServices;
using Vault.Index.Services;
using Vault.Interfaces;
using Vault.Repositories;
using Vault.Tasks;


var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<VaultContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});


builder.Services.AddScoped(typeof(IVaultRepository<>), typeof(VaultRepository<>));


var elasticUri = new Uri(builder.Configuration["ElasticSearch:Uri"] ?? "http://localhost:9200");
builder.Services.AddSingleton<IElasticSearchService>(sp => new ElasticSearchService(elasticUri));


builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
