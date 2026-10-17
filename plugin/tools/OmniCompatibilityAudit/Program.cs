using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if(args.Length!=4)throw new ArgumentException("Usage: upstream-source sdk-directory yanjiu-artifacts output-directory");
var sourceRoot=Path.GetFullPath(args[0]);var sdk=Path.GetFullPath(args[1]);var plugin=Path.GetFullPath(args[2]);var output=Path.GetFullPath(args[3]);Directory.CreateDirectory(output);
var references=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
foreach(var root in new[]{Path.GetDirectoryName(typeof(object).Assembly.Location)!,Path.GetDirectoryName(typeof(System.Windows.Forms.Form).Assembly.Location)!,sdk,plugin})
foreach(var file in Directory.EnumerateFiles(root,"*.dll")){
 try{var name=AssemblyName.GetAssemblyName(file).Name!;if(name.StartsWith("DailyRoutines")||name.StartsWith("OmniToolbox"))continue;references[name]=file;}catch{}
}
var metadata=references.Values.Select(path=>MetadataReference.CreateFromFile(path)).ToArray();
var options=new CSharpParseOptions(LanguageVersion.Preview);
var globalFiles=Directory.GetFiles(Path.Combine(sourceRoot,"GlobalUsings"),"*.cs");
var globals=globalFiles.Select(path=>CSharpSyntaxTree.ParseText(File.ReadAllText(path),options,path)).Append(CSharpSyntaxTree.ParseText("global using System;global using System.IO;global using System.Linq;global using System.Collections.Generic;global using System.Threading;global using System.Threading.Tasks;global using System.Numerics;global using System.Net.Http;global using KamiToolKit.BaseTypes;global using KamiToolKit.Interfaces;global using OmniToolbox.UI;global using OmenTools.KamiToolKit.Nodes.CircleButton;global using KamiToolKit.Nodes.Simplified;",options,"Yanjiu.ImplicitUsings.g.cs")).ToArray();
var generatorPath=Environment.GetEnvironmentVariable("YANJIU_REGEX_GENERATOR");
IIncrementalGenerator? regexGenerator=null;
if(!string.IsNullOrEmpty(generatorPath)){
 var assembly=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(generatorPath));
 Type[] generatorTypes; try { generatorTypes=assembly.GetTypes(); } catch (ReflectionTypeLoadException ex) { generatorTypes=ex.Types.OfType<Type>().ToArray(); }
 regexGenerator=generatorTypes.Where(t=>!t.IsAbstract&&typeof(IIncrementalGenerator).IsAssignableFrom(t)).Select(t=>(IIncrementalGenerator)Activator.CreateInstance(t)!).First();
}
Directory.CreateDirectory(Path.Combine(output,"compiled"));
var results=new List<AuditRow>();
var moduleFiles=Directory.EnumerateFiles(sourceRoot,"*.cs",SearchOption.AllDirectories).Where(path=>Regex.IsMatch(File.ReadAllText(path),@"\bclass\s+\w+\s*(?:\([^{}]*?\))?\s*:\s*ModuleBase\b")).Order().ToArray();
foreach(var file in moduleFiles){
 var source=File.ReadAllText(file);var name=Regex.Match(source,@"\bclass\s+(\w+)\s*(?:\([^{}]*?\))?\s*:\s*ModuleBase\b").Groups[1].Value;
 // Include helper/partial files belonging to this module, never unrelated modules.
 var parent=Path.GetDirectoryName(file)!;var grouped=Directory.GetFiles(parent,"*.cs").Count(p=>Regex.IsMatch(File.ReadAllText(p),@"\bclass\s+\w+\s*(?:\([^{}]*?\))?\s*:\s*ModuleBase\b"))==1?Directory.GetFiles(parent,"*.cs",SearchOption.AllDirectories):new[]{file};
 grouped=grouped.Concat(Directory.GetFiles(Path.Combine(sourceRoot,"Shared"),"*.cs")).Distinct().ToArray();
 var trees=grouped.Select(path=>CSharpSyntaxTree.ParseText(File.ReadAllText(path),options,Path.GetRelativePath(sourceRoot,path))).Concat(globals);
 var compilation=CSharpCompilation.Create("YanjiuOmni_"+name,trees,metadata,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true,optimizationLevel:OptimizationLevel.Release));
 if(regexGenerator!=null){GeneratorDriver driver=CSharpGeneratorDriver.Create([regexGenerator.AsSourceGenerator()],parseOptions:options);driver.RunGeneratorsAndUpdateCompilation(compilation,out var updated,out var generatorErrors);compilation=(CSharpCompilation)updated;}
 var moduleExists=compilation.GetSymbolsWithName(name,SymbolFilter.Type).OfType<INamedTypeSymbol>().Any(t=>!t.IsAbstract&&t.Locations.Any(l=>l.IsInSource)&&IsYanjiuModule(t));
 using var binary=new MemoryStream();var emitted=compilation.Emit(binary);
 if(emitted.Success&&moduleExists)File.WriteAllBytes(Path.Combine(output,"compiled",name+".dll"),binary.ToArray());
 var errors=emitted.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error).ToList();if(!moduleExists)errors.Add(Diagnostic.Create(new DiagnosticDescriptor("YDR001","Missing module","Source does not contain a concrete Yanjiu-compatible module after preprocessing","Compatibility",DiagnosticSeverity.Error,true),Location.None));
 results.Add(new(name,Path.GetRelativePath(sourceRoot,file).Replace('\\','/'),grouped.Length,emitted.Success&&moduleExists,errors.Count,errors.Take(100).Select(d=>new AuditError(d.Id,d.GetMessage(),d.Location.GetLineSpan().Path,d.Location.GetLineSpan().StartLinePosition.Line+1)).ToArray()));
 if(results.Count%25==0)Console.WriteLine($"AUDIT {results.Count}/{moduleFiles.Length}: {results.Count(r=>r.Compiles)} compiled");
}
var report=new{generatedAtUtc=DateTimeOffset.UtcNow,sourceRoot,sourceFiles=Directory.GetFiles(sourceRoot,"*.cs",SearchOption.AllDirectories).Length,moduleCount=results.Count,compilePass=results.Count(r=>r.Compiles),compileFail=results.Count(r=>!r.Compiles),runtimeValidated=0,notes="Compile-only audit against Yanjiu. No upstream module was loaded, instantiated or enabled. A compile pass is not runtime compatibility.",modules=results};
var json=new JsonSerializerOptions{WriteIndented=true};
File.WriteAllText(Path.Combine(output,"compatibility.json"),JsonSerializer.Serialize(report,json));
var counts=results.SelectMany(r=>r.Errors).GroupBy(e=>e.Message).OrderByDescending(g=>g.Count()).Take(80).Select(g=>new{message=g.Key,occurrences=g.Count()});
File.WriteAllText(Path.Combine(output,"missing-apis.json"),JsonSerializer.Serialize(counts,json));
Console.WriteLine($"DONE: {results.Count} modules, {results.Count(r=>r.Compiles)} compile passes; runtime validated: 0. Reports: {output}");
static bool IsYanjiuModule(INamedTypeSymbol type){for(var parent=type.BaseType;parent!=null;parent=parent.BaseType)if(parent.ToDisplayString()=="Yanjiu.Modules.ModuleBase")return true;return false;}
record AuditError(string Code,string Message,string File,int Line);
record AuditRow(string Name,string Source,int SourceFileCount,bool Compiles,int ErrorCount,AuditError[] Errors);






