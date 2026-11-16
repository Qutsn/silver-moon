using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Middleton.Desktop;
using System.IO;
using System.Threading;

namespace GlassPlayground;

// Visual prototype: a shared translucent action surface morphs between rows.
// This is geometric animation, not Apple's native glass or desktop refraction.
public sealed class FirmwareChooser : Canvas
{
 readonly Border[] cards=new Border[3];
 readonly TextBlock[] numbers=new TextBlock[3], titles=new TextBlock[3], details=new TextBlock[3], versions=new TextBlock[3];
 readonly Button[] choices=new Button[3];
 readonly Border glass=new() { CornerRadius=new CornerRadius(32), Background=Brush.Parse("#D8EFF0E9"), BorderBrush=Brush.Parse("#A0FFFFFF"), BorderThickness=new Thickness(1), IsHitTestVisible=false };
 readonly Button action=new(){Content=UiText.Get("FirmwareChooser.Text.961"),FontSize=19,Background=Brushes.Transparent,BorderThickness=new Thickness(0)};
 readonly TextBlock status=new(){FontSize=13,MaxWidth=1100,TextWrapping=TextWrapping.Wrap,Foreground=Brush.Parse("#CCCEC8")};
 bool selectionAnimating;
 int selectionGeneration;
 readonly Stopwatch clock=new();
 double[] current={1,0,0},from={1,0,0};
 int selected;
 double gx=969,gy=170,startX=969,startY=170;
 bool importing;
 bool confirming;
 CancellationTokenSource? preparation;
 bool previewMode=true;
 bool liveReady;
 string? importedPath;
 FirmwareAnalysis? importedAnalysis;
 public bool LiveReady {get=>liveReady;set{liveReady=value;Layout(0);}}
 bool BuiltInAvailable(int index)=>File.Exists(Path.Combine(AppContext.BaseDirectory,"Firmware",index==0?"r2.bin":"official.bin"));
 bool CanInstall=>PreviewMode || (liveReady && (selected!=2 || importedAnalysis?.Installable is not null));
 public bool PreviewMode {get=>previewMode;set{previewMode=value;action.IsEnabled=CanInstall;choices[2].IsEnabled=true;
  for(int i=0;i<2;i++){choices[i].IsEnabled=true;versions[i].Text=!value && !BuiltInAvailable(i)?UiText.Get(i==0?"Firmware.OnDemand":"Firmware.OnDemandOfficial"):UiText.Get(i==0?"FirmwareChooser.Text.2567":"FirmwareChooser.Text.2589");}}}
 public void SetStatus(string text)=>TextMotion.Set(status,text);
 public Control ActionTarget=>action;
 public void SetActionVisible(bool visible){glass.Opacity=visible?1:0;action.Opacity=visible?1:0;action.IsHitTestVisible=visible;}
 string? fileName;
 public FirmwareChooser()
 {
  string[] labels={UiText.Get("FirmwareChooser.Text.2069"),UiText.Get("FirmwareChooser.Text.2080"),UiText.Get("FirmwareChooser.Text.2089")};
  for(int i=0;i<3;i++)
  {
   cards[i]=new Border{CornerRadius=new CornerRadius(26),BorderThickness=new Thickness(1),Background=Brush.Parse("#22E5EBDD"),BorderBrush=Brush.Parse("#5AE8EDE0"),IsHitTestVisible=false};Children.Add(cards[i]);
   numbers[i]=Text($"0{i+1}",39);numbers[i].FontFamily=new FontFamily("Georgia");
   titles[i]=Text(labels[i],26);details[i]=Text(i==0?UiText.Get("FirmwareChooser.Text.2478"):i==1?UiText.Get("FirmwareChooser.Text.2509"):UiText.Get("FirmwareChooser.Text.2525"),18);
   versions[i]=Text(i==0?UiText.Get("FirmwareChooser.Text.2567"):i==1?UiText.Get("FirmwareChooser.Text.2589"):UiText.Get("FirmwareChooser.Text.2606"),15);
   int index=i;choices[i]=new Button{Content=i==0?UiText.Get("FirmwareChooser.Text.2669"):i==1?UiText.Get("FirmwareChooser.Text.2683"):UiText.Get("FirmwareChooser.Text.2692"),FontSize=16,Width=182,Height=52};choices[i].Classes.Add("secondary");choices[i].Click+=async(_,_)=>{if(index==2)await Import();else Select(index);};Children.Add(choices[i]);
  }
  Children.Add(glass);action.Classes.Add("movingAction");Children.Add(action);
  action.Click+=async(_,_)=>await ConfirmInstall();
  Children.Add(status);Put(status,22,464);
  DetachedFromVisualTree+=(_,_)=>{preparation?.Cancel();selectionAnimating=false;selectionGeneration++;};Layout(0);
 }
 public Task CheckConfirmation()=>ConfirmInstall(true);
 public Task CheckConfirmedTransfer()=>ConfirmInstall(true,true);
 async Task ConfirmInstall(bool check=false,bool accept=false)
 {
  if(!CanInstall || importing || confirming || selectionAnimating || TopLevel.GetTopLevel(this) is not Window owner)return;
  if(owner is ConnectionPreviewWindow navigating && navigating.PageTransitionBusy)return;
  confirming=true;
  var selectionPage=owner.FindControl<Viewbox>("SelectionPage")!;
  selectionPage.IsEnabled=false;
  using var cancellation=new CancellationTokenSource();preparation=cancellation;
  void Closed(object? sender,EventArgs args)=>cancellation.Cancel();
  owner.Closed+=Closed;
  int chosen=selected;
  try
  {
   string firmware=selected==0?UiText.Get("FirmwareChooser.Text.3612"):selected==1?UiText.Get("FirmwareChooser.Text.3645"):fileName??UiText.Get("FirmwareChooser.Text.3672");
   byte[]? image=null;FirmwareDescriptor? descriptor=null;
   if(!PreviewMode)
   {
    string path=selected==2?importedPath??throw new InvalidDataException(UiText.Get("FirmwareChooser.Text.3842")):Path.Combine(AppContext.BaseDirectory,"Firmware",selected==0?"r2.bin":"official.bin");
    if(chosen!=2 && !File.Exists(path))
    {
     using var client=FirmwarePreparation.CreateClient();
     image=await Task.Run(()=>new FirmwarePreparation(FirmwarePreparation.DefaultCache,client).PrepareAsync(chosen==0,
      text=>Dispatcher.UIThread.Post(()=>{if(!cancellation.IsCancellationRequested)SetStatus(text);}),cancellation.Token));
    }
    else image=await Task.Run(async()=>
    {
     await using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
     if(file.Length<1 || file.Length>FirmwareImport.MaximumLength)throw new InvalidDataException(UiText.Get("FirmwareChooser.Text.4178"));
     var content=new byte[(int)file.Length];await file.ReadExactlyAsync(content,cancellation.Token);return content;
    });
    descriptor=FirmwareCatalog.Analyze(image).Installable??throw new InvalidDataException(UiText.Get("FirmwareChooser.Text.4392"));
    if(selected==2 && descriptor.Sha256!=importedAnalysis?.Sha256)throw new InvalidDataException(UiText.Get("FirmwareChooser.Text.4513"));
    if(chosen!=2 && !descriptor.Sha256.Equals(chosen==0?FirmwarePreparation.ModifiedSha:FirmwarePreparation.OfficialSha,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException(UiText.Get("Firmware.PreparationRejected"));
    firmware=$"{descriptor.DisplayName} · {descriptor.DisplayVersion}";
   }
   cancellation.Token.ThrowIfCancellationRequested();
   var host=owner.FindControl<Grid>("ModalHost")!;
   var page=owner.FindControl<Viewbox>("SelectionPage")!;
   var chrome=owner.FindControl<Grid>("TopDragArea")!;
   var origin=action.TranslatePoint(new Point(),owner)??throw new InvalidOperationException("Source button is not arranged");
   var end=action.TranslatePoint(new Point(action.Bounds.Width,action.Bounds.Height),owner)??throw new InvalidOperationException("Source button is not arranged");
   var dialog=new FirmwareRiskWindow(firmware,selected==2,new Rect(origin,end),visible=>SetActionVisible(visible));
   if(descriptor is not null)dialog.FindControl<TextBlock>("RiskDetails")!.Text=UiText.Format("Firmware.RiskDetails",descriptor.DisplayName,descriptor.DisplayVersion,descriptor.ContainerVersion,descriptor.Length,descriptor.Sha256);
   host.Children.Add(dialog);host.IsVisible=true;page.IsEnabled=false;chrome.IsEnabled=false;
   bool confirmed;
   try{if(check){if(accept)await dialog.CheckConfirm();else await dialog.CheckCancel();}confirmed=await dialog.Result;
    if(confirmed && owner is ConnectionPreviewWindow preview)
    {
     if(PreviewMode)await preview.ShowTransfer(firmware,dialog,selected==1?1:selected==2?2:0);
     else await preview.BeginLiveUpgradeAsync(image!,dialog);
    }
   }
   finally{host.Children.Clear();host.IsVisible=false;page.IsEnabled=true;chrome.IsEnabled=true;}
   if(!confirmed)action.Focus();
  }
  catch(OperationCanceledException){if(!cancellation.IsCancellationRequested)SetStatus(UiText.Get("Firmware.PreparationFailed"));}
  catch(InvalidDataException e){SetStatus(e.Message);}
  catch(Exception){SetStatus(UiText.Get("Firmware.PreparationFailed"));}
  finally{owner.Closed-=Closed;preparation=null;confirming=false;selectionPage.IsEnabled=true;}
 }
 TextBlock Text(string value,double size){var t=new TextBlock{Text=value,FontSize=size,Foreground=Brush.Parse("#F4F3EC"),IsHitTestVisible=false};Children.Add(t);return t;}
 static void Put(Control c,double x,double y){SetLeft(c,x);SetTop(c,y);}
 public void Select(int index)
 {
  if(confirming || index<0||index>2 || (index==2&&fileName==null))return;
  from=(double[])current.Clone();startX=gx;startY=gy;selected=index;if(PreviewMode)TextMotion.Set(status,"");clock.Restart();selectionAnimating=true;Layout(0);
  int generation=++selectionGeneration;
  var top=TopLevel.GetTopLevel(this);
  if(top is null){selectionAnimating=false;Layout(1);return;}
  void Frame(TimeSpan _)
  {
   if(!selectionAnimating || generation!=selectionGeneration)return;
   Tick();if(selectionAnimating)top.RequestAnimationFrame(Frame);
  }
  top.RequestAnimationFrame(Frame);
 }
 async Task Import()
 {
  if(importing || confirming)return;importing=true;
  try
  {
   var top=TopLevel.GetTopLevel(this);if(top==null)return;
   var files=await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{Title=UiText.Get("FirmwareChooser.Text.7261"),AllowMultiple=false});
   if(files.Count==0)return;
   TextMotion.Set(status,UiText.Get("FirmwareChooser.Text.7349"));
   await using var input=await files[0].OpenReadAsync();
   var analysis=await Task.Run(()=>FirmwareImport.AnalyzeAsync(input,CancellationToken.None));
   importedPath=files[0].TryGetLocalPath();importedAnalysis=analysis;
   fileName=files[0].Name;versions[2].Text=fileName;versions[2].MaxWidth=650;versions[2].TextTrimming=TextTrimming.CharacterEllipsis;
   details[2].Text=analysis.Known is { } known?UiText.Format("Firmware.KnownFile",known.DisplayVersion):analysis.ThirdParty is not null?UiText.Get("Firmware.ThirdPartyValid"):UiText.Get("Firmware.StructureInvalid");
   titles[2].Text=analysis.Installable?.DisplayName??UiText.Get("FirmwareChooser.Text.3672");Select(2);
   TextMotion.Set(status,analysis.Installable is null?UiText.Format("Firmware.InstallationRejected",analysis.InstallationError??analysis.StructureError??UiText.Get("Firmware.StructureInvalid")):analysis.ThirdParty is not null?UiText.Get("Firmware.ThirdPartyWarning"):PreviewMode?UiText.Get("Firmware.PreviewVerified"):UiText.Get("Firmware.Verified"));
  }
  catch(InvalidDataException ex){TextMotion.Set(status,ex.Message);}
  catch(Exception){TextMotion.Set(status,UiText.Get("FirmwareChooser.Text.8273"));}
  finally{importing=false;}
 }
 void Tick()
 {
  double t=Math.Clamp(clock.Elapsed.TotalMilliseconds/280,0,1),e=1-Math.Pow(1-t,3);
  for(int i=0;i<3;i++)current[i]=from[i]+((i==selected?1:0)-from[i])*e;
  Layout(t);if(t>=1)selectionAnimating=false;
 }
 void Layout(double time)
 {
  double y=0,targetY=0;
  for(int i=0;i<3;i++)
  {
   double f=current[i],height=78+182*f;
   Put(cards[i],0,y);cards[i].Width=1268;cards[i].Height=height;cards[i].Opacity=f;
   Put(numbers[i],30,y+12+10*f);numbers[i].FontSize=39+36*f;
   Put(titles[i],136+56*f,y+8+22*f);titles[i].FontSize=26+10*f;
   Put(details[i],138+56*f,y+47+40*f);details[i].FontSize=15+5*f;details[i].MaxHeight=22+65*f;
   Put(versions[i],194,y+height-45);versions[i].Opacity=f;
   // Only the selected row relinquishes its old choice button; other rows remain visible.
   bool showChoice=i!=selected;
   Put(choices[i],1040,y+12);choices[i].Opacity=showChoice?1:0;choices[i].IsHitTestVisible=showChoice;
   if(i==2 && fileName==null){numbers[i].Opacity=f;titles[i].FontSize=16+20*f;details[i].Opacity=f;}
   if(i==selected)targetY=y+height-82;
   y+=height+14;
  }
  double e=1-Math.Pow(1-time,3);
  if(!selectionAnimating){gx=969;gy=targetY;}else{gx=startX+(969-startX)*e;gy=startY+(targetY-startY)*e;}
  // One moving plate stretches in the travel direction and settles at the target.
  double stretch=Math.Sin(time*Math.PI)*Math.Min(8,Math.Abs(targetY-startY)*.035);
  glass.Width=253-stretch*.35;glass.Height=64+stretch;Put(glass,gx+stretch*.175,gy-stretch/2);
  action.Width=253;action.Height=64;Put(action,gx,gy);action.IsEnabled=CanInstall && !importing && (time==0||time>=.92);
 }
 void CheckChoices()
 {
  for(int i=0;i<choices.Length;i++)
   if(choices[i].Opacity!=(i==selected?0:1) || choices[i].IsHitTestVisible!=(i!=selected))
    throw new InvalidOperationException("Only selected-row choice should be hidden");
 }
 public async Task CheckAnimation()
 {
  Select(1);
  CheckChoices();
  await Task.Delay(100);
  CheckChoices();
  await Task.Delay(300);
  if(current[1]<.999||selectionAnimating)throw new InvalidOperationException("Selection animation did not settle");
  if(choices[1].Opacity!=0 || choices[0].Opacity!=1 || choices[2].Opacity!=1)throw new InvalidOperationException("Settled choice visibility mismatch");
  Select(0);await Task.Delay(650);
  if(current[0]<.999)throw new InvalidOperationException("Selection return did not settle");
 }
}
