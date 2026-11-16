// SPDX-License-Identifier: GPL-3.0-or-later
using Yanjiu.NativeUI;
namespace Yanjiu.Modules;
public sealed unsafe partial class AutoRetainer
{
    public NativePanel CreateNativePanel()
    {
        return new NativePanel("AutoRetainer","自动雇员",[
            new NativeLabel("运行状态",()=>status),
            new NativeToggle("上架自动填写市场价格",()=>config.AutoPriceOnListing,value=>config.AutoPriceOnListing=value),
            new NativeToggle("打开雇员铃自动收取",()=>config.AutoCollect,value=>config.AutoCollect=value),
            new NativeToggle("自动上架超额水晶",()=>config.AutoCrystals,value=>config.AutoCrystals=value),
            new NativeToggle("自动任务之后改价",()=>config.AutoReprice,value=>config.AutoReprice=value),
            new NativeNumber("降低金币",()=>config.Undercut,value=>config.Undercut=(int)value,0,999999999),
            new NativeNumber("最低售价",()=>config.MinimumPrice,value=>config.MinimumPrice=(int)value,1,999999999),
            new NativeNumber("最大降幅百分比",()=>config.MaxDropPercent,value=>config.MaxDropPercent=(int)value,0,100),
            new NativeNumber("水晶保留数量",()=>config.CrystalKeep,value=>config.CrystalKeep=(int)value,1,9999),
            new NativeNumber("水晶上架单价",()=>config.CrystalPrice,value=>config.CrystalPrice=(int)value,1,999999999)
        ]);
    }
}
