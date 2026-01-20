using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vault.Data.Context;
using Vault.Interfaces;
using Vault.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<VaultContext>(Options =>
{
    Options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});
builder.Services.AddScoped(typeof(IVaultRepository<>), typeof(VaultRepository<>));

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();


