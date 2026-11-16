// SPDX-License-Identifier: GPL-3.0-or-later
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using OmenTools;
namespace OmniToolbox.TreePublic;
public sealed partial class ToolbarRenderer
{
    private NativeIconPicker? nativeIconPicker;
    private void OpenNativeIconPicker(MultiToolbarWidgetConfig target)
    {
        nativeIconPicker?.Dispose();nativeIconPicker=new(this,target);nativeIconPicker.Open();
    }
    private sealed unsafe class NativeIconPicker:NativeAddon
    {
        private readonly ToolbarRenderer owner;
        private readonly MultiToolbarWidgetConfig target;
        private readonly List<uint> available=[];
        private readonly List<uint> filtered=[];
        private int indexedCount;private string indexedSearch="";
        private readonly List<(TextButtonNode Button,IconImageNode Icon,TextNode Number)> cells=[];
        private ScrollingNode<ResNode>? scroll;
        private TextNode? status;
        private TextInputNode? number;
        private IconImageNode? preview;
        private int tab,rangeIndex,nextId;
        private bool complete,closed;
        private string search="";
        private uint chosen;
        private const int Columns=10,Cell=54;
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        internal NativeIconPicker(ToolbarRenderer owner,MultiToolbarWidgetConfig target)
        {
            this.owner=owner;this.target=target;chosen=target.GameIconID;
            InternalName="YanjiuToolbarIconPicker_"+owner.barID;Title="选择游戏图标";Size=new(610,620);
        }
        private bool Valid()=>!closed&&owner.CollectionConfig.Widgets.Concat(owner.CollectionConfig.Commands).Concat(owner.CollectionConfig.AuxiliaryBars.SelectMany(bar=>bar.Widgets.Concat(bar.Commands))).Contains(target);
        private void Apply(uint id){if(!Valid())return;chosen=id;target.GameIconID=id;target.ShowIcon=id!=0;target.IconFilePath="";target.IconGlyph="";owner.SaveNativeConfiguration();}
        protected override void OnSetup(AtkUnitBase* addon,Span<AtkValue> args)
        {
            closed=false;cells.Clear();
            var options=Enumerable.Range(0,GameIconTabs.Length).ToList();
            new DropDownNode<int>{Position=ContentStartPosition,Size=new(220,28),GetLabelFunction=index=>TabName(GameIconTabs[index].LabelKey),Options=options,SelectedOption=0,OnOptionSelected=value=>Reset(value)}.AttachNode(addon);
            new TextInputNode{Position=ContentStartPosition+new System.Numerics.Vector2(230,0),Size=new(320,28),PlaceholderString="按图标编号筛选",OnInputReceived=value=>{search=value.ToString();scroll?.ScrollToStart();Bind();}}.AttachNode(addon);
            scroll=new ScrollingNode<ResNode>{Position=ContentStartPosition+new System.Numerics.Vector2(0,42),Size=new(570,432),ScrollSpeed=54};scroll.AttachNode(addon);
            for(int i=0;i<100;i++){
                var button=new TextButtonNode{Size=new(44,44),String=""};button.AttachNode(scroll.ContentNode);
                var icon=new IconImageNode{FitTexture=true,Size=new(32,32)};icon.AttachNode(button);
                var label=new TextNode{Position=new(0,34),Size=new(44,16),FontSize=10,AlignmentType=AlignmentType.Center};label.AttachNode(button);
                cells.Add((button,icon,label));
            }
            preview=new IconImageNode{IconId=chosen,FitTexture=true,Position=ContentStartPosition+new System.Numerics.Vector2(0,490),Size=new(28,28),IsVisible=chosen!=0};preview.AttachNode(addon);
            number=new TextInputNode{Position=ContentStartPosition+new System.Numerics.Vector2(40,490),Size=new(180,28),String=chosen.ToString(),OnInputReceived=value=>{if(uint.TryParse(value.ToString(),out var id)){chosen=id;preview.IconId=id;preview.FitTexture=true;preview.Size=new(28,28);preview.IsVisible=id!=0;}}};number.AttachNode(addon);
            new TextButtonNode{Position=ContentStartPosition+new System.Numerics.Vector2(240,490),Size=new(120,28),String="使用此编号",OnClick=()=>Apply(chosen)}.AttachNode(addon);
            new TextButtonNode{Position=ContentStartPosition+new System.Numerics.Vector2(380,490),Size=new(120,28),String="不显示图标",OnClick=()=>{Apply(0);number.String="0";preview.IsVisible=false;}}.AttachNode(addon);
            status=new TextNode{Position=ContentStartPosition+new System.Numerics.Vector2(0,530),Size=new(560,28),FontSize=12};status.AttachNode(addon);Reset(0);
        }
        private void Reset(int index)
        {
            tab=index;available.Clear();filtered.Clear();indexedCount=0;indexedSearch=search;rangeIndex=0;complete=false;nextId=GameIconTabs[tab].Ranges.FirstOrDefault().Start;scroll?.ScrollToStart();
            if(GameIconTabs[tab].LabelKey=="IconBrowser.Tab.MapSymbols"){
                available.AddRange(DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.MapSymbol>().Where(row=>row.Icon!=0).Select(row=>(uint)row.Icon).Distinct());complete=true;
            }
            Bind();
        }
        private static bool Exists(uint id)
        {
            uint folder=id/1000;var data=DService.Instance().Data;
            return data.FileExists($"ui/icon/{folder:D3}000/{id:D6}.tex")||data.FileExists($"ui/icon/{folder:D3}000/{id:D6}_hr1.tex")||data.FileExists($"ui/icon/{folder:D3}000/en/{id:D6}.tex")||data.FileExists($"ui/icon/{folder:D3}000/en/{id:D6}_hr1.tex");
        }
        private void Scan()
        {
            var ranges=GameIconTabs[tab].Ranges;long until=System.Diagnostics.Stopwatch.GetTimestamp()+Math.Max(1,System.Diagnostics.Stopwatch.Frequency/1000);
            int budget=128;
            while(!complete&&budget-->0&&System.Diagnostics.Stopwatch.GetTimestamp()<until){
                if(rangeIndex>=ranges.Length){complete=true;break;}
                var range=ranges[rangeIndex];if(nextId<range.Start)nextId=range.Start;
                if(nextId>=range.End){rangeIndex++;nextId=rangeIndex<ranges.Length?ranges[rangeIndex].Start:0;continue;}
                uint id=(uint)nextId++;if(Exists(id))available.Add(id);
            }
        }
        private void Bind()
        {
            if(scroll==null)return;
            if(indexedSearch!=search){filtered.Clear();indexedCount=0;indexedSearch=search;}
            for(int i=indexedCount;i<available.Count;i++){uint id=available[i];if(search.Length==0||id.ToString().Contains(search,StringComparison.Ordinal))filtered.Add(id);}indexedCount=available.Count;
            int height=Math.Max(432,((filtered.Count+Columns-1)/Columns)*Cell);
            if(scroll.ContentNode.Height!=height){scroll.ContentNode.Height=height;scroll.RecalculateSizes();}
            int first=Math.Max(0,(int)(-scroll.ContentNode.Y/Cell))*Columns;
            for(int i=0;i<cells.Count;i++){
                var cell=cells[i];int index=first+i;cell.Button.IsVisible=index<filtered.Count;if(index>=filtered.Count)continue;
                uint id=filtered[index];cell.Button.Position=new((index%Columns)*Cell,(index/Columns)*Cell);cell.Button.TextTooltip=id.ToString();
                if(cell.Icon.IconId!=id)cell.Icon.IconId=id;cell.Icon.FitTexture=true;cell.Icon.Size=GameIconTabs[tab].StatusIcons?new(24,32):new(32,32);cell.Icon.Position=new(6,0);
                cell.Number.String=id.ToString();cell.Button.OnClick=()=>{Apply(id);number!.String=id.ToString();preview!.IconId=id;preview.FitTexture=true;preview.Size=new(28,28);preview.IsVisible=true;};
            }
            if(status!=null)status.String=Valid()?$"{filtered.Count} 个图标"+(complete?"":" · 正在读取…"):"原组件已移除，请关闭此窗口。";
        }
        protected override void OnUpdate(AtkUnitBase* addon){if(!complete)Scan();Bind();}
        protected override void OnFinalize(AtkUnitBase* addon){closed=true;cells.Clear();scroll=null;base.OnFinalize(addon);}
        private static string TabName(string key)=>key switch{
            "IconBrowser.Tab.Featured"=>"常用","IconBrowser.Tab.Misc"=>"杂项","IconBrowser.Tab.Misc2"=>"杂项 2","IconBrowser.Tab.Actions"=>"技能","IconBrowser.Tab.MountsMinions"=>"坐骑与宠物","IconBrowser.Tab.Artwork"=>"插画","IconBrowser.Tab.StatusEffects"=>"状态","IconBrowser.Tab.Garbage"=>"其他资源","IconBrowser.Tab.Spoilers"=>"扩展资源","IconBrowser.Tab.Spoilers2"=>"扩展资源 2","IconBrowser.Tab.MapSymbols"=>"地图符号",_=>"未分类"
        };
        private readonly record struct IconRange(int Start,int End);
        private sealed record IconTabDefinition(string LabelKey,IconRange[] Ranges,bool StatusIcons=false);

