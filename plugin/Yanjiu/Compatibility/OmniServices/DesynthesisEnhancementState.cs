// SPDX-License-Identifier: GPL-3.0-or-later
namespace OmniToolbox.Tooltips;
public enum DesynthesisStatus { BelowRecommended, MeetsRecommended }
public readonly record struct DesynthesisLevel(float Current,uint Recommended,DesynthesisStatus Status);
public static class DesynthesisEnhancementState
{
    // Compare actual player skill with the item's level. Do not invent a drop-rate forecast.
    public static DesynthesisLevel Calculate(float current,uint recommended){
        if(!float.IsFinite(current)||current<0)throw new ArgumentOutOfRangeException(nameof(current));
        return new(current,recommended,current<recommended?DesynthesisStatus.BelowRecommended:DesynthesisStatus.MeetsRecommended);
    }
    public static ushort GetColorKey(DesynthesisStatus status)=>status==DesynthesisStatus.BelowRecommended?(ushort)17:(ushort)43;
}