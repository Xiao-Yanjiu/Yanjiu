// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using KamiToolKit.Nodes;
namespace Yanjiu.UI;
internal sealed unsafe partial class NativeBuiltInSettings
{
    private static readonly HashSet<string> simpleModules=["AutoRequestItemSubmit","AutoJumboCactpot","AutoSubmarineCollect","AutoCountPlayers","AutoMoveGearsNotInSet","InstantLogout","AutoUseItemStacks","AutoDisplayPlayerHitbox","CustomizeSightDistance"];
    private object ReadModel()
    {
        var field=owner.GetType().GetField("config",BindingFlags.Instance|BindingFlags.NonPublic)!;
        if(owner.IsEnabled&&field.GetValue(owner) is {} live)return live;
        var draft=new Yanjiu.Modules.DisabledModuleSettings((DailyRoutines.Common.Module.Abstractions.ModuleBase)owner);
        return JsonSerializer.Deserialize(draft.Values.ToJsonString(),draft.ModelType,new JsonSerializerOptions{IncludeFields=true})!;
    }
    private object ReadField(string name)=>ReadModel().GetType().GetField(name)!.GetValue(ReadModel())!;
    private void WriteField(string name,object value)
    {
        if(!Available())return;
        var model=ReadModel();var field=model.GetType().GetField(name)!;var previous=field.GetValue(model);
        if(owner.ModuleName=="CustomizeSightDistance"&&value is IConvertible){
            float n=Convert.ToSingle(value,CultureInfo.InvariantCulture);float F(string key)=>Convert.ToSingle(model.GetType().GetField(key)!.GetValue(model));
            if(name=="MaxDistance"&&n<F("MinDistance")||name=="MinDistance"&&n>F("MaxDistance")||name=="MaxRotation"&&n<F("MinRotation")||name=="MinRotation"&&n>F("MaxRotation")||name=="MaxFoV"&&n<F("MinFoV")||name=="MinFoV"&&n>F("MaxFoV")||name=="FoV"&&(n<F("MinFoV")||n>F("MaxFoV")))throw new ArgumentException("数值超出了当前最小值／最大值范围。");
        }
        field.SetValue(model,field.FieldType.IsInstanceOfType(value)?value:field.FieldType.IsEnum?Enum.ToObject(field.FieldType,value):Convert.ChangeType(value,field.FieldType,CultureInfo.InvariantCulture));
        try{typeof(DailyRoutines.Common.Module.Abstractions.ModuleBase).GetMethod("SaveConfig")!.MakeGenericMethod(model.GetType()).Invoke(owner,[model]);if(owner.IsEnabled)owner.GetType().GetMethod("ApplyNativeConfiguration",BindingFlags.Instance|BindingFlags.Public)?.Invoke(owner,null);}
        catch{field.SetValue(model,previous);throw;}
    }
    private void Change(string field,object value){try{WriteField(field,value);}catch(Exception e){if(status!=null)status.String="保存失败："+e.GetBaseException().Message;}}
    private void SetupSimple(KamiToolKit.Nodes.ResNode root)
    {
        float y=0;
        void Toggle(string field,string label){
            new CheckboxNode{Position=ContentStartPosition+new System.Numerics.Vector2(0,y),Size=new(650,28),String=label,IsChecked=(bool)ReadField(field),OnClick=value=>Change(field,value)}.AttachNode(root);y+=42;
        }
        void Number(string field,string label,double min,double max){
            new TextNode{Position=ContentStartPosition+new System.Numerics.Vector2(0,y),Size=new(300,28),String=label}.AttachNode(root);
            var input=new TextInputNode{Position=ContentStartPosition+new System.Numerics.Vector2(310,y),Size=new(300,28),String=Convert.ToString(ReadField(field),CultureInfo.InvariantCulture)!,OnInputComplete=text=>{
                if(double.TryParse(text.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var value)&&double.IsFinite(value)&&value>=min&&value<=max&&(ReadField(field) is float or double||value==Math.Truncate(value)))Change(field,value);
                else if(status!=null)status.String=$"请输入 {min}～{max} 范围内的数值。";
            }};input.AttachNode(root);
            if(owner.ModuleName=="CustomizeSightDistance"){
                var reset=field switch{"MaxDistance"=>20f,"MinDistance"=>1.5f,"MaxRotation"=>.785398f,"MinRotation"=>-1.483530f,"MinFoV"=>.69f,_=>.78f};
                new TextButtonNode{Position=ContentStartPosition+new System.Numerics.Vector2(620,y),Size=new(75,28),String="重置",OnClick=()=>{try{WriteField(field,reset);input.String=Convert.ToString(ReadField(field),CultureInfo.InvariantCulture)!;}catch(Exception e){if(status!=null)status.String=e.GetBaseException().Message;}}}.AttachNode(root);
            }
            y+=42;
        }
        void ActionButton(string title,string method,bool takesTask=false){
            new TextButtonNode{Position=ContentStartPosition+new System.Numerics.Vector2(0,y),Size=new(230,28),String=title,OnClick=()=>{
                if(!Available()||!owner.IsEnabled){if(status!=null)status.String="请先启用模块再执行操作。";return;}
                try{
                    object? task=null;
                    if(takesTask){for(var type=owner.GetType();type!=null&&task==null;type=type.BaseType){task=type.GetField("TaskHelper",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)?.GetValue(owner)??type.GetProperty("TaskHelper",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)?.GetValue(owner);}
                        if(task==null)throw new InvalidOperationException("模块任务队列尚未就绪。");}
                    owner.GetType().GetMethod(method,BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,takesTask?[task]:null);
                }catch(Exception e){if(status!=null)status.String=e.GetBaseException().Message;}
            }}.AttachNode(root);y+=42;
        }
        switch(owner.ModuleName){
            case "CustomizeSightDistance":
                Number("MaxDistance","最大视距",1,80);Number("MinDistance","最小视距",0,80);
                Number("MaxRotation","最大俯仰角",-1.569,1.569);Number("MinRotation","最小俯仰角",-1.569,1.569);
                Number("MaxFoV","最大视野角",.01,3);Number("MinFoV","最小视野角",.01,3);Number("FoV","当前视野角",.01,3);
                Toggle("IgnoreCollision","忽略摄像机碰撞");
                break;
            case "AutoDisplayPlayerHitbox":
                Toggle("OnlyInCombat","仅战斗中显示");Toggle("OnlyInDuty","仅副本内显示");Toggle("OnlyUnsheathed","仅拔出武器时显示");
                new ColorEditNode{Position=ContentStartPosition+new System.Numerics.Vector2(0,y),Size=new(450,30),String="判定点颜色",CurrentColor=(System.Numerics.Vector4)ReadField("Color"),DefaultColor=System.Numerics.Vector4.One,OnColorConfirmed=value=>Change("Color",value)}.AttachNode(root);y+=42;
                Number("Size","图标大小",1,4096);Number("IconID","图标编号",0,uint.MaxValue);
                new TextButtonNode{Position=ContentStartPosition+new System.Numerics.Vector2(0,y),Size=new(180,28),String="选择图标",OnClick=()=>Services.IconPicker?.Invoke(id=>{if(Available())Change("IconID",id);})}.AttachNode(root);y+=42;
                for(int axis=0;axis<3;axis++){
                    int component=axis;var offset=(System.Numerics.Vector3)ReadField("Offset");var initial=axis==0?offset.X:axis==1?offset.Y:offset.Z;
                    new TextNode{Position=ContentStartPosition+new System.Numerics.Vector2(0,y),Size=new(280,28),String="偏移 "+"XYZ"[axis]}.AttachNode(root);
                    new TextInputNode{Position=ContentStartPosition+new System.Numerics.Vector2(310,y),Size=new(300,28),String=initial.ToString(CultureInfo.InvariantCulture),OnInputComplete=text=>{
                        if(float.TryParse(text.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var value)&&float.IsFinite(value)){
                            var updated=(System.Numerics.Vector3)ReadField("Offset");if(component==0)updated.X=value;else if(component==1)updated.Y=value;else updated.Z=value;Change("Offset",updated);
                        }else if(status!=null)status.String="偏移必须是有效数值。";
                    }}.AttachNode(root);y+=42;
                }
                break;
            case "AutoUseItemStacks":new TextNode{Position=ContentStartPosition+new System.Numerics.Vector2(0,y),Size=new(650,30),String="按住 Shift 可取消当前批量使用任务。"}.AttachNode(root);break;
            case "AutoMoveGearsNotInSet":ActionButton("取出未在套装内的装备","EnqueueRetrieve");break;
            case "InstantLogout":ActionButton("登出角色","Logout",true);ActionButton("退出游戏","Shutdown",true);break;
            case "AutoRequestItemSubmit":Toggle("IsSubmitHQItem","允许递交高品质物品");break;
            case "AutoJumboCactpot":
                new DropDownNode<int>{Position=ContentStartPosition+new System.Numerics.Vector2(0,y),Size=new(280,28),Options=[0,1],GetLabelFunction=value=>value==0?"随机号码":"固定号码",SelectedOption=Convert.ToInt32(ReadField("NumberMode")),OnOptionSelected=value=>Change("NumberMode",value)}.AttachNode(root);y+=42;
                Number("FixedNumber","固定号码（0～9999）",0,9999);break;
            case "AutoSubmarineCollect":Number("NotifyCount","通知所需完成潜水艇数量",0,4);Number("AutoCollectCount","自动收取所需完成数量",0,4);break;
            case "AutoCountPlayers":Number("ScaleFactor","显示缩放",.1,10);Toggle("DisplayLineWhenTargetingMe","显示正在以我为目标的玩家连线");Toggle("FilterFriend","过滤好友");break;
        }
    }
}
