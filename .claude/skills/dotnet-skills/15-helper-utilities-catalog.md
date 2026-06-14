---
name: Helper utilities catalog
description: Inventory of the small cross-cutting helpers (EnumParsing, RandomHelpers secure password, TimeStampHelper, StringEqualsCheck) — what they do, when to prefer them over stdlib, and their known rough edges with improvement guidance.
type: skill-section
---

# Helper utilities catalog

## When to use

Every Saarvix service carries the same small bag of helper classes in `{ServiceName}.Application/Helpers/`. They are intentionally tiny and duplicated per-service (not in `Saar_Packages.Models`) because they have no inter-service contract value — they're just convenient.

## Why document them at all?

Two reasons:

1. **Prevent reinvention.** New developers reach for `DateTime.Now.Subtract(Epoch).TotalSeconds` when `TimeStampHelper.ToTimeStamp` exists.
2. **Surface the rough edges.** A few of these have subtle bugs or suboptimal behavior that should be fixed in the next service, not copy-pasted.

## Inventory

### `EnumParsing` — parse string to enum with fallback

```csharp
public static class EnumParsing
{
    public static T ToEnum<T>(this string value, T defaultValue) where T : Enum
    {
        try
        {
            if (string.IsNullOrEmpty(value)) return defaultValue;
            return (T)Enum.Parse(typeof(T), value, ignoreCase: true);
        }
        catch
        {
            return defaultValue;
        }
    }

    public static List<T> ToEnumList<T>(this Type type) where T : Enum
        => Enum.GetValues(type).Cast<T>().ToList();
}
```

**When to use:**

- Parsing an inbound string (query param, GraphQL input) into a domain enum with a safe default.

**Rough edge:** catches everything. Consider preferring `Enum.TryParse<T>(value, true, out var parsed) ? parsed : defaultValue`. Same behavior, no exception throw cost, clearer intent.

**Usage:**

```csharp
var status = input.Status.ToEnum(WalletStatus.Active);
```

---

### `RandomHelpers.GenerateSecureRandomPassword` — cryptographic password generator

```csharp
public class RandomHelpers
{
    public static string GenerateSecureRandomPassword(int length = 16)
    {
        const string upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // excludes I, O
        const string lower   = "abcdefghijkmnopqrstuvwxyz"; // excludes l
        const string digits  = "23456789";                  // excludes 0, 1
        const string special = "!@$?_-";

        var allChars      = upper + lower + digits + special;
        var passwordChars = new char[length];
        using var rng     = RandomNumberGenerator.Create();

        // Guarantee at least one of each class
        passwordChars[0] = upper  [RandomNumber(rng, upper.Length)];
        passwordChars[1] = lower  [RandomNumber(rng, lower.Length)];
        passwordChars[2] = digits [RandomNumber(rng, digits.Length)];
        passwordChars[3] = special[RandomNumber(rng, special.Length)];

        for (var i = 4; i < length; i++)
            passwordChars[i] = allChars[RandomNumber(rng, allChars.Length)];

        // Shuffle so the class-guarantees aren't always at positions 0..3
        return new string(passwordChars.OrderBy(c => RandomNumber(rng, int.MaxValue)).ToArray());
    }

    private static int RandomNumber(RandomNumberGenerator rng, int maxExclusive)
    {
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var value = BitConverter.ToUInt32(bytes, 0);
        return (int)(value % (uint)maxExclusive);
    }
}
```

**When to use:**

- Generating temporary passwords emailed to newly provisioned users (e.g., business sub-accounts).
- Initial credential generation before forced-change-on-first-login.

**Why not `Guid.NewGuid().ToString()`?** GUIDs are not uniformly random characters — they are easily recognizable, and a user typing one is hostile UX.

**Why the exclusion list?** Ambiguous characters (`O` vs `0`, `I` vs `l` vs `1`) cause support incidents when users retype passwords from emails.

**Rough edges:**

- `RandomNumber` uses `value % maxExclusive` which has modulo bias for small ranges. For password generation this is in practice negligible (character sets are small relative to `uint.MaxValue`), but use `RandomNumberGenerator.GetInt32(maxExclusive)` on .NET 6+ to eliminate the bias.
- The `OrderBy(c => RandomNumber(...))` shuffle is O(n log n) with a cryptographic RNG — works, but a Fisher–Yates shuffle with `RandomNumberGenerator.GetInt32` is more idiomatic.

**Improved version (prefer for new services):**

```csharp
public static class RandomHelpers
{
    private const string Upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower   = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits  = "23456789";
    private const string Special = "!@$?_-";

    public static string GenerateSecureRandomPassword(int length = 16)
    {
        if (length < 4) throw new ArgumentOutOfRangeException(nameof(length));

        var chars = new char[length];
        var all   = Upper + Lower + Digits + Special;

        chars[0] = Upper  [RandomNumberGenerator.GetInt32(Upper.Length)];
        chars[1] = Lower  [RandomNumberGenerator.GetInt32(Lower.Length)];
        chars[2] = Digits [RandomNumberGenerator.GetInt32(Digits.Length)];
        chars[3] = Special[RandomNumberGenerator.GetInt32(Special.Length)];
        for (var i = 4; i < length; i++)
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        // Fisher–Yates shuffle
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }
}
```

