using QRCoder;

namespace Authly.Web.Infrastructure.Mfa;

/// <summary>
/// Renders an SVG QR code from arbitrary text (here, a TOTP <c>otpauth://</c> URI). SVG is used
/// so it's resolution-independent and needs no System.Drawing native dependency on Linux.
/// </summary>
public static class QrCodeRenderer
{
    public static string SvgFromText(string text, int pixelsPerModule = 5)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var svg = new SvgQRCode(data);
        return svg.GetGraphic(pixelsPerModule);
    }
}
