// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Collections;
using OmniToolbox.Config;
using OmniToolbox.Host;
using OmniToolbox.Items;
namespace OmniToolbox.Tooltips;
public sealed class ItemPreviewSupplement(ItemConfig config,CollectionSourceService sources)
{
    private Dictionary<uint,uint[]>? orchestrionRecipes;
    public static string FormatParameter(string name,bool relative,int value,int max)=>
        name+" "+value.ToString("+0;-0;0",CultureInfo.InvariantCulture)+(relative?"%":"")+(relative&&max!=0?"（上限 "+max.ToString(CultureInfo.InvariantCulture)+"）":"");
    public string Describe(Item item){
        var sections=new List<string>();
        if(config.ShowShopSourceInTooltip||config.ShowDutySourceInTooltip){
            var selected=sources.GetItemSources(item.RowId)
                .Where(s=>(config.ShowDutySourceInTooltip&&s.Category==CollectionSourceCategory.Duty)||
                    (config.ShowShopSourceInTooltip&&s.Category is CollectionSourceCategory.Shop or CollectionSourceCategory.Gil or CollectionSourceCategory.Scrips or CollectionSourceCategory.Tomestones or CollectionSourceCategory.CompanySeals or CollectionSourceCategory.MGP or CollectionSourceCategory.TheHunt or CollectionSourceCategory.BeastTribes))
                .Select(s=>s.Description+(string.IsNullOrWhiteSpace(s.Detail)?"":" · "+s.Detail)).Distinct().ToArray();
            if(selected.Length>0)sections.Add("获取来源：\n"+string.Join("\n",selected.Take(12))+(selected.Length>12?$"\n另有 {selected.Length-12} 个来源":""));
        }
        if(config.ShowConsumableEffectsInTooltip&&item.ItemAction.IsValid){
            var action=item.ItemAction.Value;
            if(action.Action.RowId is 844 or 845&&LuminaGetter.TryGetRow<ItemFood>(action.Data[1],out var food)){
                var nq=new List<string>();var hq=new List<string>();
                foreach(var parameter in food.Params){
                    if(!parameter.BaseParam.IsValid||parameter.BaseParam.RowId==0)continue;
                    string name=parameter.BaseParam.Value.Name.ToString();
                    nq.Add(FormatParameter(name,parameter.IsRelative,parameter.Value,parameter.Max));
                    if(item.CanBeHq)hq.Add(FormatParameter(name,parameter.IsRelative,parameter.ValueHQ,parameter.MaxHQ));
                }
                if(food.EXPBonusPercent!=0)nq.Add("经验值 +"+food.EXPBonusPercent+"%");
                if(nq.Count>0)sections.Add("普通品质效果："+string.Join("；",nq));
                if(hq.Count>0)sections.Add("高品质效果："+string.Join("；",hq));
            }
            else if(action.Action.RowId is 847 or 848 or 849){
                string recovery=action.Action.RowId==848?"魔力":"体力";
                if(action.Data[0]!=0||action.Data[1]!=0)sections.Add($"{recovery}恢复：{action.Data[0]}%（上限 {action.Data[1]}）");
                if(action.Action.RowId==849&&(action.Data[3]!=0||action.Data[4]!=0))sections.Add($"魔力恢复：{action.Data[3]}%（上限 {action.Data[4]}）");
                if(item.CanBeHq&&(action.DataHQ[0]!=0||action.DataHQ[1]!=0))sections.Add($"高品质{recovery}恢复：{action.DataHQ[0]}%（上限 {action.DataHQ[1]}）");
                if(item.CanBeHq&&action.Action.RowId==849&&(action.DataHQ[3]!=0||action.DataHQ[4]!=0))sections.Add($"高品质魔力恢复：{action.DataHQ[3]}%（上限 {action.DataHQ[4]}）");
            }
        }
        if(config.ShowItemSetContentsInTooltip){
            var contents=ItemSetCatalog.GetItemSlots(item.RowId).Where(id=>id!=0).Distinct().Select(Name).ToArray();
            if(contents.Length>0)sections.Add("套装内容："+string.Join("、",contents));
        }
        if(config.ShowBlindBoxProgressInTooltip){
            var pool=sources.GetBlindBoxDropPool(item.RowId);
            if(pool.Count>0)sections.Add(DescribeProgress("盲盒收藏进度",pool));
        }
        if(config.ShowFadedOrchestrionProgressInTooltip){
            var results=(orchestrionRecipes??=BuildOrchestrionRecipes()).GetValueOrDefault(item.RowId);
            if(results is {Length:>0}){
                sections.Add("可制作乐谱："+string.Join("、",results.Take(16).Select(Name))+(results.Length>16?$" 等{results.Length}种":""));
                sections.Add(DescribeProgress("乐谱收藏进度",results));
            }
        }
        return string.Join("\n\n",sections);
    }
    private static string Name(uint itemID)=>LuminaGetter.TryGetRow<Item>(itemID,out var row)?row.Name.ToString():$"物品 {itemID}";
    private static string DescribeProgress(string title,IEnumerable<uint> itemIDs){
        if(!DalamudServices.PlayerState.IsLoaded)return title+"：角色解锁状态暂不可用";
        int known=0,unlocked=0,unknown=0;
        foreach(uint id in itemIDs.Distinct()){
            if(!LuminaGetter.TryGetRow<Item>(id,out var item)||!DalamudServices.UnlockState.IsItemUnlockable(item)){unknown++;continue;}
            known++;if(DalamudServices.UnlockState.IsItemUnlocked(item))unlocked++;
        }
        return $"{title}：{unlocked}/{known}"+(unknown>0?$"（另有{unknown}项无法判定收藏状态）":"");
    }
    private static Dictionary<uint,uint[]> BuildOrchestrionRecipes(){
        var map=new Dictionary<uint,HashSet<uint>>();
        foreach(var recipe in LuminaGetter.Get<Recipe>()){
            if(!recipe.ItemResult.IsValid)continue;
            var result=recipe.ItemResult.Value;
            if(!result.ItemAction.IsValid||result.ItemAction.Value.Action.RowId!=25183)continue;
            for(int index=0;index<recipe.Ingredient.Count;index++){
                var ingredient=recipe.Ingredient[index];
                if(!ingredient.IsValid||ingredient.RowId==0||recipe.AmountIngredient[index]==0||ItemCategoryMap.IsCurrency(ingredient.Value))continue;
                if(!map.TryGetValue(ingredient.RowId,out var outputs))map[ingredient.RowId]=outputs=[];
                outputs.Add(result.RowId);
            }
        }
        return map.ToDictionary(p=>p.Key,p=>p.Value.Order().ToArray());
    }
}