**When NOT to use:** Hashing the password is a separate concern. These helpers generate plaintext only — always feed the output into your hashing pipeline (bcrypt / Argon2 / Identity) before persisting.

---

### `TimeStampHelper` — Unix epoch seconds conversion

```csharp
public static class TimeStampHelper
{
    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static long ToTimeStamp(this DateTime date)
    {
        TimeSpan elapsedTime = date - Epoch;
        return (long)elapsedTime.TotalSeconds;
    }

    public static DateTime FromTimeStampToDate(this long timestamp)
    {
        try
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(timestamp).ToLocalTime();
        }
        catch
        {
            return new DateTime();
        }
    }
}
```

**When to use:**

- Interoperating with APIs / events that carry Unix epoch seconds (Service Bus `x-date` property).

**Rough edges:**

- `FromTimeStampToDate` returns `.ToLocalTime()` — **this is a bug waiting to happen.** Local time varies per server. All timestamps should stay UTC until the view layer converts them. Prefer returning `DateTimeOffset` or a `DateTime` with `DateTimeKind.Utc`.
- The fallback `new DateTime()` (year 0001-01-01) silently masks bad data.

**Improved version:**

```csharp
public static class TimeStampHelper
{
    public static long ToTimeStamp(this DateTime date)
        => new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)).ToUnixTimeSeconds();

    public static DateTime FromTimeStampToDate(this long timestamp)
        => DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
}
```

**Prefer `DateTimeOffset` throughout new code.** Stamping times as `DateTime` (even `Utc`) loses the offset and causes subtle bugs when the value crosses a boundary.

---

### `StringEqualsCheck` — IEnumerable<string> contains / excludes

```csharp
public static class StringEqualsCheck
{
    public static bool StringEquals(this IEnumerable<string> source, string matchWith)
    {
        foreach (var item in source)
            if (item == matchWith) return true;
        return false;
    }

    public static bool StringNotsEquals(this IEnumerable<string> source, string matchWith)
    {
        foreach (var item in source)
            if (item == matchWith) return false;
        return true;
    }
}
```

**When to use:**

- Legacy call sites that read `claims.StringEquals("read:all")`.

**Rough edges:**

- Identical to `source.Contains(matchWith)` / `!source.Contains(matchWith)`. No new value added by the extension.
- Ordinal comparison by default — fine for claim values; dangerous for user-supplied strings where culture rules might matter.
- `StringNotsEquals` (typo, should be `StringNotEquals`) is just `!source.Contains(...)`.

**Prefer in new code:**

```csharp
source.Contains("value", StringComparer.Ordinal)
!source.Contains("value", StringComparer.Ordinal)
```

Keep the helpers only as long as existing code references them. Don't add new call sites.

---

## Location convention

| Layer | What lives here |
|---|---|
| `{ServiceName}.Api/Helpers/` | Transport-specific helpers: GraphQL scalars (`UtcDateTimeScalar`), HTTP middleware helpers (`DisableIntrospectionMiddleware`), request interceptors. |
| `{ServiceName}.Application/Helpers/` | Domain-agnostic utilities: enum parsing, string extensions, time conversion, password generation. |
| `{ServiceName}.Domain/Helper/` | Domain-specific attributes and primitives (`DocumentNameAttribute`). Singular `Helper` — keep the convention. |

If a helper has side effects or dependencies (e.g., a random password generator that pulls from a vault-backed policy), it belongs in a proper service, not a static Helpers class.

## When to promote a helper to `Saar_Packages.Models`

Never, unless it is referenced by the envelope types already living there. Helpers are per-service conveniences; elevating them creates a cross-service coupling that outweighs the duplication savings.

## Common mistakes

1. **Adding new helpers with ambient dependencies.** A `static CurrentUserHelpers` reading `IHttpContextAccessor` via a static init path becomes untestable. Use services.
2. **Catch-all `try { ... } catch { return default; }` wrappers.** Swallows bugs. Prefer `TryParse` patterns.
3. **Using `.ToLocalTime()` inside helpers.** Time-zone drift per server. Always UTC in helpers.
4. **Modulo bias in RNG helpers.** Negligible here, but on .NET 6+ prefer `RandomNumberGenerator.GetInt32`.
5. **Duplicating a helper across layers.** If `EnumParsing` lives in Application, don't add an identical `Api/Helpers/EnumParsing.cs`. Reference across layers.

## Related skills

- `02-domain-base-documents-and-multitenancy.md` — `DocumentNameAttribute` lives in `Domain/Helper/`.
- `06-hotchocolate-graphql-baseline.md` — `UtcDateTimeScalar` and `DisableIntrospectionMiddleware` live in `Api/Helpers/`.
