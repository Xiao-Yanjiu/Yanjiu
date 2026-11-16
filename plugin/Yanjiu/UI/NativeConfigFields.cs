// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;
namespace Yanjiu.UI;
internal sealed record NativeConfigField(string Path,Func<JsonNode?> Read,Action<JsonNode?> Write);
internal static class NativeConfigFields
{
    internal static NativeConfigField[] Flatten(JsonObject root)
    {
        var result=new List<NativeConfigField>();
        void Visit(JsonNode? value,string path,Func<JsonNode?> read,Action<JsonNode?> write,int depth)
        {
            if(depth>32||result.Count>=2000)throw new InvalidDataException("配置过深或字段过多，无法打开原生编辑器。");
            if(value is JsonObject obj&&obj.Count>0){
                foreach(var pair in obj){var key=pair.Key;Visit(pair.Value,path.Length==0?key:path+" / "+key,()=>obj[key],node=>obj[key]=node,depth+1);}
            }else if(value is JsonArray array&&array.Count>0){
                for(int i=0;i<array.Count;i++){int index=i;Visit(array[i],path+" ["+(i+1)+"]",()=>array[index],node=>array[index]=node,depth+1);}
            }else result.Add(new(path,read,write));
        }
        foreach(var pair in root){var key=pair.Key;Visit(pair.Value,key,()=>root[key],node=>root[key]=node,0);}
        return result.ToArray();
    }
}
