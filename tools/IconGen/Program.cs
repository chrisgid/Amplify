// Amplify logo asset generator (dev-only; see IconGen.csproj).
//
// Renders the "rising level" mark — four ascending pill bars, no container — into the full MSIX
// icon/tile asset matrix plus a multi-resolution AppIcon.ico. The geometry and colours mirror the
// master vector source in design/logo/amplify-icon.svg exactly (48-unit grid scaled to a 256 frame).
//
// Usage:  dotnet run --project tools/IconGen  [outputAssetsDir]
// Default output: <repoRoot>/src/Amplify.App/Assets

using SkiaSharp;

// ---- Master geometry (256-space), identical to amplify-icon.svg ----------------------------------
// Four bars: width 32, corner radius 16, shared baseline y = 218.667.
var bars = new (float X, float Top, float Height)[]
{
    (32f,      160f,     58.667f),
    (85.333f,  133.333f, 85.333f),
    (138.667f, 101.333f, 117.333f),
    (192f,     58.667f,  160f),
};
const float BarWidth = 32f;
const float BarRadius = 16f;

// Ink bounding box of the four bars within the 256 frame.
const float InkLeft = 32f;
const float InkTop = 58.667f;
const float InkWidth = 192f;          // 224 - 32
const float InkHeight = 160f;         // 218.667 - 58.667

// ---- Colours -------------------------------------------------------------------------------------
var fillStops = new[] { new SKColor(0x5B, 0xB4, 0xFF), new SKColor(0x1E, 0x8A, 0xF0), new SKColor(0x0A, 0x5B, 0xD6) };
var fillPos = new[] { 0f, 0.5f, 1f };
var highlightStops = new[] { new SKColor(0xFF, 0xFF, 0xFF, 140), new SKColor(0xFF, 0xFF, 0xFF, 0) }; // 0.55 -> 0
var highlightPos = new[] { 0f, 0.28f };
var shadowColor = new SKColor(0x06, 0x2A, 0x63, 77); // #062a63 @ 30%

// Mark styles.
const int StyleColor = 0, StyleMonoBlack = 1, StyleMonoWhite = 2;

// ---- Drawing -------------------------------------------------------------------------------------
void DrawMark(SKCanvas canvas, float canvasW, float canvasH, float inkWidthPx, int style, bool shadow, bool highlight)
{
    float scale = inkWidthPx / InkWidth;
    float offX = (canvasW - InkWidth * scale) / 2f - InkLeft * scale;
    float offY = (canvasH - InkHeight * scale) / 2f - InkTop * scale;

    SKRect BarRect((float X, float Top, float Height) b)
    {
        float x = b.X * scale + offX;
        float y = b.Top * scale + offY;
        return new SKRect(x, y, x + BarWidth * scale, y + b.Height * scale);
    }
    float r = BarRadius * scale;

    if (shadow)
    {
        var sp = new SKPaint
        {
            ImageFilter = SKImageFilter.CreateDropShadow(0f, 2.2f * scale, 2.4f * scale, 2.4f * scale, shadowColor),
        };
        canvas.SaveLayer(sp);
        sp.Dispose();
    }

    foreach (var b in bars)
    {
        var rect = BarRect(b);
        using var paint = new SKPaint { IsAntialias = true };
        if (style == StyleColor)
        {
            var start = new SKPoint(rect.Left + 0.12f * rect.Width, rect.Top);
            var end = new SKPoint(rect.Left + 0.86f * rect.Width, rect.Bottom);
            paint.Shader = SKShader.CreateLinearGradient(start, end, fillStops, fillPos, SKShaderTileMode.Clamp);
        }
        else
        {
            paint.Color = style == StyleMonoBlack ? SKColors.Black : SKColors.White;
        }
        canvas.DrawRoundRect(rect, r, r, paint);
    }

    if (highlight && style == StyleColor)
    {
        foreach (var b in bars)
        {
            var rect = BarRect(b);
            var start = new SKPoint(rect.Left + 0.12f * rect.Width, rect.Top);
            var end = new SKPoint(rect.Left + 0.86f * rect.Width, rect.Bottom);
            using var paint = new SKPaint { IsAntialias = true };
            paint.Shader = SKShader.CreateLinearGradient(start, end, highlightStops, highlightPos, SKShaderTileMode.Clamp);
            canvas.DrawRoundRect(rect, r, r, paint);
        }
    }

    if (shadow) canvas.Restore();
}

byte[] RenderPng(int width, int height, float inkWidthPx, int style, bool shadow, bool highlight)
{
    var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var surface = SKSurface.Create(info);
    surface.Canvas.Clear(SKColors.Transparent);
    DrawMark(surface.Canvas, width, height, inkWidthPx, style, shadow, highlight);
    surface.Canvas.Flush();
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
}

