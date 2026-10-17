// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using OmenTools.Extensions;
namespace DailyRoutines.Common.Info;
/// <summary>Reads color IDs from the active game's own UI palette.</summary>
public static class AtkColors
{
    public static AtkColor ListHeader{get;}=new(21,7);public static AtkColor ListRow{get;}=new(8,7);public static AtkColor ListFooter{get;}=new(3,7);
    public static AtkColor Label{get;}=new(8,7);public static AtkColor Text{get;}=new(2,7);public static AtkColor Hint{get;}=new(3,7);
    public static AtkColor LabelLight{get;}=new(3,7);public static AtkColor Value{get;}=new(50,32);public static AtkColor ValueEmphasize{get;}=new(64,65);public static AtkColor TargetText{get;}=new(1,23);
    public readonly unsafe struct AtkColor(uint textColor,uint edgeColor)
    {
        public readonly uint TextColor=textColor,EdgeColor=edgeColor;
        private static Vector4 Read(uint id){var stage=AtkStage.Instance();if(stage==null||stage->AtkUIColorHolder==null)throw new InvalidOperationException("游戏UI颜色表尚未就绪");return stage->AtkUIColorHolder->GetColor(true,id).ToVector4();}
        public Vector4 GetTextColor()=>Read(TextColor);public Vector4 GetEdgeColor()=>Read(EdgeColor);
        public void ApplyTo(object node){
            ArgumentNullException.ThrowIfNull(node);
            var type=node.GetType();var text=type.GetProperty("TextColor")??throw new MissingMemberException(type.FullName,"TextColor");
            var edge=type.GetProperty("TextOutlineColor")??throw new MissingMemberException(type.FullName,"TextOutlineColor");
            text.SetValue(node,GetTextColor());edge.SetValue(node,GetEdgeColor());
        }
    }
}
