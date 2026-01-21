using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vault.Models;

[Table("user_inventory")]
public class UserInventory
{
    [Key]
    [Column("id")]
    [MaxLength(96)]
    public string Id{get;set;} = null!;

    [Column("email")]
    public string? Email{get; set;}

    [Column("name")]
    public string Name{get; set;} = null!;

    [Column("provider")]
    [MaxLength(255)]
    public string? Provider{get; set;}

}
