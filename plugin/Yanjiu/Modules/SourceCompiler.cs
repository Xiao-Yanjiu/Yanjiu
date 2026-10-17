// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Yanjiu.Modules;

public sealed class CompiledModule : IDisposable
{
    public required Assembly Assembly { get; init; }
    public required Type ModuleType { get; init; }
    public AssemblyLoadContext? Context { get; init; }
    public required string Hash { get; init; }
    public ModuleBase Create()=>OmniModuleFactory.Create(ModuleType);
    public void Dispose()=>Context?.Unload();
}
public sealed class ModuleCompilationException(string message):Exception(message);
public sealed class SourceCompiler
{
    private readonly MetadataReference[] references;
    private static string PluginDirectory=>PluginFiles.DirectoryPath;
    public SourceCompiler(IEnumerable<string>? paths=null)
    {
        var files=paths?.ToArray()??ReferencePaths();var unique=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var path in files){try{var name=AssemblyName.GetAssemblyName(path).Name!;if(name.StartsWith("OmniToolbox")||name.StartsWith("DailyRoutines"))continue;unique[name]=path;}catch{}}
        foreach(var path in Directory.EnumerateFiles(PluginDirectory,"*.dll")){try{unique[AssemblyName.GetAssemblyName(path).Name!]=path;}catch{}}
        unique[typeof(ModuleBase).Assembly.GetName().Name!]=Path.Combine(PluginDirectory,"Yanjiu.dll");
        references=unique.Values.Select(path=>MetadataReference.CreateFromFile(path)).ToArray();
    }
    public static string[] ReferencePaths()
    {
        var roots=new[]{Path.GetDirectoryName(typeof(object).Assembly.Location),Path.GetDirectoryName(typeof(System.Security.Cryptography.ProtectedData).Assembly.Location),Path.GetDirectoryName(typeof(System.Windows.Forms.Form).Assembly.Location),Path.GetDirectoryName(typeof(Dalamud.Plugin.IDalamudPlugin).Assembly.Location),PluginDirectory,AppContext.BaseDirectory};
        var loaded=AppDomain.CurrentDomain.GetAssemblies().Where(a=>!a.IsDynamic).SelectMany(a=>{try{return new[]{a.Location};}catch(NotSupportedException){return Array.Empty<string>();}});
        var platform=(AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string??"").Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries);
        return ReferenceFiles(roots).Concat(loaded).Concat(platform).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static string[] ReferenceFiles(IEnumerable<string?> roots)=>roots.Where(root=>!string.IsNullOrWhiteSpace(root)&&Directory.Exists(root)).Distinct(StringComparer.OrdinalIgnoreCase).SelectMany(root=>Directory.EnumerateFiles(root!,"*.dll")).ToArray();
    public CompiledModule Compile(byte[] data,string name)
    {
        if(data.Length>4*1024*1024)throw new ModuleCompilationException("模块不能超过 4 MiB");
        var hash=Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();var context=new ModuleContext();
        try{
            Assembly assembly;
            if(Path.GetExtension(name).Equals(".dll",StringComparison.OrdinalIgnoreCase)){using var stream=new MemoryStream(data);assembly=context.LoadFromStream(stream);}
            else if(Path.GetExtension(name).Equals(".cs",StringComparison.OrdinalIgnoreCase)){
                var source=new System.Text.UTF8Encoding(false,true).GetString(data).TrimStart('\uFEFF');
                var parseOptions=new CSharpParseOptions(LanguageVersion.Preview);
                var globals="global using System;global using System.IO;global using System.Linq;global using System.Collections.Generic;global using System.Threading;global using System.Threading.Tasks;global using System.Numerics;";
                if(source.Contains("OmniToolbox.Common.Module.Abstractions",StringComparison.Ordinal)&&!source.Contains("DailyRoutines.Common.Module",StringComparison.Ordinal))globals+="global using OmniToolbox.Common.Module.Models;";
                var trees=new[]{CSharpSyntaxTree.ParseText(source,parseOptions),CSharpSyntaxTree.ParseText(globals,parseOptions)};
                var compile=CSharpCompilation.Create("YanjiuModule_"+hash[..12]+"_"+Guid.NewGuid().ToString("N"),trees,references,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,optimizationLevel:OptimizationLevel.Release,allowUnsafe:true));
                using var output=new MemoryStream();var result=compile.Emit(output);if(!result.Success)throw new ModuleCompilationException(string.Join("\n",result.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error).Take(40).Select(d=>d.ToString())));
                output.Position=0;assembly=context.LoadFromStream(output);
            }else throw new ModuleCompilationException("只支持 .cs / .dll 模块");
            var types=assembly.GetTypes().Where(t=>!t.IsAbstract&&typeof(ModuleBase).IsAssignableFrom(t)).ToArray();
            if(types.Length!=1)throw new ModuleCompilationException("需要一个可实例化的 Yanjiu/DR/Omni 兼容模块。原宿主 DLL 不能直接复用，请导入对应源码。");
            var unsupported=OmniModuleFactory.UnsupportedReason(types[0]);if(unsupported!=null)throw new ModuleCompilationException(unsupported);
            return new CompiledModule{Assembly=assembly,ModuleType=types[0],Context=context,Hash=hash};
        }catch(ReflectionTypeLoadException e){context.Unload();var missing=string.Join("；",e.LoaderExceptions.Where(x=>x!=null).Select(x=>x!.Message).Distinct().Take(4));throw new ModuleCompilationException("模块依赖不兼容，请使用针对 Yanjiu 编译的 DLL 或导入 UTF-8 源码。"+missing);}catch(System.Text.DecoderFallbackException){context.Unload();throw new ModuleCompilationException("模块源码不是有效的 UTF-8 编码，请转换为 UTF-8 后导入。");}catch{context.Unload();throw;}
    }
    private sealed class ModuleContext():AssemblyLoadContext("YanjiuModule-"+Guid.NewGuid().ToString("N"),true)
    {
        protected override Assembly? Load(AssemblyName name)=>AssemblyLoadContext.GetLoadContext(typeof(ModuleBase).Assembly)!.LoadFromAssemblyName(name);
    }
}


