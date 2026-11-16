// SPDX-License-Identifier: GPL-3.0-or-later
using KamiToolKit.Nodes;
namespace Yanjiu.UI;
internal sealed unsafe partial class NativeMainWindow
{
    private readonly List<(TextNode Label,NativeActionButton Button,NativeActionButton Secondary)> sourceRows=[];
    private MainWindow.NativeSourceAction[] sourceActions=[];
    private TextNode? sourceStatus;
    private ScrollingNode<ResNode>? sourceScroll;
    private MainWindow.NativeSourceAction[] allSourceActions=[];
    private void SetupSourceRows(ResNode root)
    {
        sourceRows.Clear();
        sourceScroll=new ScrollingNode<ResNode>{Position=new(0,82),Size=new(745,410),ScrollSpeed=36};sourceScroll.AttachNode(root);
        for(int i=0;i<9;i++){
            int index=i;
            var label=new TextNode{Position=new(0,i*52),Size=new(655,40),FontSize=13};label.AttachNode(sourceScroll.ContentNode);
            var button=new NativeActionButton{Position=new(664,i*52),Size=new(28,28),OnClick=()=>{
                if(index>=sourceActions.Length)return;
                try{sourceActions[index].Run();}catch(Exception e){Services.Log.Error(e,"Native source action failed");}
                nextRefresh=0;
            }};button.AttachNode(sourceScroll.ContentNode);            var secondary=new NativeActionButton{Position=new(704,i*52),Size=new(28,28),OnClick=()=>{
                if(index>=sourceActions.Length)return;
                try{sourceActions[index].SecondaryRun();}catch(Exception e){Services.Log.Error(e,"Native source removal failed");}
                nextRefresh=0;
            }};secondary.AttachNode(sourceScroll.ContentNode);sourceRows.Add((label,button,secondary));
        }
        sourceStatus=new TextNode{Position=new(0,500),Size=new(730,70),FontSize=12};sourceStatus.AttachNode(root);
    }
    private void RefreshSources()
    {
        var actions=controller.NativeSourceActions();
        allSourceActions=actions;
        if(sourceScroll!=null){var height=Math.Max(410,actions.Length*52);if(sourceScroll.ContentNode.Height!=height){sourceScroll.ContentNode.Height=height;sourceScroll.RecalculateSizes();}}
        BindVisibleSources();
        if(sourceStatus!=null)sourceStatus.String=controller.NativeStatus;
    }
    private void BindVisibleSources()
    {
        if(sourceScroll==null)return;
        int first=Math.Max(0,(int)(-sourceScroll.ContentNode.Y/52));
        sourceActions=allSourceActions.Skip(first).Take(9).ToArray();
        for(int i=0;i<sourceRows.Count;i++){
            var row=sourceRows[i];bool visible=i<sourceActions.Length;
            row.Label.IsVisible=row.Button.IsVisible=row.Secondary.IsVisible=visible;
            if(visible){row.Label.Position=new(0,(first+i)*52);row.Button.Position=new(664,(first+i)*52);row.Secondary.Position=new(704,(first+i)*52);row.Label.String=sourceActions[i].Detail;row.Button.String="↻";row.Button.TextTooltip=sourceActions[i].Label;row.Secondary.String="×";row.Secondary.TextTooltip=sourceActions[i].SecondaryLabel;row.Label.TextTooltip=sourceActions[i].Detail;}
        }
    }
}
