// SPDX-License-Identifier: GPL-3.0-or-later
using OmenTools.Dalamud;
namespace DailyRoutines.Extensions;
public sealed record YanjiuRaphaelResult(uint RequestID,RaphaelCalculationStatus Status,string ErrorMessage,List<uint> Actions,int FinalProgress,int FinalQuality);
/// <summary>Public six-field Raphael result contract, independent of the pinned Omen bootstrap.</summary>
public static class RaphaelCalculationResults
{
    public static YanjiuRaphaelResult GetCalculationStatus(uint requestID){
        try{
            var value=Yanjiu.Services.PluginInterface.GetIpcSubscriber<uint,Tuple<uint,string,string,List<uint>,int,int>>("Raphael.Dalamud.GetCalculationStatus").InvokeFunc(requestID);
            return Decode(requestID,value);
        }catch(Exception e){
            return Failed(requestID,"无法读取 Raphael 计算结果，请确认已安装提供进度和品质结果接口的版本："+e.GetBaseException().Message);
        }
    }
    public static YanjiuRaphaelResult Decode(uint requested,Tuple<uint,string,string,List<uint>,int,int>? value){
        if(value==null||value.Item1!=requested)return Failed(requested,"Raphael 返回的计算请求编号不匹配");
        if(!Enum.TryParse<RaphaelCalculationStatus>(value.Item2,out var status)||!Enum.IsDefined(status))return Failed(requested,"Raphael 返回未知计算状态");
        if(status==RaphaelCalculationStatus.Success&&(value.Item4==null||value.Item4.Count>4096||value.Item5<0||value.Item6<0))
            return Failed(requested,"Raphael 返回无效的动作或进度品质数据");
        return new(requested,status,value.Item3??"",value.Item4?.ToList()??[],value.Item5,value.Item6);
    }
    private static YanjiuRaphaelResult Failed(uint id,string message)=>new(id,RaphaelCalculationStatus.Failed,message,[],0,0);
}