using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vault.Models;
[Table("Document")]
public class Document
{
    [Key]
    [Column("id")]
    [MaxLength(96)]
    public string Id{get; set;} = null!;

    [Column("path")]
    [MaxLength(4096)]
    public string Path{get; set;} = null!;

    [Column("project_id")]
    [MaxLength(96)]
    public string ProjectId{get;set;} = null!;

    [Column("page_number")]
    [MaxLength(10000)]
    public int PageNumber{get; set;} = 1;

    [Column("content")]
    public string Content{get; set;} = null!;

    [Column("metadata")]
    public string Metadata{get;set;} = null!;

    [Column("status")]
    public byte? Status{get; set;}

    [Column("extraction_level")]
    public byte? ExtractionLevel{get; set;} = null!;

    [Column("language")]
    [MaxLength(2)]
    public string? Language{get; set;} = null!;

    [Column("extraction_date")]
    public DateTime? ExtractionDate{get; set;}

    [Column("parent_id")]
    [MaxLength(96)]
    public string ParentId{get; set;}= null!;

    [Column("root_id")]
    [MaxLength(96)]
    public string? RootId{get; set;} = null!;

    [Column("content_type")]
    [MaxLength(256)]
    public string? ContentType{get; set;} = null!;

    [Column("content_length")]
    public long? ContentLength{get; set;} = null!;

    [Column("charset")]
    [MaxLength(32)]
    public string? Charset{get; set;}

    [Column("ner_mask")]
    public short? NerMask {get; set;}

    [Column("checksum")]
    [MaxLength(64)]
    public string Checksum{get; set;} = null!;
}


