/*
 * authly-webauthn.js — browser side of the passkey (WebAuthn) ceremonies.
 * Talks to the server endpoints which use Fido2NetLib JSON (base64url-encoded byte fields).
 */
(function () {
    function b64urlToBytes(s) {
        s = s.replace(/-/g, '+').replace(/_/g, '/');
        const pad = s.length % 4; if (pad) s += '='.repeat(4 - pad);
        const bin = atob(s); const out = new Uint8Array(bin.length);
        for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
        return out.buffer;
    }
    function bytesToB64url(buf) {
        const bytes = new Uint8Array(buf); let bin = '';
        for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
        return btoa(bin).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    }

    // Convert Fido2NetLib assertion options JSON → the shape navigator.credentials.get expects.
    function toGetOptions(o) {
        const p = o.publicKey || o;
        p.challenge = b64urlToBytes(p.challenge);
        (p.allowCredentials || []).forEach(c => { c.id = b64urlToBytes(c.id); });
        return { publicKey: p };
    }
    function toCreateOptions(o) {
        const p = o.publicKey || o;
        p.challenge = b64urlToBytes(p.challenge);
        p.user.id = b64urlToBytes(p.user.id);
        (p.excludeCredentials || []).forEach(c => { c.id = b64urlToBytes(c.id); });
        return { publicKey: p };
    }

    function assertionToJson(cred) {
        const r = cred.response;
        return JSON.stringify({
            id: cred.id,
            rawId: bytesToB64url(cred.rawId),
            type: cred.type,
            extensions: cred.getClientExtensionResults(),
            response: {
                authenticatorData: bytesToB64url(r.authenticatorData),
                clientDataJSON: bytesToB64url(r.clientDataJSON),
                signature: bytesToB64url(r.signature),
                userHandle: r.userHandle ? bytesToB64url(r.userHandle) : null
            }
        });
    }
    function attestationToJson(cred) {
        const r = cred.response;
        return JSON.stringify({
            id: cred.id,
            rawId: bytesToB64url(cred.rawId),
            type: cred.type,
            extensions: cred.getClientExtensionResults(),
            response: {
                attestationObject: bytesToB64url(r.attestationObject),
                clientDataJSON: bytesToB64url(r.clientDataJSON)
            }
        });
    }

    // Passwordless sign-in. emailInputId + token (antiforgery) come from the page.
    // returnUrl (optional) carries the original post-login destination (e.g. an OAuth request).
    window.authlyPasskeyLogin = async function (email, token, onError, returnUrl) {
        try {
            const form = new URLSearchParams();
            form.append('email', email);
            form.append('__RequestVerificationToken', token);
            const optResp = await fetch('/account/passkey/options', {
                method: 'POST', body: form,
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
            });
            if (!optResp.ok) { onError('No passkey is registered for that email.'); return; }
            const options = toGetOptions(await optResp.json());

            const assertion = await navigator.credentials.get(options);
            const loginUrl = returnUrl
                ? '/account/passkey/login?returnUrl=' + encodeURIComponent(returnUrl)
                : '/account/passkey/login';
            const loginResp = await fetch(loginUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: assertionToJson(assertion)
            });
            if (!loginResp.ok) { onError('Passkey sign-in failed. Try again.'); return; }
            const data = await loginResp.json();
            window.location = data.redirect || '/';
        } catch (e) {
            onError('Passkey sign-in was cancelled or failed.');
        }
    };

    // Enrolment in the portal. beginUrl/completeUrl + token come from the page.
    window.authlyPasskeyRegister = async function (beginUrl, completeUrl, friendlyName, token, onError, onDone) {
        try {
            const beginResp = await fetch(beginUrl, {
                method: 'POST', headers: { 'RequestVerificationToken': token }
            });
            if (!beginResp.ok) { onError('Could not start passkey setup.'); return; }
            const options = toCreateOptions(await beginResp.json());

            const cred = await navigator.credentials.create(options);
            const body = new URLSearchParams();
            body.append('friendlyName', friendlyName || '');
            body.append('response', attestationToJson(cred));
            body.append('__RequestVerificationToken', token);
            const completeResp = await fetch(completeUrl, {
                method: 'POST', body: body,
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
            });
            if (!completeResp.ok) { onError('Could not save the passkey.'); return; }
            onDone();
        } catch (e) {
            onError('Passkey setup was cancelled or failed.');
        }
    };
})();
