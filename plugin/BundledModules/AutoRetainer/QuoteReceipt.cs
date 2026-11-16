// SPDX-License-Identifier: GPL-3.0-or-later
namespace Yanjiu.Modules;
internal sealed class QuoteReceipt
{
 internal readonly record struct Quote(ulong Id,uint Item,bool Hq,bool Mannequin,ulong Owner,uint Price,uint Quantity);
 private readonly Dictionary<ulong,Quote> rows=new();
 private readonly HashSet<int> usedIds=new();
 private int request; private uint item; private bool active,invalid;
 internal bool Begin(int id,uint target){Cancel();if(!usedIds.Add(id))return false;request=id;item=target;active=true;return true;}
 internal bool Invalid=>invalid;
 internal void Cancel(){active=false;rows.Clear();invalid=false;}
 internal void Receive(int id,Quote[] quotes){
  if(!active)return;
  if(id!=request){invalid=true;return;}
  foreach(var q in quotes){
   if(q.Item!=item||q.Id==0||q.Price==0||q.Quantity==0||q.Owner==0){invalid=true;return;}
   if(rows.TryGetValue(q.Id,out var old)&&old!=q){invalid=true;return;}
   if(!rows.ContainsKey(q.Id)&&rows.Count>=100){invalid=true;return;}
   rows[q.Id]=q;
  }
 }
 internal Quote[]? Read(int current,uint target,int count){
  if(!active||invalid||current!=request||target!=item||count<=0||count>100||rows.Count!=count)return null;
  return rows.Values.ToArray();
 }
}
