namespace Authly.Core.Enums;

/// <summary>Layout of the hosted login/register/MFA pages.</summary>
public enum BrandingLayout
{
    /// <summary>A single centered card on a tinted solid background (the default).</summary>
    CenteredPlain,

    /// <summary>A centered card floating over a full-page background image/gradient.</summary>
    CenteredOverBackground,

    /// <summary>Two columns: the form on the left, a branded panel on the right.</summary>
    FormLeft,

    /// <summary>Two columns: a branded panel on the left, the form on the right.</summary>
    FormRight
}

/// <summary>What fills a branded panel (split layouts) or the page (centered-over-background).</summary>
public enum BrandingBackground
{
    /// <summary>A flat fill of the primary color.</summary>
    Solid,

    /// <summary>A linear gradient between two configurable colors.</summary>
    Gradient,

    /// <summary>A background image (with optional scrim overlay).</summary>
    Image
}

/// <summary>How a background image is sized within its area.</summary>
public enum BackgroundFit
{
    /// <summary>Scale to cover the whole area, cropping as needed (default).</summary>
    Cover,

    /// <summary>Scale to fit entirely inside the area, possibly letterboxed.</summary>
    Contain,

    /// <summary>Repeat the image at its natural size.</summary>
    Tile
}

/// <summary>Relative type scale for the hosted-page heading.</summary>
public enum HeadingSize
{
    Small,
    Medium,
    Large
}

/// <summary>Surface treatment of the form card.</summary>
public enum CardStyle
{
    /// <summary>An opaque card matching the page surface.</summary>
    Solid,

    /// <summary>A translucent, blurred "glass" card (reads well over imagery).</summary>
    Glass
}

/// <summary>Drop-shadow strength of the form card.</summary>
public enum CardShadow
{
    None,
    Soft,
    Strong
}
