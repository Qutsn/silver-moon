using System.Globalization;
using System.Text.Json;
using Middleton.Desktop;

static class LocalizationChecks
{
 public static void Run()
 {
  foreach(var (name,expected) in new[]{("zh-CN","zh"),("zh-TW","zh"),("zh-HK","zh"),("en-US","en"),("en-GB","en"),("de-DE","en"),("ja-JP","en"),("","en")})
   if(UiLanguage.Select(CultureInfo.GetCultureInfo(name))!=expected)throw new Exception("Language selection: "+name);
  var source=new Dictionary<string,string>{{"a","中文"}};
  if(UiLanguage.Resolve("a",new Dictionary<string,string>{{"a","English"}},source)!="English")throw new Exception("Translation ignored");
  if(UiLanguage.Resolve("a",new Dictionary<string,string>{{"a",""}},source)!="中文")throw new Exception("Empty translation did not fall back");
  if(UiLanguage.Resolve("missing",source,source)!="missing")throw new Exception("Unknown key fallback");
  var root=Environment.GetEnvironmentVariable("MIDDLETON_TEST_REPOSITORY")!;
  var zh=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(root,"glass-playground/Localization/zh.json")))!;
  var en=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(root,"glass-playground/Localization/en.json")))!;
  if(!zh.Keys.Order().SequenceEqual(en.Keys.Order()) || zh.Values.Any(string.IsNullOrWhiteSpace) || en.Values.Any(string.IsNullOrWhiteSpace))throw new Exception("Catalog shape mismatch");
  var original=CultureInfo.CurrentUICulture;
  try
  {
   foreach(var culture in new[]{"en-US","de-DE","zh-CN","zh-TW"})
   {
    AppText.ConfigureCulture(CultureInfo.GetCultureInfo(culture));
    var expected=culture.StartsWith("zh")?zh:en;
    foreach(var (key,value) in expected)if(AppText.Get(key)!=value)throw new Exception("Embedded translation mismatch: "+culture+"/"+key);
   }
  }
  finally{AppText.ConfigureCulture(original);}
  Console.WriteLine("PASS: display language selection, translation/fallback and catalog key parity");
 }
}
