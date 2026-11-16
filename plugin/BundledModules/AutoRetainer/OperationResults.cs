// SPDX-License-Identifier: GPL-3.0-or-later
namespace Yanjiu.Modules;
internal static class OperationResults
{
 internal static bool Withdraw(uint beforePlayer,uint beforeRetainer,uint amount,uint player,uint retainer)
  => amount>0&&amount<=beforeRetainer&&(ulong)beforePlayer+amount<=999999999
   &&player==(ulong)beforePlayer+amount&&retainer==beforeRetainer-amount;
 internal static bool Venture(uint expectedTask,uint actualTask,uint previousEnd,uint actualEnd,long serverTime,int beforeTokens,int afterTokens,int cost,bool dialogOpen)
  =>cost>0&&beforeTokens>=cost&&expectedTask==actualTask&&actualEnd>previousEnd&&actualEnd>serverTime
   &&afterTokens==beforeTokens-cost&&!dialogOpen;
}
