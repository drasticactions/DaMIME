using System.IO;
using System.Text;

namespace DaMIME.Tests;

[TestClass]
public class MimeTypeTests
{
    [TestMethod]
    public void For_WithNoParameters_ReturnsBinary()
    {
        var result = MimeType.For();
        Assert.AreEqual("application/octet-stream", result);
    }

    [TestMethod]
    public void For_WithExtensionOnly_ReturnsCorrectType()
    {
        Assert.AreEqual("image/jpeg", MimeType.For(extension: "jpg"));
        Assert.AreEqual("image/png", MimeType.For(extension: "png"));
        Assert.AreEqual("application/pdf", MimeType.For(extension: "pdf"));
        Assert.AreEqual("text/plain", MimeType.For(extension: "txt"));
    }

    [TestMethod]
    public void For_WithExtensionIncludingDot_ReturnsCorrectType()
    {
        Assert.AreEqual("image/jpeg", MimeType.For(extension: ".jpg"));
        Assert.AreEqual("image/png", MimeType.For(extension: ".png"));
    }

    [TestMethod]
    public void For_WithUppercaseExtension_ReturnsCorrectType()
    {
        Assert.AreEqual("image/jpeg", MimeType.For(extension: "JPG"));
        Assert.AreEqual("image/png", MimeType.For(extension: ".PNG"));
        Assert.AreEqual("application/pdf", MimeType.For(extension: "PDF"));
    }

    [TestMethod]
    public void For_WithNameOnly_ReturnsCorrectType()
    {
        Assert.AreEqual("image/jpeg", MimeType.For(name: "photo.jpg"));
        Assert.AreEqual("image/png", MimeType.For(name: "image.png"));
        Assert.AreEqual("application/pdf", MimeType.For(name: "document.pdf"));
        Assert.AreEqual("text/plain", MimeType.For(name: "readme.txt"));
    }

    [TestMethod]
    public void For_WithPathInName_ReturnsCorrectType()
    {
        Assert.AreEqual("image/jpeg", MimeType.For(name: "/path/to/photo.jpg"));
        Assert.AreEqual("image/png", MimeType.For(name: "C:\\Users\\test\\image.png"));
    }

    [TestMethod]
    public void For_WithDeclaredTypeOnly_ReturnsCorrectType()
    {
        Assert.AreEqual("text/html", MimeType.For(declaredType: "text/html"));
        Assert.AreEqual("application/json", MimeType.For(declaredType: "application/json"));
    }

    [TestMethod]
    public void For_WithDeclaredTypeAndCharset_ReturnsTypeWithoutCharset()
    {
        Assert.AreEqual("text/html", MimeType.For(declaredType: "text/html; charset=utf-8"));
        Assert.AreEqual("text/plain", MimeType.For(declaredType: "text/plain; charset=iso-8859-1"));
    }

    [TestMethod]
    public void For_WithDeclaredOctetStream_UsesExtension()
    {
        // application/octet-stream should be treated as "undeclared" and allow extension to be used
        Assert.AreEqual("application/pdf", MimeType.For(
            declaredType: "application/octet-stream",
            extension: "pdf"));
    }

    [TestMethod]
    public void For_WithJpegStream_ReturnsJpegType()
    {
        // JPEG magic bytes: FF D8 FF
        var jpegData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        using var stream = new MemoryStream(jpegData);

        var result = MimeType.For(stream);
        Assert.AreEqual("image/jpeg", result);
    }

    [TestMethod]
    public void For_WithPngStream_ReturnsPngType()
    {
        // PNG magic bytes: 89 50 4E 47 0D 0A 1A 0A
        var pngData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var stream = new MemoryStream(pngData);

        var result = MimeType.For(stream);
        Assert.AreEqual("image/png", result);
    }

    [TestMethod]
    public void For_WithPdfStream_ReturnsPdfType()
    {
        // PDF magic bytes: %PDF
        var pdfData = Encoding.ASCII.GetBytes("%PDF-1.4");
        using var stream = new MemoryStream(pdfData);

        var result = MimeType.For(stream);
        Assert.AreEqual("application/pdf", result);
    }

