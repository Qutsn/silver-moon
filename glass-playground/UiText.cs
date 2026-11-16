using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Avalonia.Markup.Xaml;
using Middleton.Desktop;

namespace GlassPlayground;

public static class UiText
{
 static CultureInfo DisplayCulture()
 {
  var args=Environment.GetCommandLineArgs();
  var preview=args.Contains("--preview")?args.FirstOrDefault(a=>a.StartsWith("--ui-culture=",StringComparison.Ordinal)):null;
  return preview is null?CultureInfo.CurrentUICulture:CultureInfo.GetCultureInfo(preview[13..]);
 }
 static UiText()=>AppText.ConfigureCulture(DisplayCulture());
 public static string Language=>AppText.Language;
 public static string Get(string key)=>AppText.Get(key);
 public static string Format(string key,params object?[] values)=>AppText.Format(key,values);
}

public sealed class TrExtension : MarkupExtension
{
 public string Key {get;set;}="";
 public TrExtension(){}
 public TrExtension(string key)=>Key=key;
 public override object ProvideValue(IServiceProvider serviceProvider)=>UiText.Get(Key);
}