// Square asset: ink width = fraction of the side; shadow/highlight only when large enough to read.
byte[] RenderSquare(int side, float inkFraction, int style)
{
    bool detail = side >= 48 && style == StyleColor;
    return RenderPng(side, side, side * inkFraction, style, detail, detail);
}

// ---- ICO (embeds PNG entries for each size) ------------------------------------------------------
void WriteIco(string path, int[] sizes)
{
    var images = new List<byte[]>();
    foreach (int s in sizes)
    {
        bool detail = s >= 48;
        images.Add(RenderPng(s, s, s * 0.86f, StyleColor, detail, detail));
    }

    using var fs = File.Create(path);
    using var w = new BinaryWriter(fs);
    w.Write((short)0);              // reserved
    w.Write((short)1);              // type: icon
    w.Write((short)sizes.Length);   // count

    int offset = 6 + 16 * sizes.Length;
    for (int i = 0; i < sizes.Length; i++)
    {
        int s = sizes[i];
        w.Write((byte)(s >= 256 ? 0 : s)); // width  (0 => 256)
        w.Write((byte)(s >= 256 ? 0 : s)); // height (0 => 256)
        w.Write((byte)0);                  // palette count
        w.Write((byte)0);                  // reserved
        w.Write((short)1);                 // colour planes
        w.Write((short)32);                // bits per pixel
        w.Write(images[i].Length);         // bytes in resource
        w.Write(offset);                   // offset to image data
        offset += images[i].Length;
    }
    foreach (var img in images) w.Write(img);
}

// ---- Locate the app's Assets folder --------------------------------------------------------------
string FindAssetsDir(string[] args)
{
    if (args.Length > 0) return args[0];

    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Amplify.slnx")))
                return Path.Combine(dir.FullName, "src", "Amplify.App", "Assets");
            dir = dir.Parent;
        }
    }
    throw new DirectoryNotFoundException("Could not locate the repo root (Amplify.slnx). Pass the Assets dir as an argument.");
}

var assets = FindAssetsDir(args);
Directory.CreateDirectory(assets);
void Save(string name, byte[] png)
{
    File.WriteAllBytes(Path.Combine(assets, name), png);
    Console.WriteLine($"  {name}");
}

Console.WriteLine($"Generating Amplify assets into {assets}");

// Square44x44Logo — app-list / taskbar (plated). Mark ~72% of the canvas.
foreach (var (suffix, px) in new[] { ("scale-100", 44), ("scale-125", 55), ("scale-150", 66), ("scale-200", 88), ("scale-400", 176) })
    Save($"Square44x44Logo.{suffix}.png", RenderSquare(px, 0.72f, StyleColor));

// Square44x44Logo — unplated target sizes (taskbar / Start / ALT+TAB). Mark ~86%.
foreach (int px in new[] { 16, 24, 32, 48, 256 })
{
    Save($"Square44x44Logo.targetsize-{px}_altform-unplated.png", RenderSquare(px, 0.86f, StyleColor));
    // High Contrast Black theme has a black background (loads contrast-black) → white mark;
    // High Contrast White theme has a white background (loads contrast-white) → black mark.
    Save($"Square44x44Logo.targetsize-{px}_altform-unplated_contrast-black.png", RenderSquare(px, 0.86f, StyleMonoWhite));
    Save($"Square44x44Logo.targetsize-{px}_altform-unplated_contrast-white.png", RenderSquare(px, 0.86f, StyleMonoBlack));
}

// Square150x150Logo — medium Start tile. Mark ~50% of the tile.
foreach (var (suffix, px) in new[] { ("scale-100", 150), ("scale-125", 188), ("scale-150", 225), ("scale-200", 300), ("scale-400", 600) })
    Save($"Square150x150Logo.{suffix}.png", RenderSquare(px, 0.50f, StyleColor));

// StoreLogo — installer / Partner Center. Mark ~72%.
foreach (var (suffix, px) in new[] { ("scale-100", 50), ("scale-125", 63), ("scale-150", 75), ("scale-200", 100), ("scale-400", 200) })
    Save($"StoreLogo.{suffix}.png", RenderSquare(px, 0.72f, StyleColor));

// SplashScreen — mark centred on transparent; ink height ~42% of the splash height.
foreach (var (suffix, w2, h2) in new[] { ("scale-100", 620, 300), ("scale-200", 1240, 600) })
    Save($"SplashScreen.{suffix}.png", RenderPng(w2, h2, 0.42f * h2 * (InkWidth / InkHeight), StyleColor, true, true));

// AppIcon.ico — window + system tray.
WriteIco(Path.Combine(assets, "AppIcon.ico"), new[] { 16, 24, 32, 48, 64, 128, 256 });
Console.WriteLine("  AppIcon.ico");

Console.WriteLine("Done.");
