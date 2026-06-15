using System.Text.Json;
using Authly.Core.WebAuthn;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace Authly.Infrastructure.Security;

/// <summary>
/// <see cref="IWebAuthnGateway"/> backed by FIDO2 (Fido2NetLib). All library types stay inside this
/// class — Core/Modules see only the gateway contract. The relying-party id/origin are derived from
/// the CURRENT request host so passkeys work on localhost and on each tenant's custom domain.
/// </summary>
public sealed class Fido2WebAuthnGateway : IWebAuthnGateway
{
    private readonly IWebAuthnRelyingParty _rp;

    public Fido2WebAuthnGateway(IWebAuthnRelyingParty rp) => _rp = rp;

    public WebAuthnChallenge BeginRegistration(WebAuthnUser user, IReadOnlyList<byte[]> excludeCredentialIds)
    {
        var fido2 = Build();
        var fidoUser = new Fido2User { Id = user.Id, Name = user.Name, DisplayName = user.DisplayName };
        var exclude = excludeCredentialIds.Select(id => new PublicKeyCredentialDescriptor(id)).ToList();

        var options = fido2.RequestNewCredential(
            fidoUser, exclude,
            new AuthenticatorSelection { UserVerification = UserVerificationRequirement.Preferred },
            AttestationConveyancePreference.None);

        return new WebAuthnChallenge(options.ToJson(), options.ToJson());
    }

    public async Task<WebAuthnNewCredential> CompleteRegistrationAsync(string state, string responseJson, CancellationToken ct = default)
    {
        var fido2 = Build();
        var options = CredentialCreateOptions.FromJson(state);
        AuthenticatorAttestationRawResponse response;
        try
        {
            response = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(responseJson)
                       ?? throw new WebAuthnException("Empty attestation response.");
        }
        catch (JsonException ex)
        {
            throw new WebAuthnException("Malformed attestation response.", ex);
        }

        try
        {
            var result = await fido2.MakeNewCredentialAsync(
                response, options, (_, _) => Task.FromResult(true), cancellationToken: ct);

            var cred = result.Result ?? throw new WebAuthnException("Attestation verification failed.");
            return new WebAuthnNewCredential(cred.CredentialId, cred.PublicKey, cred.Counter, cred.Aaguid);
        }
        catch (Fido2VerificationException ex)
        {
            throw new WebAuthnException(ex.Message, ex);
        }
    }

    public WebAuthnChallenge BeginAssertion(IReadOnlyList<byte[]> allowedCredentialIds)
    {
        var fido2 = Build();
        var allowed = allowedCredentialIds.Select(id => new PublicKeyCredentialDescriptor(id)).ToList();
        var options = fido2.GetAssertionOptions(allowed, UserVerificationRequirement.Preferred);
        return new WebAuthnChallenge(options.ToJson(), options.ToJson());
    }

    public async Task<WebAuthnAssertionResult> CompleteAssertionAsync(string state, string responseJson,
        IReadOnlyList<WebAuthnStoredCredential> credentials, CancellationToken ct = default)
    {
        var fido2 = Build();
        var options = AssertionOptions.FromJson(state);
        AuthenticatorAssertionRawResponse response;
        try
        {
            response = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(responseJson)
                       ?? throw new WebAuthnException("Empty assertion response.");
        }
        catch (JsonException ex)
        {
            throw new WebAuthnException("Malformed assertion response.", ex);
        }

        var stored = credentials.FirstOrDefault(c => c.CredentialId.AsSpan().SequenceEqual(response.Id))
                     ?? throw new WebAuthnException("Assertion used an unknown credential.");

        try
        {
            var result = await fido2.MakeAssertionAsync(
                response, options, stored.PublicKey, stored.SignCount,
                (_, _) => Task.FromResult(true), cancellationToken: ct);

            return new WebAuthnAssertionResult(result.CredentialId, result.Counter);
        }
        catch (Fido2VerificationException ex)
        {
            throw new WebAuthnException(ex.Message, ex);
        }
    }

    /// <summary>Builds a request-scoped FIDO2 instance bound to the current host (rpId) and origin.</summary>
    private Fido2 Build()
    {
        var rp = _rp.Current();
        return new Fido2(new Fido2Configuration
        {
            ServerDomain = rp.RpId,
            ServerName = "Authly",
            Origins = new HashSet<string> { rp.Origin }
        });
    }
}
