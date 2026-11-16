// SPDX-License-Identifier: GPL-3.0-or-later
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
using OmenTools;
using Lumina.Excel.Sheets;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using KamiToolKit.Enums;
namespace OmniToolbox.TreePublic;
public sealed partial class ToolbarRenderer
{
    private sealed unsafe partial class NativeRuntimePopup
    {
        private float quickCommandWidth=240;
        private ResNode? quickCommands;
        private CollisionNode? quickMenuCollision;
        private uint queuedQuickCategory;
        private uint selectedQuickCategory;
        private float quickSecondaryHeight;

        private void SetupQuickCommandMenu(AtkUnitBase* addon)
        {
            // Keep the native window's collision owner alive and visible. Hide
            // only its decoration; hiding the owner also disables UI blocking.
            if(WindowNode is WindowNode window){
                window.IsVisible=true;window.BackgroundTextureNode.IsVisible=false;
                window.BorderTextureNode.IsVisible=false;window.BackgroundImageNode.IsVisible=false;
                window.HeaderContainerNode.IsVisible=false;window.HeaderCollisionNode.IsVisible=false;
            }
            selectedQuickCategory=0;quickSecondaryHeight=0;
            quickMenuCollision=new CollisionNode{Size=Size,NodeFlags=NodeFlags.Visible|NodeFlags.Enabled|NodeFlags.HasCollision|NodeFlags.RespondToMouse|NodeFlags.EmitsEvents|NodeFlags.Focusable};
            quickMenuCollision.AttachNode(addon);
            foreach(var type in new[]{AtkEventType.MouseDown,AtkEventType.MouseUp,AtkEventType.MouseClick})
                quickMenuCollision.AddEvent(type,(listener,eventType,param,ev,data)=>{if(ev!=null)ev->SetEventIsHandled(true);});
            addon->WindowCollisionNode=quickMenuCollision;
            addon->FocusNode=quickMenuCollision;
            addon->WindowHeaderCollisionNode=null;
            SelectQuickCommandCategory(Math.Clamp(owner.quickCommandCategory,1u,7u));
        }
        private static void AddQuickMenuBackground(ResNode panel)
        {
            // Match KamiToolKit DropDownNode's native popup skin, including
            // its slice offsets. WindowH is a different, rounder context skin.
            new SimpleNineGridNode{
                TexturePath="ui/uld/ListB.tex",TextureCoordinates=Vector2.Zero,TextureSize=new(32),
                TopOffset=10,BottomOffset=12,LeftOffset=10,RightOffset=10,Size=panel.Size,
            }.AttachNode(panel);
        }
        private ListButtonNode QuickMenuRow(ResNode parent,Vector2 position,float width,float height,string label,System.Action click)
        {
            void Invoke(){if(Finished||owner.activePopup!=PopupKind.Widget||owner.activeWidgetPopupType!=MultiToolbarWidgetType.QuickCommands)return;Run(click);}
            var button=new ListButtonNode{Position=position,Size=new(width,height),String=label,OnClick=Invoke};button.AttachNode(parent);
            button.CollisionNode.AddEvent(AtkEventType.MouseClick,(listener,type,param,ev,data)=>{if(ev!=null)ev->SetEventIsHandled(true);if(data!=null&&data->MouseData.ButtonId==0)Invoke();});
            button.CollisionNode.AddEvent(AtkEventType.MouseDown,(listener,type,param,ev,data)=>{if(ev!=null)ev->SetEventIsHandled(true);button.Timeline?.PlayAnimation(3);});
            button.CollisionNode.AddEvent(AtkEventType.MouseUp,(listener,type,param,ev,data)=>{if(ev!=null)ev->SetEventIsHandled(true);button.Timeline?.PlayAnimation(2);});
            button.CollisionNode.AddEvent(AtkEventType.MouseOver,()=>button.Timeline?.PlayAnimation(2));
            button.CollisionNode.AddEvent(AtkEventType.MouseOut,()=>button.Timeline?.PlayAnimation(4));
            button.LabelNode.Position=new(10,0);button.LabelNode.Size=new(width-16,height);
            button.LabelNode.FontSize=14;button.LabelNode.TextFlags=TextFlags.Edge|TextFlags.Ellipsis;
            return button;
        }
        internal void QueueQuickCommandCategory(uint id,Vector2 target)
        {
            if(id is <1 or >7||Finished)return;
            queuedQuickCategory=id;anchor=target;
        }
        private void ApplyQuickCommandCategory()
        {
            if(queuedQuickCategory==0)return;
            uint selected=queuedQuickCategory;queuedQuickCategory=0;
            SelectQuickCommandCategory(selected);PositionQuickCommandMenu();
        }
        private void SelectQuickCommandCategory(uint id)
        {
            if(Finished||selectedQuickCategory==id)return;
            selectedQuickCategory=id;quickCommands?.Dispose();quickCommands=null;
            AtkUnitBase* addon=this;if(addon==null)return;
            var commands=DService.Instance().Data.GetExcelSheet<MainCommand>().Where(c=>c.RowId!=0&&c.MainCommandCategory.RowId==id&&!string.IsNullOrWhiteSpace(c.Name.ToString())).OrderBy(c=>c.SortID).ToArray();
            var captions=commands.Select(c=>QuickCommandCaption(c.Name.ToString())).ToArray();
            var title=DService.Instance().Data.GetExcelSheet<MainCommandCategory>().GetRow(id).Name;
            var header=new TextNode{String=title,FontSize=14};
            float measured=header.GetTextDrawSize(false).X;
            foreach(var caption in captions)measured=Math.Max(measured,header.GetTextDrawSize(caption,false).X);
            quickCommandWidth=Math.Clamp(measured+24,190,200);
            var screen=AtkStage.Instance()->ScreenSize;
            quickSecondaryHeight=Math.Min(commands.Length*22+36,Math.Max(80,screen.Height-16));
            quickCommands=new ResNode{Size=new(quickCommandWidth,quickSecondaryHeight)};quickCommands.AttachNode(addon);
            AddQuickMenuBackground(quickCommands);
            header.Position=new(8,3);header.Size=new(quickCommandWidth-16,24);header.TextFlags=TextFlags.Edge;header.AttachNode(quickCommands);
            // Divider from the same native popup atlas; do not hand-paint a
            // title band or border that differs from the active game theme.
            new SimpleImageNode{
                TexturePath="ui/uld/ListB.tex",TextureCoordinates=new(0,58),TextureSize=new(32,5),
                ImageNodeFlags=0,Position=new(5,28),Size=new(32,5),Scale=new((quickCommandWidth-10)/32,1f/5),
            }.AttachNode(quickCommands);
            var scroll=new ScrollingNode<ResNode>{Position=new(4,31),Size=new(quickCommandWidth-8,quickSecondaryHeight-36),ScrollSpeed=22};scroll.AttachNode(quickCommands);
            for(int index=0;index<commands.Length;index++){
                var command=commands[index];uint commandId=command.RowId;
                QuickMenuRow(scroll.ContentNode,new(0,index*22),scroll.Width-14,22,captions[index],()=>{
                    var ui=UIModule.Instance();if(ui==null)return;ui->ExecuteMainCommand(commandId);owner.ClosePopup();
                });
            }
            scroll.ContentNode.Height=commands.Length*22;scroll.RecalculateSizes();PositionQuickCommandMenu();
        }
        private static string QuickCommandCaption(string name)
        {
            var input=name switch{
                "切换行动状态"=>InputId.SWARD,"技能菜单"=>InputId.MENU_ACTION,
                "冒险者铭牌"=>InputId.MENU_CHARA_CARD,"肖像列表"=>InputId.MENU_BANNER_LIST,
                "货币一览"=>InputId.MENU_CURRENCY,"角色"=>InputId.MENU_STATUS,
                "兵装库"=>InputId.MENU_ARMORY,"物品" or "背包"=>InputId.MENU_BAG,
                "陆行鸟鞍囊"=>InputId.MENU_BUDDY_BAG,"搭档"=>InputId.MENU_BUDDY,
                "坐骑一览"=>InputId.MENU_MOUNT_NOTEBOOK,"宠物一览"=>InputId.MENU_MINION_NOTEBOOK,
                "面部配饰"=>InputId.MENU_FACE_ACCESSORY,"时尚配饰"=>InputId.MENU_ORNAMENT_NOTEBOOK,
                "青魔法书"=>InputId.MENU_AOZ,"魔晶器图鉴"=>InputId.MENU_MCGUFFIN,
                "对战资料"=>InputId.MENU_PVP_PROFILE,"金碟游乐场"=>InputId.MENU_GOLD_SAUCER,
                "成就"=>InputId.MENU_ACHIVE,"讨伐笔记"=>InputId.MENU_MONSTER,
                "探索笔记"=>InputId.MENU_ADVENTURE_NOTEBOOK,"制作笔记"=>InputId.MENU_CRAFT,
                "采集笔记"=>InputId.MENU_GATHER,"钓鱼笔记"=>InputId.MENU_FISH,
                "鱼类图鉴"=>InputId.MENU_FISH_GUIDE,"管弦乐琴乐谱集"=>InputId.MENU_ORCHESTRION,
                "挑战笔记"=>InputId.MENU_CONTENTS_NOTEBOOK,"地图"=>InputId.MENU_MAP,
                "任务情报"=>InputId.MENU_JOURNAL,"系统设置"=>InputId.MENU_SYSCONFIG,
                "角色设置"=>InputId.MENU_CONFIG,"键位设置"=>InputId.MENU_KEYBIND,
                "用户宏"=>InputId.MENU_MACRO,"场景标记"=>InputId.MENU_FIELD_MARK,
                "目标标记"=>InputId.MENU_MARK,_=>InputId.NotFound};
            var manager=UIInputData.Instance();if(manager==null||input==InputId.NotFound)return name;
            var binding=manager->GetKeybind(input);if(binding==null)return name;
            var key=binding->KeySettings[0];if(key.Key==SeVirtualKey.NO_KEY)key=binding->KeySettings[1];
            if(key.Key==SeVirtualKey.NO_KEY)return name;
            string code=key.Key.ToString().Replace("KEY_","");
            string prefix=(key.KeyModifier.HasFlag(KeyModifierFlag.Ctrl)?"Ctrl+":"")+(key.KeyModifier.HasFlag(KeyModifierFlag.Alt)?"Alt+":"")+(key.KeyModifier.HasFlag(KeyModifierFlag.Shift)?"Shift+":"");
            return $"{name} [{prefix}{code}]";
        }
        private void PositionQuickCommandMenu()
        {
            AtkUnitBase* addon=this;if(addon==null||quickCommands==null)return;
            var screen=AtkStage.Instance()->ScreenSize;
            float height=quickSecondaryHeight,width=quickCommandWidth;
            Size=new(width,height);
            bool bottom=owner.config.Alignment==MultiToolbarAlignment.Bottom;
            quickCommands.Position=Vector2.Zero;
            if(quickMenuCollision!=null)quickMenuCollision.Size=Size;
            addon->SetPosition((short)Math.Clamp(anchor.X,0,Math.Max(0,screen.Width-width)),(short)Math.Clamp(bottom?anchor.Y-height:anchor.Y,0,Math.Max(0,screen.Height-height)));
            addon->UpdateCollisionNodeList(false);
        }
    }
}
