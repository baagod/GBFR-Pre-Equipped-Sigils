using DearImguiSharp;
using Reloaded.Imgui.Hook;
using Reloaded.Imgui.Hook.Direct3D11;
using Reloaded.Imgui.Hook.Implementations;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace GBFR.ExtraSigilSlots.Reloaded;

internal sealed unsafe class CjkConfiguredDx11Hook : IImguiHook
{
    private readonly SafeImguiHookDx11 _inner;
    private readonly string _modDirectory;
    private readonly Action<string> _log;
    private ushort[]? _glyphRanges;
    private GCHandle _glyphRangesHandle;
    private ImFont? _font;
    private bool _disposed;

    internal CjkConfiguredDx11Hook(
        string modDirectory,
        Action presentTick,
        Func<bool> shouldRenderFrontend,
        Action<string> log,
        Action onPermanentFailure)
    {
        _modDirectory = modDirectory;
        _log = log;
        _inner = new SafeImguiHookDx11(
            presentTick,
            shouldRenderFrontend,
            log,
            onPermanentFailure);
    }

    public bool IsApiSupported() => _inner.IsApiSupported();

    public void Initialize()
    {
        ConfigureCjkFont();
        _inner.Initialize();
    }

    public void Disable() => _inner.Disable();

    public void Enable() => _inner.Enable();

    private void ConfigureCjkFont()
    {
        try
        {
            string? fontPath = FindCjkFont();
            if (fontPath is null)
            {
                _log("No CJK system font was found; ImGui will use its Latin default font.");
                return;
            }

            _glyphRanges = BuildGlyphRanges();
            _glyphRangesHandle = GCHandle.Alloc(_glyphRanges, GCHandleType.Pinned);
            ushort* glyphRanges = (ushort*)_glyphRangesHandle.AddrOfPinnedObject();
            ref ushort firstGlyphRange = ref Unsafe.AsRef<ushort>(glyphRanges);

            ImGuiIO io = ImguiHook.IO;
            ImFontAtlas atlas = io.Fonts;
            _font = ImGui.ImFontAtlasAddFontFromFileTTF(
                atlas,
                fontPath,
                18.0f,
                null!,
                ref firstGlyphRange
            );
            if (_font is null || !ImGui.ImFontAtlasBuild(atlas))
                throw new InvalidOperationException("Dear ImGui rejected the CJK font atlas.");
            io.FontDefault = _font;
            _log(
                $"CJK font loaded before DX11 hook initialization: {Path.GetFileName(fontPath)}, " +
                $"{(_glyphRanges.Length - 1) / 2} glyph ranges."
            );
        }
        catch (Exception exception)
        {
            if (_glyphRangesHandle.IsAllocated)
                _glyphRangesHandle.Free();
            _glyphRanges = null;
            _font = null;
            _log($"CJK font setup failed; continuing with the default font: {exception}");
        }
    }

    private string? FindCjkFont()
    {
        string fontsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        string[] candidates =
        [
            Path.Combine(fontsDirectory, "msyh.ttc"),
            Path.Combine(fontsDirectory, "msyhl.ttc"),
            Path.Combine(fontsDirectory, "simhei.ttf"),
            Path.Combine(fontsDirectory, "simsun.ttc"),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private ushort[] BuildGlyphRanges()
    {
        SortedSet<ushort> glyphs = [];
        for (int codePoint = 0x20; codePoint <= 0xFF; ++codePoint)
            glyphs.Add((ushort)codePoint);
        // Preset names are user-defined, so they cannot be covered by the
        // static UI seed or the localized sigil-name table alone.
        for (int codePoint = 0x3400; codePoint <= 0x9FFF; ++codePoint)
            glyphs.Add((ushort)codePoint);

        string tablePath = Path.Combine(
            _modDirectory,
            "GBFR-ExtraSigilSlots.names.zh-CN.tsv"
        );
        if (File.Exists(tablePath))
        {
            string table = File.ReadAllText(tablePath, Encoding.UTF8);
            foreach (char character in table)
            {
                if (character != '\0' && !char.IsSurrogate(character))
                    glyphs.Add(character);
            }
        }
        foreach (char character in UiLocalization.GlyphSeed)
        {
            if (character != '\0' && !char.IsSurrogate(character))
                glyphs.Add(character);
        }
        glyphs.Add(0xFFFD);

        List<ushort> ranges = [];
        bool hasRange = false;
        ushort rangeStart = 0;
        ushort rangeEnd = 0;
        foreach (ushort glyph in glyphs)
        {
            if (!hasRange)
            {
                rangeStart = rangeEnd = glyph;
                hasRange = true;
                continue;
            }
            if (glyph == rangeEnd + 1)
            {
                rangeEnd = glyph;
                continue;
            }
            ranges.Add(rangeStart);
            ranges.Add(rangeEnd);
            rangeStart = rangeEnd = glyph;
        }
        if (hasRange)
        {
            ranges.Add(rangeStart);
            ranges.Add(rangeEnd);
        }
        ranges.Add(0);
        return ranges.ToArray();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _inner.Dispose();
        _font = null;
        if (_glyphRangesHandle.IsAllocated)
            _glyphRangesHandle.Free();
        _glyphRanges = null;
    }
}