    [TestMethod]
    public void For_MagicBytesOverrideWrongExtension()
    {
        // PNG data with .jpg name
        var pngData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var stream = new MemoryStream(pngData);

        var result = MimeType.For(stream, name: "fake.jpg");
        // Magic bytes should win
        Assert.AreEqual("image/png", result);
    }

    [TestMethod]
    public void For_ExtensionRefinesParentType()
    {
        // PDF magic bytes with .ai extension (Adobe Illustrator is child of PDF)
        var pdfData = Encoding.ASCII.GetBytes("%PDF-1.4");
        using var stream = new MemoryStream(pdfData);

        var result = MimeType.For(stream, extension: "ai");
        // Should refine to illustrator (child of PDF)
        Assert.AreEqual("application/illustrator", result);
    }

    [TestMethod]
    public void For_PreservesStreamPosition()
    {
        var pngData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
        using var stream = new MemoryStream(pngData);
        stream.Position = 5; // Set to non-zero position

        MimeType.For(stream);

        // Position should be preserved
        Assert.AreEqual(5, stream.Position);
    }

    [TestMethod]
    public void ParseMediaType_WithSimpleType_ReturnsType()
    {
        Assert.AreEqual("text/html", MimeType.ParseMediaType("text/html"));
        Assert.AreEqual("application/json", MimeType.ParseMediaType("application/json"));
    }

    [TestMethod]
    public void ParseMediaType_WithCharset_ReturnsTypeOnly()
    {
        Assert.AreEqual("text/html", MimeType.ParseMediaType("text/html; charset=utf-8"));
        Assert.AreEqual("text/plain", MimeType.ParseMediaType("text/plain;charset=iso-8859-1"));
    }

    [TestMethod]
    public void ParseMediaType_WithMultipleParameters_ReturnsTypeOnly()
    {
        Assert.AreEqual("multipart/form-data",
            MimeType.ParseMediaType("multipart/form-data; boundary=----WebKit; charset=utf-8"));
    }

    [TestMethod]
    public void ParseMediaType_WithInvalidInput_ReturnsNull()
    {
        Assert.IsNull(MimeType.ParseMediaType(null));
        Assert.IsNull(MimeType.ParseMediaType(""));
        Assert.IsNull(MimeType.ParseMediaType("   "));
        Assert.IsNull(MimeType.ParseMediaType("invalid")); // No slash
    }

    [TestMethod]
    public void ParseMediaType_WithUppercase_ReturnsLowercase()
    {
        Assert.AreEqual("text/html", MimeType.ParseMediaType("TEXT/HTML"));
        Assert.AreEqual("application/json", MimeType.ParseMediaType("Application/JSON"));
    }

    [TestMethod]
    public void MostSpecificType_WithSingleCandidate_ReturnsThatType()
    {
        Assert.AreEqual("text/plain", MimeType.MostSpecificType("text/plain"));
    }

    [TestMethod]
    public void MostSpecificType_WithParentAndChild_ReturnsChild()
    {
        // text/csv is child of text/plain
        Assert.AreEqual("text/csv", MimeType.MostSpecificType("text/plain", "text/csv"));
        Assert.AreEqual("text/csv", MimeType.MostSpecificType("text/csv", "text/plain")); // Order shouldn't matter
    }

    [TestMethod]
    public void MostSpecificType_WithUnrelatedTypes_ReturnsFirst()
    {
        // image/png and text/plain are unrelated
        Assert.AreEqual("image/png", MimeType.MostSpecificType("image/png", "text/plain"));
    }

    [TestMethod]
    public void MostSpecificType_WithNullCandidates_HandlesGracefully()
    {
        Assert.AreEqual("text/plain", MimeType.MostSpecificType(null, "text/plain", null));
        Assert.IsNull(MimeType.MostSpecificType(null, null));
    }

    [TestMethod]
    public void MostSpecificType_IllustratorAndPdf_ReturnsIllustrator()
    {
        // application/illustrator is child of application/pdf
        Assert.AreEqual("application/illustrator",
            MimeType.MostSpecificType("application/pdf", "application/illustrator"));
    }
}
