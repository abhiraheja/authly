using Authly.Core.WebAuthn;

namespace Authly.Web.Infrastructure.WebAuthn;

/// <summary>
/// Resolves the WebAuthn relying-party id (rpId) and origin from the current request host so
/// passkeys bind to whatever domain the user is on — localhost in dev, or a tenant's custom
/// domain in production. Implements the Core seam the Infrastructure FIDO2 gateway depends on.
/// </summary>
public sealed class WebAuthnRelyingParty : IWebAuthnRelyingParty
{
    private readonly IHttpContextAccessor _http;

    public WebAuthnRelyingParty(IHttpContextAccessor http) => _http = http;

    public RelyingPartyInfo Current()
    {
        var request = _http.HttpContext?.Request;
        if (request is null)
            return new RelyingPartyInfo("localhost", "https://localhost");

        // rpId is the bare host (no port); the origin is the full scheme://host[:port].
        return new RelyingPartyInfo(request.Host.Host, $"{request.Scheme}://{request.Host.Value}");
    }
}
