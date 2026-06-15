namespace Authly.Core.Authorization;

/// <summary>
/// Pure decision logic for permission checks. A granted permission satisfies a required
/// <c>resource.action</c> if it matches exactly or via a wildcard: <c>*</c> (all), <c>user.*</c>
/// (all actions on a resource), or <c>*.read</c> (an action on all resources). Wildcards are a
/// convenience for custom roles; the system catalogue uses explicit grants.
/// </summary>
public static class PermissionEvaluator
{
    public static bool Satisfies(IEnumerable<string> granted, string required)
    {
        if (string.IsNullOrWhiteSpace(required)) return false;

        var (reqResource, reqAction) = Split(required);

        foreach (var g in granted)
        {
            if (string.IsNullOrWhiteSpace(g)) continue;
            if (g == "*") return true;

            var (resource, action) = Split(g);
            var resourceMatch = resource == "*" || string.Equals(resource, reqResource, StringComparison.OrdinalIgnoreCase);
            var actionMatch = action == "*" || string.Equals(action, reqAction, StringComparison.OrdinalIgnoreCase);
            if (resourceMatch && actionMatch) return true;
        }

        return false;
    }

    private static (string Resource, string Action) Split(string permission)
    {
        var dot = permission.IndexOf('.');
        return dot < 0
            ? (permission, "*")                                  // bare "user" means any action on user
            : (permission[..dot], permission[(dot + 1)..]);
    }
}