    private static readonly IconTabDefinition[] GameIconTabs =
    [
        new("IconBrowser.Tab.Featured",
        [
            new(0, 100),
            new(62_000, 62_600),
            new(62_800, 62_900),
            new(66_000, 66_400),
            new(90_000, 100_000),
            new(114_000, 114_100),
            new(230_850, 231_000),
            new(10_000_000, 10_003_000)
        ]),
        new("IconBrowser.Tab.Misc",
        [
            new(60_000, 61_000),
            new(61_200, 61_250),
            new(61_290, 62_000),
            new(62_600, 62_620),
            new(63_900, 64_000),
            new(64_500, 64_550),
            new(65_000, 65_900),
            new(180_000, 180_060),
            new(230_000, 230_850),
            new(231_000, 240_000)
        ]),
        new("IconBrowser.Tab.Misc2",
        [
            new(62_900, 63_200),
            new(63_875, 63_900),
            new(65_900, 66_000),
            new(66_400, 66_500),
            new(67_000, 68_000),
            new(70_120, 70_200),
            new(70_500, 70_960),
            new(70_960, 71_450),
            new(72_000, 72_500),
            new(72_500, 72_620),
            new(76_000, 76_200),
            new(80_000, 80_200),
            new(80_730, 81_000),
            new(82_000, 82_100),
            new(82_270, 82_325),
            new(83_000, 84_000),
            new(180_060, 180_100),
            new(240_000, 241_000)
        ]),
        new("IconBrowser.Tab.Actions",
        [
            new(100, 4_000),
            new(5_100, 8_000),
            new(8_000, 9_000),
            new(9_000, 10_000),
            new(19_600, 19_800),
            new(19_800, 20_000),
            new(61_250, 61_290),
            new(64_200, 64_325),
            new(64_550, 64_600),
            new(64_600, 64_800),
            new(64_800, 65_000),
            new(70_000, 70_120),
            new(82_200, 82_270),
            new(246_000, 250_000)
        ]),
        new("IconBrowser.Tab.MountsMinions",
        [
            new(4_000, 4_400),
            new(4_400, 5_100),
            new(59_000, 59_400),
            new(59_400, 60_000),
            new(68_000, 68_400),
            new(68_400, 69_000)
        ]),
        new("IconBrowser.Tab.Items",
        [
            new(20_000, 30_000),
            new(50_000, 54_000),
            new(58_000, 59_000)
        ]),
        new("IconBrowser.Tab.Equipment",
        [
            new(30_000, 50_000),
            new(54_000, 54_225),
            new(54_225, 54_400),
            new(54_400, 58_000),
            new(200_000, 210_000)
        ]),
        new("IconBrowser.Tab.Artwork",
        [
            new(130_000, 142_000),
            new(250_000, 251_004),
            new(251_600, 251_602),
            new(251_700, 251_704)
        ]),
        new("IconBrowser.Tab.StatusEffects", [new(210_000, 230_000)], true),
        new("IconBrowser.Tab.Garbage",
        [
            new(61_000, 61_100),
            new(62_620, 62_800),
            new(63_200, 63_875),
            new(66_500, 67_000),
            new(69_000, 70_000),
            new(70_200, 70_500),
            new(71_450, 71_500),
            new(78_000, 80_000),
            new(80_200, 80_730),
            new(81_000, 82_000),
            new(82_100, 82_200),
            new(84_000, 85_000),
            new(85_000, 87_000),
            new(150_000, 170_000),
            new(190_000, 200_000),
            new(241_000, 241_200)
        ]),
        new("IconBrowser.Tab.Spoilers",
        [
            new(87_000, 90_000),
            new(72_620, 76_000),
            new(120_000, 130_000),
            new(142_000, 150_000),
            new(181_000, 181_500)
        ]),
        new("IconBrowser.Tab.Spoilers2",
        [
            new(71_500, 72_000),
            new(100_000, 114_000),
            new(114_100, 120_000)
        ]),
        new("IconBrowser.Tab.MapSymbols", []),
        new("IconBrowser.Tab.Uncategorized",
        [
            new(10_000, 19_600),
            new(61_100, 61_200),
            new(64_000, 64_200),
            new(64_325, 64_500),
            new(76_200, 78_000),
            new(82_325, 83_000),
            new(181_500, 190_000),
            new(241_200, 246_000),
            new(251_004, 251_600),
            new(251_602, 251_700),
            new(251_704, 350_000)
        ])
    ];
    }
}
