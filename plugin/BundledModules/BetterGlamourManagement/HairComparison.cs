using FFXIVClientStructs.FFXIV.Client.Game.Character;
namespace OmniToolbox.TreePublic;
internal static class HairComparison { public static bool HairAppearanceMatches(CustomizeData actual, CustomizeData expected) =>
        actual.Hairstyle == expected.Hairstyle && actual.HairColor == expected.HairColor &&
        actual.HighlightsColor == expected.HighlightsColor && actual.Highlights == expected.Highlights;
}