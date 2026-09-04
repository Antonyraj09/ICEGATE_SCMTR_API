using Icegate.Integration.Configuration;
using Xunit;

namespace Icegate.Integration.Tests.Configuration;

public class IcegateSettingsTests
{
    [Fact]
    public void IsConfirmed_ReturnsFalse_ForPlaceholder()
    {
        var settings = new IcegateSettings();

        Assert.False(settings.IsConfirmed(IcegateSettings.ConfirmWithIcegatePlaceholder));
    }

    [Fact]
    public void IsConfirmed_ReturnsFalse_ForPlaceholder_CaseInsensitive()
    {
        var settings = new IcegateSettings();

        Assert.False(settings.IsConfirmed("confirm_with_icegate"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsConfirmed_ReturnsFalse_ForBlankValues(string? url)
    {
        var settings = new IcegateSettings();

        Assert.False(settings.IsConfirmed(url));
    }

    [Fact]
    public void IsConfirmed_ReturnsTrue_ForRealUrl()
    {
        var settings = new IcegateSettings();

        Assert.True(settings.IsConfirmed("https://apiwso2uat.icegate.gov.in/uploadFile/1.0/getAck"));
    }

    [Fact]
    public void DefaultGetAcknowledgementUrl_IsUnconfirmedPlaceholder()
    {
        var settings = new IcegateSettings();

        Assert.False(settings.IsConfirmed(settings.GetAcknowledgementUrl));
    }
}
