using System.IO;
using System.Text;

namespace DaMIME.Tests;

[TestClass]
public class MagicTests
{
    [TestMethod]
    public void ByExtension_WithValidExtension_ReturnsType()
    {
        Assert.AreEqual("image/jpeg", Magic.ByExtension("jpg"));
        Assert.AreEqual("image/png", Magic.ByExtension("png"));
        Assert.AreEqual("application/pdf", Magic.ByExtension("pdf"));
    }

    [TestMethod]
    public void ByExtension_WithLeadingDot_ReturnsType()
    {
        Assert.AreEqual("image/jpeg", Magic.ByExtension(".jpg"));
        Assert.AreEqual("image/png", Magic.ByExtension(".png"));
    }

    [TestMethod]
    public void ByExtension_CaseInsensitive()
    {
        Assert.AreEqual("image/jpeg", Magic.ByExtension("JPG"));
        Assert.AreEqual("image/png", Magic.ByExtension("PNG"));
        Assert.AreEqual("application/pdf", Magic.ByExtension("Pdf"));
    }

    [TestMethod]
    public void ByExtension_WithUnknownExtension_ReturnsNull()
    {
        Assert.IsNull(Magic.ByExtension("unknown"));
        Assert.IsNull(Magic.ByExtension("unknownextensionthatshouldnotexist"));
    }

    [TestMethod]
    public void ByExtension_WithNullOrEmpty_ReturnsNull()
    {
        Assert.IsNull(Magic.ByExtension(null));
        Assert.IsNull(Magic.ByExtension(""));
        Assert.IsNull(Magic.ByExtension("   "));
    }

    [TestMethod]
    public void ByPath_WithValidPath_ReturnsType()
    {
        Assert.AreEqual("image/jpeg", Magic.ByPath("photo.jpg"));
        Assert.AreEqual("image/png", Magic.ByPath("/path/to/image.png"));
        Assert.AreEqual("application/pdf", Magic.ByPath("C:\\Documents\\file.pdf"));
    }

    [TestMethod]
    public void ByPath_WithNoExtension_ReturnsNull()
    {
        Assert.IsNull(Magic.ByPath("filename"));
        Assert.IsNull(Magic.ByPath("/path/to/file"));
    }

    [TestMethod]
    public void ByMagic_WithJpeg_ReturnsJpegType()
    {
        var jpegData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        using var stream = new MemoryStream(jpegData);

        Assert.AreEqual("image/jpeg", Magic.ByMagic(stream));
    }

    [TestMethod]
    public void ByMagic_WithPng_ReturnsPngType()
    {
        var pngData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var stream = new MemoryStream(pngData);

        Assert.AreEqual("image/png", Magic.ByMagic(stream));
    }

    [TestMethod]
    public void ByMagic_WithGif87_ReturnsGifType()
    {
        var gifData = Encoding.ASCII.GetBytes("GIF87a");
        using var stream = new MemoryStream(gifData);

        Assert.AreEqual("image/gif", Magic.ByMagic(stream));
    }

    [TestMethod]
    public void ByMagic_WithGif89_ReturnsGifType()
    {
        var gifData = Encoding.ASCII.GetBytes("GIF89a");
        using var stream = new MemoryStream(gifData);

        Assert.AreEqual("image/gif", Magic.ByMagic(stream));
    }

    [TestMethod]
    public void ByMagic_WithPdf_ReturnsPdfType()
    {
        var pdfData = Encoding.ASCII.GetBytes("%PDF-1.4");
        using var stream = new MemoryStream(pdfData);

        Assert.AreEqual("application/pdf", Magic.ByMagic(stream));
    }

    [TestMethod]
    public void ByMagic_WithZip_ReturnsZipType()
    {
        var zipData = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // PK\x03\x04
        using var stream = new MemoryStream(zipData);

        Assert.AreEqual("application/zip", Magic.ByMagic(stream));
    }

    [TestMethod]
    public void ByMagic_WithWebP_ReturnsWebPType()
    {
        // WebP: RIFF....WEBP (magic at offset 8)
        var webpData = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
        using var stream = new MemoryStream(webpData);

        Assert.AreEqual("image/webp", Magic.ByMagic(stream));
    }

