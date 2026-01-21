using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vault.Data.Context;
using Vault.Interfaces;
using Vault.Repositories;
using Vault.Index.IServices;
using Vault.Index.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<VaultContext>(Options =>
{
    Options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});
builder.Services.AddScoped(typeof(IVaultRepository<>), typeof(VaultRepository<>));
var elasticUri = new Uri(builder.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200");
builder.Services.AddSingleton<IElasticSearchService>(sp => new ElasticSearchService(elasticUri));

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();


