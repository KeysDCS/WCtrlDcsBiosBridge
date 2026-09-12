using Newtonsoft.Json;
using WwDevicesDotNet;
using Xunit;

namespace WCtrlDcsBiosBridge.Tests;

/// <summary>
/// Holds every font file to the shape the upload path requires.
///
/// These files are edited by hand as often as they are generated, and a slip costs more than it
/// should: a row a pixel too long throws out of McduFontGlyph.GetBytes when the font is sent, so
/// the aircraft loads, the panel is handed a font, and the whole thing falls over on a device
/// nobody can attach to a debugger. A glyph the right shape but the wrong width against the
/// header throws later still, out of McduFontPacketMap, counting bytes against the slot map.
///
/// Both are cheap to catch here and expensive to catch anywhere else.
///
/// What is not checked is which character means "off". GetBytes takes '1' and 'X' as ink and
/// everything else as blank, so the AH-64D and CH-47F fonts writing a space where the others
/// write a dot is a difference in typing, not a fault.
/// </summary>
public class FontFileTests
{
    public static TheoryData<string> FontFiles()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Resources");
        var data = new TheoryData<string>();

        foreach (var path in Directory.GetFiles(directory, "*-font-*.json").OrderBy(p => p))
            data.Add(Path.GetFileName(path));

        return data;
    }

    private static McduFontFile Font(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", file);
        var font = JsonConvert.DeserializeObject<McduFontFile>(File.ReadAllText(path));

        Assert.NotNull(font);
        return font!;
    }

    /// <summary>There is one to find. A glob that matches nothing passes every test below.</summary>
    [Fact]
    public void TheFontsAreWhereTheTestsLookForThem()
    {
        Assert.NotEmpty(FontFiles());
    }

    [Theory]
    [MemberData(nameof(FontFiles))]
    public void EveryGlyphIsTheSizeItsHeaderDeclares(string file)
    {
        var font = Font(file);

        var wrong = new List<string>();

        foreach (var (set, glyphs) in new (string, IEnumerable<McduFontGlyph>)[]
                 {
                     ("large", font.LargeGlyphs), ("small", font.SmallGlyphs),
                 })
        {
            foreach (var glyph in glyphs)
            {
                var rows = glyph.BitArray;
                var widths = rows.Select(r => r.Length).Distinct().ToList();

                if (widths.Count > 1)
                {
                    wrong.Add($"{set} U+{(int)glyph.Character:X4}: rows of {string.Join("/", widths)} " +
                              "bits — every row must be the same length");
                    continue;
                }

                if (widths.Count == 1 && widths[0] != font.GlyphWidth)
                    wrong.Add($"{set} U+{(int)glyph.Character:X4}: {widths[0]} bits wide, " +
                              $"header says {font.GlyphWidth}");

                if (rows.Length != font.GlyphHeight)
                    wrong.Add($"{set} U+{(int)glyph.Character:X4}: {rows.Length} rows, " +
                              $"header says {font.GlyphHeight}");
            }
        }

        Assert.True(wrong.Count == 0, $"{file}:{Environment.NewLine}  {string.Join($"{Environment.NewLine}  ", wrong)}");
    }

    /// <summary>
    /// One bitmap per character. The upload builds a dictionary keyed by character, so a second
    /// entry either throws or quietly wins — which of the two depends on the deserialiser, and
    /// neither is what whoever added it meant.
    /// </summary>
    [Theory]
    [MemberData(nameof(FontFiles))]
    public void NoCharacterIsDeclaredTwice(string file)
    {
        var font = Font(file);

        foreach (var (set, glyphs) in new (string, IEnumerable<McduFontGlyph>)[]
                 {
                     ("large", font.LargeGlyphs), ("small", font.SmallGlyphs),
                 })
        {
            var duplicates = glyphs
                .GroupBy(g => g.Character)
                .Where(g => g.Count() > 1)
                .Select(g => $"U+{(int)g.Key:X4} ({g.Count()}x)")
                .ToList();

            Assert.True(duplicates.Count == 0,
                $"{file} {set}: {string.Join(", ", duplicates)}");
        }
    }

    /// <summary>
    /// The path the device takes, run against every glyph. GetBytes is where a malformed row is
    /// actually noticed at runtime, and it is noticed on the USB thread mid-upload.
    /// </summary>
    [Theory]
    [MemberData(nameof(FontFiles))]
    public void EveryGlyphConvertsToBytes(string file)
    {
        var font = Font(file);

        foreach (var glyph in font.LargeGlyphs.Concat(font.SmallGlyphs))
        {
            var exception = Record.Exception(() => glyph.GetBytes());

            Assert.True(exception is null,
                $"{file} U+{(int)glyph.Character:X4}: {exception?.Message}");
        }
    }
}