    [TestMethod]
    public void ByMagic_WithFlac_ReturnsFlacType()
    {
        var flacData = Encoding.ASCII.GetBytes("fLaC");
        using var stream = new MemoryStream(flacData);

        // Base Tika tables have audio/x-flac, Definitions extends to audio/flac
        var result = Magic.ByMagic(stream);
        Assert.IsTrue(result == "audio/x-flac" || result == "audio/flac", $"Expected FLAC type, got {result}");
    }

    [TestMethod]
    public void ByMagic_WithOggVorbis_ReturnsOggType()
    {
        // OGG with vorbis codec
        var oggData = new byte[40];
        Encoding.ASCII.GetBytes("OggS").CopyTo(oggData, 0);
        Encoding.ASCII.GetBytes("vorbis").CopyTo(oggData, 29);
        using var stream = new MemoryStream(oggData);

        // Base Tika has audio/vorbis, Definitions can extend to audio/ogg
        var result = Magic.ByMagic(stream);
        Assert.IsTrue(result == "audio/vorbis" || result == "audio/ogg", $"Expected Ogg/Vorbis type, got {result}");
    }

    [TestMethod]
    public void ByMagic_WithWav_ReturnsWavType()
    {
        // WAV: RIFF....WAVE
        var wavData = new byte[20];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(wavData, 0);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(wavData, 8);
        using var stream = new MemoryStream(wavData);

        // Base Tika has audio/vnd.wave, Definitions extends to audio/x-wav
        var result = Magic.ByMagic(stream);
        Assert.IsTrue(result == "audio/vnd.wave" || result == "audio/x-wav", $"Expected WAV type, got {result}");
    }

    [TestMethod]
    public void ByMagic_WithUnknownData_ReturnsNull()
    {
        var unknownData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        using var stream = new MemoryStream(unknownData);

        Assert.IsNull(Magic.ByMagic(stream));
    }

    [TestMethod]
    public void ByMagic_WithEmptyStream_ReturnsNull()
    {
        using var stream = new MemoryStream();
        Assert.IsNull(Magic.ByMagic(stream));
    }

    [TestMethod]
    public void ByMagic_WithNullStream_ReturnsNull()
    {
        Assert.IsNull(Magic.ByMagic(null));
    }

    [TestMethod]
    public void AllByMagic_ReturnsAllMatchingTypes()
    {
        var jpegData = new byte[] { 0xFF, 0xD8, 0xFF };
        using var stream = new MemoryStream(jpegData);

        var results = Magic.AllByMagic(stream);
        Assert.IsNotEmpty(results);
        Assert.IsTrue(results.Contains("image/jpeg"));
    }

    [TestMethod]
    public void IsChildOf_WithSameType_ReturnsTrue()
    {
        Assert.IsTrue(Magic.IsChildOf("text/plain", "text/plain"));
        Assert.IsTrue(Magic.IsChildOf("image/jpeg", "image/jpeg"));
    }

    [TestMethod]
    public void IsChildOf_WithDirectParent_ReturnsTrue()
    {
        Assert.IsTrue(Magic.IsChildOf("text/csv", "text/plain"));
        Assert.IsTrue(Magic.IsChildOf("text/x-web-markdown", "text/plain")); // Base Tika type
        Assert.IsTrue(Magic.IsChildOf("application/illustrator", "application/pdf"));
    }

    [TestMethod]
    public void IsChildOf_WithUnrelatedTypes_ReturnsFalse()
    {
        Assert.IsFalse(Magic.IsChildOf("image/png", "text/plain"));
        Assert.IsFalse(Magic.IsChildOf("application/pdf", "image/jpeg"));
    }

    [TestMethod]
    public void IsChildOf_WithNullValues_ReturnsFalse()
    {
        Assert.IsFalse(Magic.IsChildOf(null, "text/plain"));
        Assert.IsFalse(Magic.IsChildOf("text/plain", null));
        Assert.IsFalse(Magic.IsChildOf(null, null));
    }

    [TestMethod]
    public void IsChildOf_ParentNotChild_ReturnsFalse()
    {
        // Parent is not a child of its child
        Assert.IsFalse(Magic.IsChildOf("text/plain", "text/csv"));
        Assert.IsFalse(Magic.IsChildOf("application/pdf", "application/illustrator"));
    }
}
