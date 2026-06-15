using Authly.Core.Authorization;

namespace Authly.Tests.Authorization;

public class PermissionEvaluatorTests
{
    [Theory]
    [InlineData("user.read", true)]   // exact grant present
    [InlineData("user.write", true)]  // exact grant present
    [InlineData("user.delete", false)] // not granted
    [InlineData("role.read", false)]   // unrelated resource
    public void Exact_grants_allow_only_the_granted_action(string required, bool expected)
    {
        var granted = new[] { "user.read", "user.write" };
        Assert.Equal(expected, PermissionEvaluator.Satisfies(granted, required));
    }

    [Fact]
    public void Resource_wildcard_grants_all_actions_on_that_resource()
    {
        var granted = new[] { "user.*" };
        Assert.True(PermissionEvaluator.Satisfies(granted, "user.delete"));
        Assert.False(PermissionEvaluator.Satisfies(granted, "role.delete"));
    }

    [Fact]
    public void Action_wildcard_grants_that_action_across_resources()
    {
        var granted = new[] { "*.read" };
        Assert.True(PermissionEvaluator.Satisfies(granted, "application.read"));
        Assert.False(PermissionEvaluator.Satisfies(granted, "application.write"));
    }

    [Fact]
    public void Global_wildcard_grants_everything()
    {
        var granted = new[] { "*" };
        Assert.True(PermissionEvaluator.Satisfies(granted, "anything.goes"));
    }

    [Fact]
    public void Empty_grant_set_denies()
        => Assert.False(PermissionEvaluator.Satisfies(Array.Empty<string>(), "user.read"));
}
