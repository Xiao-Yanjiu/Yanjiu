// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using DailyRoutines.Common.Module.Abstractions;
namespace Yanjiu.Modules;

/// <summary>Edits the persisted configuration without invoking the module's runtime lifecycle.</summary>
public sealed class DisabledModuleSettings
{
    private static readonly JsonSerializerOptions Options=new(){IncludeFields=true,WriteIndented=true};
    private readonly Type modelType;
    private readonly string path;
    public JsonObject Values{get;private set;}
    public bool Dirty{get;set;}
    public Dictionary<uint,string> Inputs{get;}=new();public HashSet<uint> InvalidInputs{get;}=new();
    public string Error{get;private set;}="";
    public Type ModelType=>modelType;
    public DisabledModuleSettings(DailyRoutines.Common.Module.Abstractions.ModuleBase owner)
    {
        modelType=FindConfigType(owner.GetType())??throw new InvalidOperationException("此模块没有独立保存的设置");
        path=Path.Combine(Services.ConfigDirectory,"modules",owner.GetType().FullName!.Replace(':','_')+".json");
        object model;
        if(File.Exists(path)){
            if(new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("模块配置超过4 MiB");
            model=JsonSerializer.Deserialize(File.ReadAllText(path),modelType,Options)??throw new InvalidDataException("模块配置为空");
        }else model=owner.CreateSettingsModel(modelType);
        Values=JsonSerializer.SerializeToNode(model,modelType,Options) as JsonObject??throw new InvalidDataException("模块配置不是对象");
    }
    public static Type? FindConfigType(Type moduleType)
    {
        for(var type=moduleType;type!=null&&type!=typeof(DailyRoutines.Common.Module.Abstractions.ModuleBase);type=type.BaseType){
            var fields=type.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)
                .Where(field=>typeof(ModuleConfig).IsAssignableFrom(field.FieldType)&&!field.FieldType.IsAbstract).Select(field=>field.FieldType).Distinct().ToArray();
            if(fields.Length==1)return fields[0];
            if(fields.Length>1)throw new InvalidOperationException("此模块包含多份独立配置，需要分别适配");
        }
        return null;
    }
    public void Save()
    {
        try{
            if(InvalidInputs.Count>0)throw new InvalidDataException("请先修正无效的数值");
            var json=Values.ToJsonString(Options);
            if(json.Length>4*1024*1024)throw new InvalidDataException("模块配置超过4 MiB");
            // Validate against the exact runtime model, including enums, numeric ranges and collections.
            var model=JsonSerializer.Deserialize(json,modelType,Options)??throw new InvalidDataException("模块配置为空");
            var output=JsonSerializer.Serialize(model,modelType,Options);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path+".tmp",output);File.Move(path+".tmp",path,true);
            Dirty=false;Error="";
        }catch(Exception e){Error=e.GetBaseException().Message;throw;}
    }
}

