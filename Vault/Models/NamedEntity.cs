using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vault.Models;

[Table("named_entity")]
public class NamedEntity
{
    [Key]
    [Column("id")]
    [MaxLength(96)]    
    public string Id {get; set;} = null!;
    
    [Column("mention")]
    public string Mention {get; set;} = null!;

    [Column("ne_offset")]
    public long Offset {get; set;}

    [Column("extractor")]
    public byte Extractor{get; set;}

    [Column("category")]
    [MaxLength(8)]
    public string? Category{get;set;} = null!;

    [Column("doc_id")]
    [MaxLength(96)]
    public string DocId{get; set;} = null!;

    [Column("root_id")]
    [MaxLength(96)]
    public string? RootId{get; set;}

    [Column("extractor_language")]
    [MaxLength(2)]
    public string? ExtractorLanguage {get; set;} = null!;

    [Column("hidden")]
    public bool? Hidden{get; set;}

}
