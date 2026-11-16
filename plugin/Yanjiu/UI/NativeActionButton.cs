// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using KamiToolKit.Nodes;
namespace Yanjiu.UI;
// Same game atlas and regions used by OmenTools.KamiToolKit CircleButtonNode.
internal sealed class NativeActionButton : TextureButtonNode
{
    private string current="";
    internal NativeActionButton()
    {
        TexturePath="ui/uld/CircleButtons.tex";
        TextureSize=new(28,28);
    }
    public string String
    {
        get=>current;
        set{
            // Apply on every binding: native hover timelines may reset image tint.
            MultiplyColor=value=="☆"?new(.5f,.5f,.5f):Vector3.One;
            ImageNode.MultiplyColor=Vector3.One;
            if(current==value)return;
            current=value;
            TexturePath="ui/uld/CircleButtons.tex";
            TextureSize=value is "★" or "☆"?new(24,24):new(28,28);

            TextureCoordinates=value switch{
                "⚙" or "设置"=>Vector2.Zero,
                "↻" or "刷新"=>new(112,0),
                "↓"=>new(196,28),
                "×"=>new(168,56),
                "★"=>new(72,136),
                "☆"=>new(72,136),
                _=>new(112,0)
            };
        }
    }
}