namespace OmniToolbox.Config;

public enum ItemImagePreviewPosition { Right,Left }

[Serializable]
public sealed class ItemImagePreviewConfig
{
    public bool ShowMounts { get; set; } = true;

    public bool ShowMinions { get; set; } = true;

    public bool ShowHairstyles { get; set; } = true;

    public bool ShowPaintings { get; set; } = true;

    public bool ShowFashionAccessories { get; set; } = true;

    public bool ShowExchangeRewards { get; set; } = true;

    public ItemImagePreviewPosition Position {get;set;}=ItemImagePreviewPosition.Right;

    public float Scale { get; set; } = 1f;
}
