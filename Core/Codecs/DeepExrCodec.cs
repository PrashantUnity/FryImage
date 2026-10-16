using System;
using System.IO;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

/// <summary>
/// Deep OpenEXR codec and sample-count table admission validator (#5632, #4958).
/// Enforces max_alloc budget checks on deep tile count tables and scratch copies.
/// </summary>
public static class DeepExrCodec
{
    public const ulong DefaultMaxAlloc = 64 * 1024 * 1024; // 64 MiB

    /// <summary>
    /// Validates whether a deep EXR tile sample-count table fits within the memory allocation limits
    /// before allocating decompression buffers and scratch space.
    /// </summary>
    public static void ValidateCountTableAllocation(
        int packedTableBytes,
        int blockPixelCount,
        int tileW,
        int tileH,
        ulong maxAlloc = DefaultMaxAlloc)
    {
        if (blockPixelCount < 0 || tileW <= 0 || tileH <= 0)
        {
            throw new ArgumentException("Invalid tile dimensions or pixel count.");
        }

        ulong expectedTable = (ulong)blockPixelCount * 4UL;

        // A compressed clipped table needs its scratch copy (2 buffers), whereas raw uncompressed needs 1.
        ulong buffers = (ulong)packedTableBytes == expectedTable ? 1UL : 2UL;
        ulong clippedNeed = expectedTable * buffers;

        if (clippedNeed > maxAlloc)
        {
            throw new InvalidOperationException(
                $"The sample-count table needs {clippedNeed} bytes including decompression scratch, exceeding max_alloc {maxAlloc}");
        }

        ulong fullTable = (ulong)tileW * (ulong)tileH * 4UL;
        if (fullTable > expectedTable)
        {
            // Edge tile table stored at full tile size: tried only when max_alloc has room for both table and scratch copy
            ulong fullNeed = fullTable * 2UL;
            if (fullNeed > maxAlloc)
            {
                throw new InvalidOperationException(
                    $"The sample-count table of a {tileW}x{tileH} deep tile does not decode at the clipped size, and at the full tile size it needs {fullNeed} bytes with its scratch copy, exceeding max_alloc {maxAlloc}");
            }
        }
    }
}
