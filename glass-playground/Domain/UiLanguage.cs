using System;
using System.Collections.Generic;
using System.Globalization;

namespace Middleton.Desktop;

public static class UiLanguage
{
 // Add a supported neutral language here when its resource catalog is ready.
 static readonly HashSet<string> Supported=new(StringComparer.OrdinalIgnoreCase){"zh","en"};
 public static string Select(CultureInfo culture)
 {
  for(var current=culture;current!=CultureInfo.InvariantCulture;current=current.Parent)
   if(Supported.Contains(current.Name))return current.Name.ToLowerInvariant();
  return "en";
 }
 public static string Resolve(string key,IReadOnlyDictionary<string,string> selected,IReadOnlyDictionary<string,string> fallback)
 {
  if(selected.TryGetValue(key,out var translated) && !string.IsNullOrWhiteSpace(translated))return translated;
  return fallback.TryGetValue(key,out var source)?source:key;
 }
}
