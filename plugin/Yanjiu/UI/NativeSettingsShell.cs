// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
namespace Yanjiu.UI;

/// <summary>Native chrome only. Module controls remain in the transparent ImGui body.</summary>
internal sealed unsafe class NativeSettingsShell : NativeAddon
{
    internal bool Finalized {get;private set;}
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    internal NativeSettingsShell()
    {
        InternalName="YanjiuModuleSettingsShell";Title="模块设置";
        Size=new(1050,650);
    }
    internal bool TryGetBody(out Vector2 position,out Vector2 size,out float scale)
    {
        position=size=default;scale=1;
        if(Finalized||!IsOpen||InternalAddon==null||InternalAddon->RootNode==null)return false;
        var root=InternalAddon->RootNode;
        scale=InternalAddon->Scale;
        if(!float.IsFinite(scale)||scale<=0)return false;
        position=new Vector2(root->X,root->Y)+ContentStartPosition*scale;
        size=ContentSize*scale;
        return size.X>1&&size.Y>1;
    }
    protected override void OnSetup(AtkUnitBase* addon,Span<AtkValue> values)=>Finalized=false;
    protected override void OnFinalize(AtkUnitBase* addon)=>Finalized=true;
}
