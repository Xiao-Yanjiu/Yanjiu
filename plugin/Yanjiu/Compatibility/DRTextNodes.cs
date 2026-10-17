// SPDX-License-Identifier: GPL-3.0-or-later
using KamiToolKit.Nodes;
namespace DailyRoutines.Common.Extensions;
public static class YanjiuTextNodeExtensions
{
    // Shrink against the actual rendered text width; keep a readable nonzero size.
    public static void AutoAdjustTextSize(this TextNode node)
    {
        while(node.FontSize>1&&node.GetTextDrawSize(node.String,false).X>node.Size.X)node.FontSize--;
    }
}
