// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using KamiToolKit.BaseTypes;
namespace Yanjiu.Modules;
public sealed unsafe partial class AutoRetainer
{
 private sealed class NativeRetainerPanel : NativeAddon
 {
  private readonly AutoRetainer module;
  private TextButtonNode[] buttons=[];
  private TextButtonNode? stopButton;
  private TextNode? statusText;
  [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
  public NativeRetainerPanel(AutoRetainer owner)
  {
   module=owner;InternalName="YanjiuAutoRetainerNative";Title="自动雇员";Size=new Vector2(280,350);
  }
  protected override void OnSetup(AtkUnitBase* addon,Span<AtkValue> values)
  {
   var list=new VerticalListNode{Position=ContentStartPosition,Size=new(ContentSize.X,0),FitContents=true,FitWidth=true,ItemSpacing=4};
   var actions=new (string Title,Action Run)[]{("自动收取雇员",()=>module.Start(false)),("自动道具合并递交",()=>module.StartInventory(false)),("自动取出雇员金币",()=>module.Start(true)),("自动雇员改价",module.StartReprice),("上架水晶",()=>module.StartInventory(true))};
   var nodes=new List<TextButtonNode>();
   foreach(var action in actions){
    var button=new TextButtonNode{String="开始",Size=new(ContentSize.X,36),OnClick=()=>{try{if(module.steps.Count>0||module.pendingJobs.Count>0)module.Stop("已停止。");else action.Run();}catch(Exception e){module.Stop(e.Message);}}};
    nodes.Add(button);
    list.AddNode(button);button.String=action.Title;
   }
   stopButton=new TextButtonNode{String="停止",Size=new(ContentSize.X,28),OnClick=()=>module.Stop("已停止，不再执行后续操作。")};
   list.AddNode(stopButton);
   statusText=new TextNode{String=module.status,Size=new(ContentSize.X,42),FontSize=12,TextFlags=TextFlags.WordWrap|TextFlags.MultiLine};
   list.AddNode(statusText);
   buttons=nodes.ToArray();list.AttachNode(addon);
  }
  protected override void OnUpdate(AtkUnitBase* addon)
  {
   bool busy=module.steps.Count>0||module.pendingJobs.Count>0||module.listingVisible&&!module.listingFinished;
   foreach(var button in buttons)button.IsEnabled=!busy;
   if(stopButton!=null)stopButton.IsEnabled=busy;
   if(statusText!=null)statusText.String=module.status;
   var host=Addon("RetainerList");
   if(host!=null&&host->IsVisible)SetWindowPosition(new Vector2(Math.Max(0,host->X-Size.X*host->Scale-4),Math.Max(0,(int)host->Y)));
   base.OnUpdate(addon);
  }
 }
}


