namespace DaMIME;

/// <summary>
/// Represents a magic byte pattern used for content-based MIME type detection.
/// </summary>
public readonly struct MagicPattern
{
    /// <summary>
    /// Gets the starting offset for pattern matching. For fixed offset, this is the exact position.
    /// For range offset, this is the beginning of the search range.
    /// </summary>
    public int OffsetStart { get; init; }

    /// <summary>
    /// Gets the ending offset for range-based pattern matching.
    /// If -1, indicates a fixed offset at OffsetStart.
    /// If >= 0, indicates a range search from OffsetStart to OffsetEnd.
    /// </summary>
    public int OffsetEnd { get; init; }

    /// <summary>
    /// Gets the binary value to match against the file content.
    /// </summary>
    public byte[] Value { get; init; }

    /// <summary>
    /// Gets the nested child patterns that must match if this parent pattern matches.
    /// Null if there are no child patterns.
    /// </summary>
    public MagicPattern[]? Children { get; init; }

    /// <summary>
    /// Initializes a new instance of the MagicPattern struct with a fixed offset.
    /// </summary>
    /// <param name="offset">The fixed offset where the pattern should match.</param>
    /// <param name="value">The binary value to match.</param>
    /// <param name="children">Optional nested child patterns.</param>
    public MagicPattern(int offset, byte[] value, MagicPattern[]? children = null)
    {
        OffsetStart = offset;
        OffsetEnd = -1;
        Value = value;
        Children = children;
    }

    /// <summary>
    /// Initializes a new instance of the MagicPattern struct with a range offset.
    /// </summary>
    /// <param name="offsetStart">The start of the search range.</param>
    /// <param name="offsetEnd">The end of the search range.</param>
    /// <param name="value">The binary value to match.</param>
    /// <param name="children">Optional nested child patterns.</param>
    public MagicPattern(int offsetStart, int offsetEnd, byte[] value, MagicPattern[]? children = null)
    {
        OffsetStart = offsetStart;
        OffsetEnd = offsetEnd;
        Value = value;
        Children = children;
    }

    /// <summary>
    /// Returns true if this pattern uses a range offset rather than a fixed offset.
    /// </summary>
    public bool IsRangeOffset => OffsetEnd >= 0;
}
