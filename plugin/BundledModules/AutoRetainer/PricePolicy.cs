namespace Yanjiu.Modules;
internal static class PricePolicy
{
 internal static uint? Calculate(uint market,uint original,int undercut,int floor,int maxDrop)
 {
  if(market==0||market==uint.MaxValue)return null;
  var price=(uint)Math.Clamp(Math.Max(Math.Clamp(floor,1,999999999),(long)market-Math.Max(0,undercut)),1,999999999);
  return original>0&&(long)price*100<(long)original*(100-Math.Clamp(maxDrop,0,100))?null:price;
 }
}
