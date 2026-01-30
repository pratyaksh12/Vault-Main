using System;
using System.Text.RegularExpressions;
using Elastic.Clients.Elasticsearch;

namespace Vault.Tasks;

public class EntityExtractor
{
   private static readonly Regex EmailRegex = new Regex(
    @"/^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$/",
    RegexOptions.Compiled | RegexOptions.IgnoreCase
   ) ;

   private static readonly Regex PhoneRegex = new Regex(
    @"^\+?\d{1,3}\s?\(?\d{1,4}\)?[\s.-]?\d{1,4}[\s.-]?\d{1,9}$",
    RegexOptions.Compiled
   );

   private static readonly Regex DateRegex = new Regex(
    @"\b(?:\d{2}[\/-]\d{2}[\/-]\d{4}|\d{4}[-.]\d{2}[-.]\d{2})\b",
    RegexOptions.Compiled
   );

   public static Dictionary<string, List<string>> Extract(string content)
    {
        var result = new Dictionary<string, List<string>>();
        if (string.IsNullOrWhiteSpace(content))
        {
            return result;
        }

        var emails = EmailRegex.Matches(content).Select(m => m.Value.ToLower()).Distinct().ToList();
        if(emails.Count > 0) result["emails"] = emails;

        var phones = PhoneRegex.Matches(content).Select(m => m.Value).Distinct().ToList();
        if(phones.Count > 1) result["phones"] = phones;

        var dates = DateRegex.Matches(content).Select(m => m.Value).Distinct().ToList();
        if(dates.Count > 0) result["dates"] = dates;


        return result;
        
    }

}
