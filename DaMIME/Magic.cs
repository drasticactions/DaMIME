using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DaMIME;

/// <summary>
/// Class for MIME type detection using magic bytes and file extensions.
/// Provides low-level access to MIME detection mechanisms.
/// </summary>
public static class Magic
{
    /// <summary>
    /// Looks up a MIME type by file extension.
    /// </summary>
    /// <param name="extension">The file extension (with or without leading dot, case-insensitive).</param>
    /// <returns>The MIME type string, or null if not found.</returns>
    public static string? ByExtension(string? extension)
    {
        if (string.IsNullOrEmpty(extension))
            return null;

        // Normalize: remove leading dot and convert to lowercase
        var normalized = extension?.TrimStart('.') ?? string.Empty;

        return Tables.Extensions.TryGetValue(normalized, out var mimeType) ? mimeType : null;
    }

    /// <summary>
    /// Looks up a MIME type by file path, extracting the extension.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The MIME type string, or null if not found.</returns>
    public static string? ByPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var extension = Path.GetExtension(path);
        return ByExtension(extension);
    }

    /// <summary>
    /// Looks up a MIME type by analyzing magic bytes in the stream.
    /// Returns the first matching type.
    /// </summary>
    /// <param name="stream">The stream to analyze. Must be seekable.</param>
    /// <returns>The MIME type string, or null if no match found.</returns>
    public static string? ByMagic(Stream? stream)
    {
        if (stream == null || !stream.CanSeek)
            return null;

        var originalPosition = stream.Position;
        try
        {
            foreach (var (type, patterns) in Tables.MagicPatterns)
            {
                stream.Position = originalPosition;
                if (MagicMatchPatterns(stream, patterns))
                    return type;
            }

            return null;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    /// <summary>
    /// Looks up all MIME types that match the magic bytes in the stream.
    /// </summary>
    /// <param name="stream">The stream to analyze. Must be seekable.</param>
    /// <returns>An array of matching MIME type strings.</returns>
    public static string[] AllByMagic(Stream? stream)
    {
        if (stream == null || !stream.CanSeek)
            return Array.Empty<string>();

        var originalPosition = stream.Position;
        var matches = new List<string>();

        try
        {
            foreach (var (type, patterns) in Tables.MagicPatterns)
            {
                stream.Position = originalPosition;
                if (MagicMatchPatterns(stream, patterns))
                    matches.Add(type);
            }

            return matches.ToArray();
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    /// <summary>
    /// Checks if a child MIME type is a descendant of a parent type in the hierarchy.
    /// </summary>
    /// <param name="child">The potential child type.</param>
    /// <param name="parent">The potential parent type.</param>
    /// <returns>True if child is the same as or descends from parent.</returns>
    public static bool IsChildOf(string? child, string? parent)
    {
        if (string.IsNullOrEmpty(child) || string.IsNullOrEmpty(parent))
            return false;

        // A type is considered a child of itself
        if (string.Equals(child, parent, StringComparison.OrdinalIgnoreCase))
            return true;

        if (child == null)
            return false;

        // Check if child has parents in the hierarchy
        if (!Tables.TypeParents.TryGetValue(child, out var parents))
            return false;

        // Recursively check if any parent matches
        return parents.Any(p => IsChildOf(p, parent));
    }

    /// <summary>
    /// Adds a custom magic pattern for a MIME type.
    /// Pattern is prepended to give custom types priority over built-in types.
    /// </summary>
    /// <param name="type">The MIME type.</param>
    /// <param name="patterns">The magic byte patterns.</param>
    /// <remarks>
    /// This method should be called during application startup for thread safety.
    /// Custom patterns are checked before built-in patterns.
    /// </remarks>
    internal static void AddMagicPattern(string type, MagicPattern[] patterns)
    {
        if (string.IsNullOrEmpty(type) || patterns == null || patterns.Length == 0)
            return;

        // Prepend to the beginning of the magic patterns array (like Ruby's unshift)
        var existingPatterns = Tables.MagicPatterns.ToList();
        existingPatterns.Insert(0, (type, patterns));

        // Update the array reference
        // NOTE: This is not thread-safe during detection. Call during initialization only.
        System.Array.Resize(ref Tables.MagicPatternsArray, existingPatterns.Count);
        for (int i = 0; i < existingPatterns.Count; i++)
        {
            Tables.MagicPatternsArray[i] = existingPatterns[i];
        }
    }

    /// <summary>
    /// Resets the magic patterns to their initial state.
    /// This is primarily useful for testing to ensure a clean slate between test runs.
    /// </summary>
    /// <remarks>
    /// This method should be called during application startup for thread safety.
    /// </remarks>
    internal static void ResetMagicPatterns()
    {
        Tables.ResetMagicPatterns();
    }

    /// <summary>
    /// Removes a MIME type and all its associated extensions, magic patterns, and parent relationships.
    /// </summary>
    /// <param name="type">The MIME type to remove.</param>
    /// <remarks>
    /// This method should be called during application startup for thread safety.
    /// </remarks>
    public static void Remove(string type)
    {
        if (string.IsNullOrEmpty(type))
            return;

        // Remove from EXTENSIONS (any extension pointing to this type)
        var extensionsToRemove = Tables.Extensions
            .Where(kvp => kvp.Value.Equals(type, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var ext in extensionsToRemove)
        {
            Tables.Extensions.Remove(ext);
        }

        // Remove from TYPE_EXTS
        Tables.TypeExtensions.Remove(type);

        // Remove from TYPE_PARENTS
        Tables.TypeParents.Remove(type);

        // Remove from MAGIC patterns
        var remainingPatterns = Tables.MagicPatterns
            .Where(p => !string.Equals(p.Type, type, StringComparison.OrdinalIgnoreCase))
            .ToList();

        System.Array.Resize(ref Tables.MagicPatternsArray, remainingPatterns.Count);
        for (int i = 0; i < remainingPatterns.Count; i++)
        {
            Tables.MagicPatternsArray[i] = remainingPatterns[i];
        }
    }

    /// <summary>
    /// Matches an array of magic patterns against a stream.
    /// Returns true if ANY pattern in the array matches (OR logic).
    /// </summary>
    private static bool MagicMatchPatterns(Stream stream, MagicPattern[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (MagicMatchSinglePattern(stream, pattern))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Matches a single magic pattern against a stream.
    /// If the pattern has children, they must ALL match (AND logic).
    /// </summary>
    private static bool MagicMatchSinglePattern(Stream stream, MagicPattern pattern)
    {
        bool matched;

        if (pattern.IsRangeOffset)
        {
            // Range offset: search for value within the range
            matched = MagicMatchRange(stream, pattern.OffsetStart, pattern.OffsetEnd, pattern.Value);
        }
        else
        {
            // Fixed offset: match value at exact position
            matched = MagicMatchFixed(stream, pattern.OffsetStart, pattern.Value);
        }

        if (!matched)
            return false;

        // If parent matched and there are children, check them all
        if (pattern.Children != null && pattern.Children.Length > 0)
        {
            // Reset stream position for children
            stream.Position = 0;

            // All children must match (AND logic)
            foreach (var child in pattern.Children)
            {
                if (!MagicMatchPatterns(stream, new[] { child }))
                    return false;

                // Reset for next child
                stream.Position = 0;
            }
        }

        return true;
    }

    /// <summary>
    /// Matches a fixed offset pattern.
    /// </summary>
    private static bool MagicMatchFixed(Stream stream, int offset, byte[] value)
    {
        if (value == null || value.Length == 0)
            return false;

        try
        {
            // Seek to the offset
            if (stream.Length < offset + value.Length)
                return false;

            stream.Position = offset;

#if NET8_0_OR_GREATER
            // For small patterns, use stackalloc; otherwise use ArrayPool
            if (value.Length <= 256)
            {
                Span<byte> buffer = stackalloc byte[value.Length];
                stream.ReadExactly(buffer);
                return buffer.SequenceEqual(value);
            }
            else
            {
                var rentedBuffer = ArrayPool<byte>.Shared.Rent(value.Length);
                try
                {
                    var buffer = rentedBuffer.AsSpan(0, value.Length);
                    stream.ReadExactly(buffer);
                    return buffer.SequenceEqual(value);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                }
            }
#else
            // netstandard2.0 fallback - use ArrayPool with manual read checking
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(value.Length);
            try
            {
                var bytesRead = stream.Read(rentedBuffer, 0, value.Length);
                if (bytesRead < value.Length)
                    return false;

                return rentedBuffer.AsSpan(0, value.Length).SequenceEqual(value);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
#endif
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Matches a range offset pattern by searching for the value within the range.
    /// </summary>
    private static bool MagicMatchRange(Stream stream, int offsetStart, int offsetEnd, byte[] value)
    {
        if (value == null || value.Length == 0)
            return false;

        try
        {
            var searchLength = offsetEnd - offsetStart + value.Length;
            if (stream.Length < offsetStart + searchLength)
                searchLength = (int)(stream.Length - offsetStart);

            if (searchLength <= 0)
                return false;

            stream.Position = offsetStart;

            // Rent a buffer large enough for the search range
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(searchLength);
            try
            {
#if NET8_0_OR_GREATER
                var buffer = rentedBuffer.AsSpan(0, searchLength);
                var bytesRead = stream.Read(buffer);
                if (bytesRead < value.Length)
                    return false;

                return buffer[..bytesRead].IndexOf(value) >= 0;
#else
                // netstandard2.0 fallback
                var bytesRead = stream.Read(rentedBuffer, 0, searchLength);
                if (bytesRead < value.Length)
                    return false;

                return rentedBuffer.AsSpan(0, bytesRead).IndexOf(value) >= 0;
#endif
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
        catch
        {
            return false;
        }
    }
}
