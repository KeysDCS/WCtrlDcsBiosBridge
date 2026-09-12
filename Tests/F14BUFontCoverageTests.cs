using Newtonsoft.Json;
using WCtrlDcsBiosBridge.Aircrafts.F14;
using WwDevicesDotNet;
using Xunit;

namespace WCtrlDcsBiosBridge.Tests;

/// <summary>
/// Holds the CDNU's symbols against the font the panel is given, and against what the panel
/// will accept.
///
/// Two things have to be true for a symbol to appear, and neither says anything when it is not.
/// The font has to carry a bitmap under that character — an upload skips a character it has no
/// bitmap for. And the character has to be one of the 110 slots the device exposes: the packet
/// map is what addresses a bitmap to the panel, and McduFontPacketMap.FillPacketsWithGlyphs
/// silently drops a glyph whose character it cannot find there.
///
/// The second is the one nobody expects. The horizontal double-headed arrow was left blank for
/// exactly that reason: it is U+2194 by rights, and U+2194 is not a slot, so it had to be drawn
/// into a spare one instead (see tools/f14bu-font/build-arrows.py). A future symbol filed
/// under its proper Unicode name would fail the same way and just as quietly.
///
/// The large set is what these check, because the CDNU rows are composed without Small(): eight
/// rows of large glyphs is the whole page. The small set is short of the diamond, which costs
/// nothing while nothing asks for it and would have to be drawn before anything does.
/// </summary>
public class F14BUFontCoverageTests
{
    /// <summary>
    /// Every stand-in the DCS-BIOS module emits for a CDNU control code. The module replaces
    /// each code with a printable Latin-1 character before exporting the row — its
    /// cdnu_replace_map — and the diamond it does not replace at all.
    /// </summary>
    private const string Standins = "\u00AB\u00BB{}\u00AE\u00A9\u0013_\u00B0";

    private static T Resource<T>(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", file);
        var value = JsonConvert.DeserializeObject<T>(File.ReadAllText(path));

        Assert.NotNull(value);
        return value!;
    }

    /// <summary>Font file paired with the packet map for its glyph height.</summary>
    public static TheoryData<string, string> Fonts() => new()
    {
        { "f14bu-font-21x31.json", "WinctrlFontPacketMap-3x31.json" },
        { "f14bu-font-21x32.json", "WinctrlFontPacketMap-3x32.json" },
    };

    [Theory]
    [MemberData(nameof(Fonts))]
    public void EverySymbolTheCdnuSendsIsDrawn(string fontFile, string _)
    {
        var font = Resource<McduFontFile>(fontFile);
        var large = font.LargeGlyphs.Select(g => g.Character).ToHashSet();

        // Through the listener's own substitutions: the font is not expected to carry a glyph
        // under the character the module spells a symbol with, only under what it is mapped to.
        var gaps = F14BU_Listener.MapGlyphs(Standins)
            .Where(ch => ch != ' ')
            .Where(ch => !large.Contains(ch))
            .Distinct()
            .Select(ch => $"U+{(int)ch:X4}")
            .ToList();

        Assert.True(gaps.Count == 0, $"{fontFile}: no glyph for {string.Join(", ", gaps)}");
    }

    [Theory]
    [MemberData(nameof(Fonts))]
    public void EverySymbolTheCdnuSendsLandsInADeviceSlot(string _, string packetMapFile)
    {
        var map = Resource<McduFontPacketMap>(packetMapFile);
        var large = map.LargeGlyphOffsets.Select(o => o.Character).ToHashSet();

        var unaddressable = F14BU_Listener.MapGlyphs(Standins)
            .Where(ch => ch != ' ')
            .Where(ch => !large.Contains(ch))
            .Distinct()
            .Select(ch => $"U+{(int)ch:X4}")
            .ToList();

        Assert.True(unaddressable.Count == 0,
            $"{packetMapFile}: the device has no slot for {string.Join(", ", unaddressable)} — " +
            "pick a spare slot and draw the bitmap into that instead");
    }

    /// <summary>
    /// A control code with no mapping is blanked rather than passed through. It has no glyph
    /// under its own character, and passing it on would leave a hole in the row.
    /// </summary>
    [Fact]
    public void AnUnmappedControlCodeIsBlanked()
    {
        Assert.Equal("  ", F14BU_Listener.MapGlyphs("\u0001\u001F"));
    }

    /// <summary>Text the CDNU writes plainly comes through untouched, lowercase included.</summary>
    [Fact]
    public void PlainTextIsLeftAlone()
    {
        Assert.Equal("Bagram Departure", F14BU_Listener.MapGlyphs("Bagram Departure"));
    }
}
