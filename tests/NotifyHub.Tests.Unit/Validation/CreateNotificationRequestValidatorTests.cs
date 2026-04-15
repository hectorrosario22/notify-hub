using FluentValidation.TestHelper;
using NotifyHub.Api.Validation;
using NotifyHub.Contracts.Requests;

namespace NotifyHub.Tests.Unit.Validation;

public class CreateNotificationRequestValidatorTests
{
    private readonly CreateNotificationRequestValidator _validator = new();

    private static CreateNotificationRequest ValidRequest() => new(
        RecipientUserId: Guid.NewGuid(),
        Title: "Test Title",
        Body: "Test Body",
        Channels: new Dictionary<string, string> { { "push", "device-token" } });

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyRecipientUserId_FailsValidation()
    {
        var request = ValidRequest() with { RecipientUserId = Guid.Empty };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.RecipientUserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrNullTitle_FailsValidation(string? title)
    {
        var request = ValidRequest() with { Title = title! };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void TitleExceeds500Chars_FailsValidation()
    {
        var request = ValidRequest() with { Title = new string('a', 501) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrNullBody_FailsValidation(string? body)
    {
        var request = ValidRequest() with { Body = body! };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Body);
    }

    [Fact]
    public void BodyExceeds4000Chars_FailsValidation()
    {
        var request = ValidRequest() with { Body = new string('a', 4001) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Body);
    }

    [Fact]
    public void EmptyChannels_FailsValidation()
    {
        var request = ValidRequest() with { Channels = new Dictionary<string, string>() };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Channels);
    }

    [Fact]
    public void InvalidChannelKey_FailsValidation()
    {
        var request = ValidRequest() with
        {
            Channels = new Dictionary<string, string> { { "telegram", "recipient" } }
        };
        var result = _validator.TestValidate(request);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void EmptyChannelRecipient_FailsValidation()
    {
        var request = ValidRequest() with
        {
            Channels = new Dictionary<string, string> { { "email", "" } }
        };
        var result = _validator.TestValidate(request);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("push")]
    [InlineData("email")]
    [InlineData("sms")]
    [InlineData("whatsapp")]
    public void ValidChannelKeys_PassValidation(string channel)
    {
        var request = ValidRequest() with
        {
            Channels = new Dictionary<string, string> { { channel, "recipient" } }
        };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
