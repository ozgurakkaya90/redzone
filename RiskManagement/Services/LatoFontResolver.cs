using System.Reflection;
using PdfSharpCore.Fonts;

namespace RiskManagement.Services;

/// <summary>
/// PDF üretimi için gömülü (embedded) Lato fontunu sağlar. Sistem fontlarına bağımlı
/// olmadığından macOS/Linux/CI dahil her platformda aynı çıktıyı verir — Docker (Linux)
/// üretim ortamı için kritiktir. Lato, SIL Open Font License 1.1 ile dağıtılır.
/// </summary>
public sealed class LatoFontResolver : IFontResolver
{
    public static readonly LatoFontResolver Instance = new();

    private static readonly byte[] Regular = Load("Lato-Regular.ttf");
    private static readonly byte[] Bold     = Load("Lato-Bold.ttf");

    public string DefaultFontName => "Lato";

    public byte[] GetFont(string faceName) =>
        faceName.Contains("#b", StringComparison.OrdinalIgnoreCase) ? Bold : Regular;

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? "Lato#b" : "Lato#");

    private static byte[] Load(string fileName)
    {
        var asm = typeof(LatoFontResolver).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .First(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(resourceName)!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
