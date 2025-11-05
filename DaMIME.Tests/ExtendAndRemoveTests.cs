using System.IO;
using System.Linq;
using System.Text;

namespace DaMIME.Tests;

[TestClass]
public class ExtendAndRemoveTests
{
    [TestCleanup]
    public void Cleanup()
    {
        // Reset state AFTER each test to prevent interference with subsequent tests
        // This ensures the next test starts with a clean slate

        // First, clear any custom extensions and parents that were added by this test
        var testTypes = new[]
        {
            "application/x-custom-test",
            "application/x-custom-child",
            "application/x-custom-format",
            "application/x-super-custom",
            "application/x-temp",
            "application/x-custom-jpg",
            "application/x-nested-format"
        };

        foreach (var type in testTypes)
        {
            Tables.TypeExtensions.Remove(type);
            Tables.TypeParents.Remove(type);

            var extensionsToRemove = Tables.Extensions
                .Where(kvp => string.Equals(kvp.Value, type, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var ext in extensionsToRemove)
            {
                Tables.Extensions.Remove(ext);
            }
        }

        // Finally, reset magic patterns to initial state
        Magic.ResetMagicPatterns();
    }
    [TestMethod]
    public void Extend_WithExtensions_AddsToExtensionDictionary()
    {
        // Add a custom type with extension
        MimeType.Extend("application/x-custom-test", extensions: new[] { "customtest" });

        // Verify it's added
        Assert.AreEqual("application/x-custom-test", MimeType.For(extension: "customtest"));
    }

    [TestMethod]
    public void Extend_WithParents_AddsToHierarchy()
    {
        // Add a custom type with parent
        MimeType.Extend("application/x-custom-child", parents: new[] { "application/json" });

        // Verify hierarchy
        Assert.IsTrue(Magic.IsChildOf("application/x-custom-child", "application/json"));
    }

    [TestMethod]
    public void Extend_WithMagicPatterns_AddsToMagicDetection()
    {
        // Add a custom type with magic bytes
        var customMagic = new[]
        {
            new MagicPattern(0, Encoding.ASCII.GetBytes("CUSTOMFORMAT"))
        };

        MimeType.Extend("application/x-custom-format",
            extensions: new[] { "customfmt" },
            magic: customMagic);

        // Create stream with custom magic bytes
        var data = Encoding.ASCII.GetBytes("CUSTOMFORMAT data here");
        using var stream = new MemoryStream(data);

        // Verify magic detection works
        var detected = MimeType.For(stream);
        Assert.AreEqual("application/x-custom-format", detected);

        // Verify extension also works
        Assert.AreEqual("application/x-custom-format", MimeType.For(extension: "customfmt"));
    }

    [TestMethod]
    public void Extend_WithAllParameters_WorksTogether()
    {
        // Add a comprehensive custom type
        MimeType.Extend("application/x-super-custom",
            extensions: new[] { "scustom", "sc" },
            parents: new[] { "application/octet-stream" },
            magic: new[]
            {
                new MagicPattern(0, Encoding.ASCII.GetBytes("SUPER"))
            });

        // Test magic detection
        var data = Encoding.ASCII.GetBytes("SUPER custom data");
        using (var stream = new MemoryStream(data))
        {
            Assert.AreEqual("application/x-super-custom", MimeType.For(stream));
        }

        // Test extensions
        Assert.AreEqual("application/x-super-custom", MimeType.For(extension: "scustom"));
        Assert.AreEqual("application/x-super-custom", MimeType.For(extension: "sc"));

        // Test hierarchy
        Assert.IsTrue(Magic.IsChildOf("application/x-super-custom", "application/octet-stream"));
    }

    [TestMethod]
    public void Remove_RemovesAllAssociations()
    {
        // Add a custom type
        MimeType.Extend("application/x-temp",
            extensions: new[] { "tmp123" },
            parents: new[] { "application/octet-stream" },
            magic: new[]
            {
                new MagicPattern(0, Encoding.ASCII.GetBytes("TEMP"))
            });

        // Verify it exists
        Assert.AreEqual("application/x-temp", MimeType.For(extension: "tmp123"));

        // Remove it
        Magic.Remove("application/x-temp");

        // Verify it's gone from extension lookup
        Assert.IsNull(Magic.ByExtension("tmp123"));

        // Verify it's gone from hierarchy
        Assert.IsFalse(Magic.IsChildOf("application/x-temp", "application/octet-stream"));

        // Verify it's gone from magic detection
        var data = Encoding.ASCII.GetBytes("TEMP data");
        using var stream = new MemoryStream(data);
        var detected = Magic.ByMagic(stream);
        Assert.AreNotEqual("application/x-temp", detected);
    }

    [TestMethod]
    public void Extend_CustomTypeTakesPriority_OverBuiltIn()
    {
        // Add a custom type that matches before built-in types
        MimeType.Extend("application/x-custom-jpg",
            extensions: new[] { "customjpg" },
            magic: new[]
            {
                new MagicPattern(0, new byte[] { 0xFF, 0xD8, 0xFF }) // JPEG magic bytes
            });

        // JPEG magic bytes should match custom type first (prepended)
        var jpegData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        using var stream = new MemoryStream(jpegData);

        var detected = MimeType.For(stream);
        // Custom types are prepended, so they should be checked first
        Assert.AreEqual("application/x-custom-jpg", detected);
    }

    [TestMethod]
    public void Extend_WithNestedMagicPatterns_WorksCorrectly()
    {
        // Add a type with nested magic pattern (parent-child structure)
        MimeType.Extend("application/x-nested-format",
            magic: new[]
            {
                new MagicPattern(0, Encoding.ASCII.GetBytes("OUTER"), new[]
                {
                    new MagicPattern(10, Encoding.ASCII.GetBytes("INNER"))
                })
            });

        // Create data with both parent and child patterns
        var data = new byte[20];
        Encoding.ASCII.GetBytes("OUTER").CopyTo(data, 0);
        Encoding.ASCII.GetBytes("INNER").CopyTo(data, 10);

        using var stream = new MemoryStream(data);
        var detected = MimeType.For(stream);

        Assert.AreEqual("application/x-nested-format", detected);
    }
}
