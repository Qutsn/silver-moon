using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Middleton.Desktop;

// Shared by the UI and device services. Localization never changes protocol values.
public static class AppText
{
 static readonly Dictionary<string,string> Source=Load("zh");
 static readonly Dictionary<string,string> English=Load("en");
 static string language=UiLanguage.Select(CultureInfo.CurrentUICulture);
 public static string Language=>language;
 public static void ConfigureCulture(CultureInfo culture)=>language=UiLanguage.Select(culture);
 static Dictionary<string,string> Load(string language)
 {
  using var stream=typeof(AppText).Assembly.GetManifestResourceStream($"GlassPlayground.Localization.{language}.json")
   ??throw new InvalidDataException($"Missing embedded UI catalog: {language}");
  return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)??throw new InvalidDataException("Invalid UI catalog.");
 }
 public static string Get(string key)=>UiLanguage.Resolve(key,language=="zh"?Source:English,Source);
 public static string Format(string key,params object?[] values)=>string.Format(CultureInfo.CurrentCulture,Get(key),values);
}
