// SPDX-License-Identifier: GPL-3.0-or-later
using OmniToolbox.Config;
namespace OmniToolbox.Items;
public static class ImagePreviewPlacement
{
    public static bool ChooseRight(ItemImagePreviewPosition preferred,float width,float right,float left){
        if(!float.IsFinite(width)||width<=0)return preferred!=ItemImagePreviewPosition.Left;
        right=float.IsFinite(right)?Math.Max(0,right):0;
        left=float.IsFinite(left)?Math.Max(0,left):0;
        if(preferred==ItemImagePreviewPosition.Left){if(left>=width)return false;if(right>=width)return true;}
        else{if(right>=width)return true;if(left>=width)return false;}
        return right>=left;
    }
}