// SPDX-License-Identifier: GPL-3.0-or-later
using Dalamud.Bindings.ImGui;
namespace DailyRoutines.Extensions
{
    public static class ClipboardUIExtensions
    {
        extension(global::OmenTools.ImGuiOm.ImGuiOm)
        {
            public static void ClickToCopyAndNotify(string text)
            {
                if(!ImGui.IsItemHovered())return;
                ImGui.SetTooltip("点击复制："+text);
                if(ImGui.IsItemClicked())ImGui.SetClipboardText(text);
            }
        }
    }
}
namespace TimeAgo
{
    public static class DateTimeExtensions
    {
        public static string TimeAgo(this DateTime value)
        {
            var delta=DateTime.UtcNow-value.ToUniversalTime();bool future=delta.Ticks<0;double seconds=Math.Abs(delta.TotalSeconds);
            var (amount,unit)=seconds<60?(Math.Max(1,(int)seconds),"秒"):seconds<3600?((int)(seconds/60),"分钟"):seconds<86400?((int)(seconds/3600),"小时"):((int)(seconds/86400),"天");
            return amount+unit+(future?"后":"前");
        }
    }
}
