using System.Buffers;
using System.Text;

namespace PamisWang.ClaudeAgentSdk;

/// <summary>
/// 主對話可以用哪些 skill，用在 <see cref="ClaudeAgentOptions.Skills"/>。對應 Python SDK 的 <c>skills</c>（<c>"all"</c> 或名稱清單）。
/// 這是 context 的篩選，不是沙箱：沒列的 skill 不會出現在模型的清單裡，Skill 工具也會拒絕，但檔案還在磁碟上，Read 或 Bash 仍然讀得到。
/// </summary>
public sealed class SkillSelection
{
    // 名稱會寫進 --allowedTools 的 Skill(名稱) 規則，CLI 用括號外的逗號與空白切開規則，而且不認跳脫字元；跟 Python 擋的字元一樣
    private static readonly SearchValues<char> InvalidCharacters = SearchValues.Create(
        "(),﻿" + string.Concat(Enumerable.Range(0, 0x20).Concat(Enumerable.Range(0x7F, 0x21)).Select(code => (char)code)));

    private SkillSelection(IReadOnlyList<string>? names)
    {
        Names = names;
    }

    /// <summary>所有找得到的 skill。</summary>
    public static SkillSelection All { get; } = new(names: null);

    /// <summary>只能用這些 skill；<see langword="null"/> 表示全部（<see cref="All"/>）。</summary>
    public IReadOnlyList<string>? Names { get; }

    /// <summary>只能用列出來的 skill。名稱跟 SKILL.md 的 <c>name</c> 或資料夾名稱一樣；外掛的 skill 寫成 <c>外掛:skill</c>。</summary>
    /// <exception cref="ArgumentException">
    /// 名稱是空的、前後有空白、含有括號、逗號、控制字元、BOM 或不成對的 surrogate、是 <c>*</c> 或以 <c>:*</c> 結尾的萬用字元、
    /// 以 <c>/</c> 開頭、含有連續的反斜線，或以反斜線結尾。這些名稱放進規則後會切錯，或永遠對不到任何 skill；跟 Python SDK 擋的一樣。
    /// </exception>
    public static SkillSelection Only(params IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        List<string> list = [.. names];
        foreach (var name in list)
        {
            Validate(name);
        }

        return new SkillSelection(list);
    }

    private static void Validate(string name)
    {
        var reason = name switch
        {
            null => "名稱不能是 null",
            _ when string.IsNullOrWhiteSpace(name) => "名稱是空的",
            _ when !IsWellFormedUtf16(name) => "含有不成對的 surrogate，CLI 找到的 skill 不可能有這種名稱",
            _ when name != name.Trim() => "前後不能有空白：Skill 工具會把名稱前後的空白去掉，永遠對不上",
            _ when name.AsSpan().ContainsAny(InvalidCharacters) => "不能含有括號、逗號、控制字元或 BOM；名稱是 skill 的資料夾名稱，外掛的寫成「外掛:skill」",
            "*" => "要開放全部的 skill 請用 SkillSelection.All",
            _ when name.EndsWith(":*", StringComparison.Ordinal) || name.EndsWith(" *", StringComparison.Ordinal) => "不能用萬用字元，請逐一列出名稱",
            _ when name.StartsWith('/') => "不能以 / 開頭：這裡要的是 skill 的名稱，不是斜線指令",
            _ when name.Contains(@"\\", StringComparison.Ordinal) => "不能含有連續的反斜線：規則的解析會把它們合併，對到的是別的 skill",
            _ when name.EndsWith('\\') => "不能以單獨的反斜線結尾",
            _ => null,
        };

        if (reason is not null)
        {
            throw new ArgumentException($"skill 名稱「{name}」不能用：{reason}。", nameof(name));
        }
    }

    private static bool IsWellFormedUtf16(string value)
    {
        try
        {
            _ = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }
}
