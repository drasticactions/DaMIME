using System;
using System.IO;
using System.Linq;

namespace DaMIME;

/// <summary>
/// Provides MIME type detection for files based on content analysis, file extensions, and declared types.
/// </summary>
public static class MimeType
{
    /// <summary>
    /// The default MIME type used when no specific type can be determined.
    /// </summary>
    public const string Binary = "application/octet-stream";

    /// <summary>
    /// Static constructor to initialize custom type definitions.
    /// </summary>
    static MimeType()
    {
    }

    /// <summary>
    /// Determines the most appropriate MIME type for the given input.
    /// </summary>
    /// <param name="stream">Optional stream containing file data. Must be seekable if provided.</param>
    /// <param name="name">Optional filename.</param>
    /// <param name="extension">Optional file extension.</param>
    /// <param name="declaredType">Optional declared MIME type (e.g., from Content-Type header).</param>
    /// <returns>
    /// The detected MIME type string. Returns "application/octet-stream" if no type can be determined.
    /// </returns>
    /// <remarks>
    /// Detection priority:
    /// 1. Magic bytes from stream content
    /// 2. Declared type (unless it's application/octet-stream)
    /// 3. Extension from name or extension parameter
    /// 4. Fallback to application/octet-stream
    ///
    /// The most specific type is selected when multiple types match (e.g., text/csv is preferred over text/plain).
    /// </remarks>
    public static string For(
        Stream? stream = null,
        string? name = null,
        string? extension = null,
        string? declaredType = null)
    {
        var magicType = ForData(stream);
        var parsedDeclaredType = ForDeclaredType(declaredType);
        var extensionType = ForName(name) ?? ForExtension(extension);
        return MostSpecificType(magicType, parsedDeclaredType, extensionType, Binary) ?? Binary;
    }

    /// <summary>
    /// Parses a Content-Type header or media type string, extracting just the MIME type.
    /// </summary>
    /// <param name="contentType">The Content-Type header value (e.g., "text/html; charset=utf-8").</param>
    /// <returns>The MIME type portion (e.g., "text/html"), or null if invalid.</returns>
    /// <remarks>
    /// This method strips charset, boundary, and other parameters from the Content-Type header.
    /// </remarks>
    public static string? ParseMediaType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return null;

        if (contentType is null)
            return null;

        // Split on semicolon, comma, or whitespace and take the first part
        var parts = contentType.ToLowerInvariant().Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return null;

        var result = parts[0];

        // Verify it's a valid MIME type (contains a slash)
        return result.Contains('/') ? result : null;
    }

    /// <summary>
    /// Selects the most specific MIME type from multiple candidates.
    /// </summary>
    /// <param name="candidates">The candidate MIME types.</param>
    /// <returns>The most specific (deepest child) type, or null if no candidates.</returns>
    /// <remarks>
    /// When multiple types match, this method uses the type hierarchy to select the most specific.
    /// For example, if both "text/plain" and "text/csv" match, "text/csv" is selected because it's
    /// a more specific child type of "text/plain".
    /// </remarks>
    public static string? MostSpecificType(params string?[] candidates)
    {
        var validCandidates = candidates
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (validCandidates.Length == 0)
            return null;

        if (validCandidates.Length == 1)
            return validCandidates[0];

        // Reduce to the most specific type using hierarchy
        return validCandidates.Aggregate((current, candidate) =>
            Magic.IsChildOf(candidate, current) ? candidate : current);
    }

    /// <summary>
    /// Extends the MIME type system with a custom type definition.
    /// </summary>
    /// <param name="type">The MIME type string (e.g., "application/x-custom").</param>
    /// <param name="extensions">Optional file extensions for this type.</param>
    /// <param name="parents">Optional parent types in the hierarchy.</param>
    /// <param name="magic">Optional magic byte patterns for detection.</param>
    /// <remarks>
    /// This method allows adding custom MIME types at runtime. Extensions should be done
    /// at application startup before any detection calls for thread safety.
    /// </remarks>
    public static void Extend(
        string type,
        string[]? extensions = null,
        string[]? parents = null,
        MagicPattern[]? magic = null)
    {
        // Add extensions to EXTENSIONS dictionary
        if (extensions != null && extensions.Length > 0)
        {
            // Update TYPE_EXTS
            Tables.TypeExtensions[type] = extensions;

            // Update EXTENSIONS
            foreach (var ext in extensions)
            {
                var normalized = ext.TrimStart('.').ToLowerInvariant();
                Tables.Extensions[normalized] = type;
            }
        }

        // Add parent relationships
        if (parents != null && parents.Length > 0)
        {
            Tables.TypeParents[type] = parents;
        }

        // Add magic patterns (prepend to give custom types priority)
        if (magic != null && magic.Length > 0)
        {
            Magic.AddMagicPattern(type, magic);
        }
    }

    /// <summary>
    /// Detects MIME type from stream content using magic bytes.
    /// </summary>
    private static string? ForData(Stream? stream)
    {
        if (stream == null || !stream.CanSeek)
            return null;

        return Magic.ByMagic(stream);
    }

    /// <summary>
    /// Detects MIME type from filename.
    /// </summary>
    private static string? ForName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return Magic.ByPath(name);
    }

    /// <summary>
    /// Detects MIME type from file extension.
    /// </summary>
    private static string? ForExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;

        return Magic.ByExtension(extension);
    }

    /// <summary>
    /// Processes declared type, treating application/octet-stream as undeclared.
    /// </summary>
    private static string? ForDeclaredType(string? declaredType)
    {
        var parsedType = ParseMediaType(declaredType);

        // application/octet-stream is treated as an undeclared/missing type,
        // allowing the type to be inferred from other sources
        return string.Equals(parsedType, Binary, StringComparison.OrdinalIgnoreCase)
            ? null
            : parsedType;
    }
}
