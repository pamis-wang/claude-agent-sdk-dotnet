using PamisWang.ClaudeAgentSdk.Permissions;

namespace PamisWang.ClaudeAgentSdk.Tests;

/// <summary>選項裡用字串包成的型別與 skill 的篩選。</summary>
public sealed class OptionValueTests
{
    [Fact]
    public void PermissionMode_ComparesByCliValue()
    {
        Assert.Equal(PermissionMode.Auto, new PermissionMode("auto"));
        Assert.NotEqual(PermissionMode.Auto, PermissionMode.Default);
        Assert.Equal("acceptEdits", PermissionMode.AcceptEdits.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void PermissionMode_EmptyValue_Throws(string value) =>
        Assert.Throws<ArgumentException>(() => new PermissionMode(value));

    [Fact]
    public void SettingSource_ValueWithComma_Throws() =>
        Assert.Throws<ArgumentException>(() => new SettingSource("user,project"));

    [Fact]
    public void SkillSelection_OnlyKeepsNamesInOrder()
    {
        Assert.Equal(["pdf", "my-plugin:docx"], SkillSelection.Only("pdf", "my-plugin:docx").Names);
        Assert.Null(SkillSelection.All.Names);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" pdf")]
    [InlineData("a,b")]
    [InlineData("a(b)")]
    [InlineData("a\u0001b")]
    [InlineData("﻿pdf")]
    [InlineData("*")]
    [InlineData("plugin:*")]
    [InlineData("/pdf")]
    [InlineData(@"a\\b")]
    [InlineData(@"pdf\")]
    public void SkillSelection_NameThatCannotBeARule_Throws(string name) =>
        Assert.Throws<ArgumentException>(() => SkillSelection.Only(name));

    /// <summary>不放進 InlineData：xUnit 傳參數時會把不成對的 surrogate 換成 U+FFFD。</summary>
    [Fact]
    public void SkillSelection_UnpairedSurrogate_Throws() =>
        Assert.Throws<ArgumentException>(() => SkillSelection.Only("pdf" + (char)0xD800));
}
