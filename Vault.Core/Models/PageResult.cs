using System;

namespace Vault.Core.Models;

public class PageResult<T>
{
    public IEnumerable<T> Items{get; set;} = new List<T>();
    public long TotalCount{get; set;}
    public int Page{get; set;}
    public int PageSize{get; set;}

}
