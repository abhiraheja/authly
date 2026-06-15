using Authly.Modules.Messaging;

namespace Authly.Tests.Messaging;

public class TemplateRendererTests
{
    [Fact]
    public void Substitutes_known_variables()
    {
        var vars = new Dictionary<string, string> { ["user_name"] = "Ada", ["otp"] = "123456" };
        var result = TemplateRenderer.Render("Hi {{user_name}}, code {{otp}}.", vars, htmlEncode: false);
        Assert.Equal("Hi Ada, code 123456.", result);
    }

    [Fact]
    public void Leaves_unknown_placeholders_intact()
    {
        var result = TemplateRenderer.Render("Hi {{missing}}.", new Dictionary<string, string>(), htmlEncode: false);
        Assert.Equal("Hi {{missing}}.", result);
    }

    [Fact]
    public void Html_encodes_values_when_requested()
    {
        var vars = new Dictionary<string, string> { ["user_name"] = "<b>x</b>" };
        var html = TemplateRenderer.Render("<p>{{user_name}}</p>", vars, htmlEncode: true);
        Assert.Equal("<p>&lt;b&gt;x&lt;/b&gt;</p>", html);

        var text = TemplateRenderer.Render("{{user_name}}", vars, htmlEncode: false);
        Assert.Equal("<b>x</b>", text);
    }

    [Fact]
    public void Lists_distinct_placeholders()
    {
        var names = TemplateRenderer.Placeholders("{{a}} {{b}} {{a}}");
        Assert.Equal(new[] { "a", "b" }, names);
    }

    [Fact]
    public void Builtins_exist_for_security_critical_keys()
    {
        Assert.NotNull(BuiltInTemplates.Find(MessageTemplateKeys.VerifyEmail, Core.Enums.MessageChannel.Email));
        Assert.NotNull(BuiltInTemplates.Find(MessageTemplateKeys.Otp, Core.Enums.MessageChannel.Email));
        Assert.NotNull(BuiltInTemplates.Find(MessageTemplateKeys.Otp, Core.Enums.MessageChannel.WhatsApp));
        // verify_email must carry the action link, otp must carry the code.
        Assert.Contains("{{action_url}}", BuiltInTemplates.Find(MessageTemplateKeys.VerifyEmail, Core.Enums.MessageChannel.Email)!.Body);
        Assert.Contains("{{otp}}", BuiltInTemplates.Find(MessageTemplateKeys.Otp, Core.Enums.MessageChannel.WhatsApp)!.Body);
    }
}
