// SPDX-License-Identifier: GPL-3.0-or-later
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace OmniToolbox.UI;
public static class CooldownFontNames
{
    public static readonly FontType[] SupportedFonts=[FontType.Axis,FontType.MiedingerMed,FontType.Miedinger,FontType.TrumpGothic];
    public static string GetFontName(FontType font)=>font switch{FontType.Axis=>"Axis",FontType.MiedingerMed=>"Miedinger Medium",FontType.Miedinger=>"Miedinger",_=>"Trump Gothic"};
}
