using System.Runtime.Loader;
using System.Text.Json;
if(args.Length!=3)throw new ArgumentException("plugin-directory sdk-directory audit-directory");
AssemblyLoadContext.Default.Resolving+=(context,name)=>{foreach(var root in args.Take(2)){var file=Path.Combine(root,name.Name+".dll");if(File.Exists(file))return context.LoadFromAssemblyPath(file);}return null;};
PackageCheck.Run(args);
static class PackageCheck
{
 public static void Run(string[] args){
  using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(args[2],"compatibility.json")));
  var compiler=new Yanjiu.Modules.SourceCompiler(Directory.GetFiles(args[0],"*.dll").Concat(Directory.GetFiles(args[1],"*.dll")).Concat(Yanjiu.Modules.SourceCompiler.ReferencePaths()));
  var rows=new List<object>();int passed=0;
  foreach(var module in document.RootElement.GetProperty("modules").EnumerateArray()){
   string id=module.GetProperty("Name").GetString()!;bool compiles=module.GetProperty("Compiles").GetBoolean();string error="";bool importable=false;
   if(compiles)try{var file=Path.Combine(args[2],"compiled",id+".dll");using var compiled=compiler.Compile(File.ReadAllBytes(file),file);importable=true;passed++;}catch(Exception e){error=e.GetBaseException().Message;}
   rows.Add(new{id,compiles,importable,error});
  }
  File.WriteAllText(Path.Combine(args[2],"binding.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
  Console.WriteLine($"PACKAGE: {passed}/{rows.Count} module types and constructor bindings validated; no constructors/Init executed.");
 }
}
