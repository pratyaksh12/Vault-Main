using System;
using System.Reflection.Metadata;
using Microsoft.EntityFrameworkCore;
using Vault.Models;
using Document = Vault.Models.Document;

namespace Vault.Data.Context;

public class VaultContext(DbContextOptions<VaultContext> options) : DbContext(options)
{
    public DbSet<Document> Documents{get;set;} = null!;
    public DbSet<NamedEntity> NamedEntities{get;set;} = null!;
    public DbSet<UserInventory> UserInventories{get; set;} = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasIndex(e => e.ParentId).HasDatabaseName("document_parent_id");
            entity.HasIndex(e=>e.Status).HasDatabaseName("document_status");
        });
        modelBuilder.Entity<NamedEntity>(entity =>
        {
            entity.HasIndex(e => e.DocId).HasDatabaseName("named_entity_doc_id");
        });
    }
}
