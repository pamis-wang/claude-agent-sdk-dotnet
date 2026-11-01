using PamisWang.ClaudeAgentSdk.Transport;

namespace PamisWang.ClaudeAgentSdk.Tests.Transport;

public sealed class CliVersionCheckTests
{
    [Theory]
    [InlineData("2.1.309 (Claude Code)", "2.1.309")]
    [InlineData("  2.0.0\n", "2.0.0")]
    [InlineData("10.20.30-beta.1", "10.20.30")]
    public void Parse_LeadingVersion_ReturnsIt(string output, string expected)
    {
        Assert.Equal(Version.Parse(expected), CliVersionCheck.Parse(output));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Claude Code 2.1.309")]
    [InlineData("2.1")]
    [InlineData("99999999999.0.0")]
    public void Parse_NoUsableVersion_ReturnsNull(string output)
    {
        Assert.Null(CliVersionCheck.Parse(output));
    }
}
