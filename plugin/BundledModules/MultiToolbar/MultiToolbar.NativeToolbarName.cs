// SPDX-License-Identifier: GPL-3.0-or-later
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace OmniToolbox.TreePublic;
public sealed partial class MultiToolbar
{
    private sealed unsafe class NativeToolbarName:NativeAddon
    {
        private string name;
        private readonly System.Action<string> save;
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        internal NativeToolbarName(string name,System.Action<string> save){this.name=name;this.save=save;InternalName="YanjiuToolbarRename";Title="重命名工具栏";Size=new(420,170);}
        protected override void OnSetup(AtkUnitBase* addon,Span<AtkValue> args){
            new TextInputNode{Position=ContentStartPosition,Size=new(360,28),String=name,MaxCharacters=80,OnInputReceived=value=>name=value.ToString()}.AttachNode(addon);
            var status=new TextNode{Position=ContentStartPosition+new System.Numerics.Vector2(0,75),Size=new(360,26),FontSize=12};status.AttachNode(addon);
            new TextButtonNode{Position=ContentStartPosition+new System.Numerics.Vector2(0,38),Size=new(130,28),String="保存名称",OnClick=()=>{try{if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("名称不能为空。");save(name.Trim());Close();}catch(Exception error){status.String=error.GetBaseException().Message;}}}.AttachNode(addon);
        }
    }
}
