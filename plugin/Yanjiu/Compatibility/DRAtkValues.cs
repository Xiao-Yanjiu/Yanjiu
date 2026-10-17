// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using OmenTools.Interop.Game.Models;
namespace DailyRoutines.Common.Extensions;
public static class YanjiuAtkValueParsing
{
    extension(AtkValueArray)
    {
        public static AtkValueArray FromString(string[] arguments)
        {
            if(arguments.Length>128||arguments.Any(s=>s.Length>4096))throw new ArgumentException("回调参数过多或过长");
            return new AtkValueArray(arguments.Select(Parse).ToArray());
        }
    }
    public static object Parse(string value){
        if(bool.TryParse(value,out var boolean))return boolean;
        if(int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var number))return number;
        if(uint.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var unsigned))return unsigned;
        if(float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var fraction)&&float.IsFinite(fraction))return fraction;
        return value;
    }
}
