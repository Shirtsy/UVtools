﻿/*
 *                     GNU AFFERO GENERAL PUBLIC LICENSE
 *                       Version 3, 19 November 2007
 *  Copyright (C) 2007 Free Software Foundation, Inc. <https://fsf.org/>
 *  Everyone is permitted to copy and distribute verbatim copies
 *  of this license document, but changing it is not allowed.
 */

using Emgu.CV;
using Emgu.CV.CvEnum;
using K4os.Compression.LZ4;
using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using K4os.Compression.LZ4.Encoders;
using UVtools.Core.Extensions;
using StreamExtensions = UVtools.Core.Extensions.StreamExtensions;

namespace UVtools.Core.EmguCV;

public abstract class MatCompressor
{
    /// <summary>
    /// Compresses the <see cref="Mat"/> into a byte array.
    /// </summary>
    /// <param name="src"></param>
    /// <param name="argument"></param>
    /// <returns></returns>
    public abstract byte[] Compress(Mat src, object? argument = null);

    /// <summary>
    /// Decompresses the <see cref="Mat"/> from a byte array.
    /// </summary>
    /// <param name="compressedBytes"></param>
    /// <param name="dst"></param>
    /// <param name="argument"></param>
    public abstract void Decompress(byte[] compressedBytes, Mat dst, object? argument = null);

    /// <summary>
    /// Compresses the <see cref="Mat"/> into a byte array.
    /// </summary>
    /// <param name="src"></param>
    /// <param name="argument"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<byte[]> CompressAsync(Mat src, object? argument = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Compress(src, argument), cancellationToken);
    }

    /// <summary>
    /// Decompresses the <see cref="Mat"/> from a byte array.
    /// </summary>
    /// <param name="compressedBytes"></param>
    /// <param name="dst"></param>
    /// <param name="argument"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task DecompressAsync(byte[] compressedBytes, Mat dst, object? argument = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Decompress(compressedBytes, dst, argument), cancellationToken);
    }
}

#region None
public sealed class MatCompressorNone : MatCompressor
{
    /// <summary>
    /// Instance of <see cref="MatCompressorNone"/>.
    /// </summary>
    public static readonly MatCompressorNone Instance = new();

    private MatCompressorNone()
    {
    }

    /// <inheritdoc />
    public override byte[] Compress(Mat src, object? argument = null)
    {
        return src.GetBytes();
    }

    /// <inheritdoc />
    public override void Decompress(byte[] compressedBytes, Mat dst, object? argument = null)
    {
        dst.SetBytes(compressedBytes);
    }

    public override string ToString()
    {
        return "None";
    }
}
#endregion

#region PNG
public sealed class MatCompressorPng : MatCompressor
{
    /// <summary>
    /// Instance of <see cref="MatCompressorPng"/>.
    /// </summary>
    public static readonly MatCompressorPng Instance = new();

    private MatCompressorPng()
    {
    }

    /// <inheritdoc />
    public override byte[] Compress(Mat src, object? argument = null)
    {
        return src.GetPngByes();
    }

    /// <inheritdoc />
    public override void Decompress(byte[] compressedBytes, Mat dst, object? argument = null)
    {
        CvInvoke.Imdecode(compressedBytes, ImreadModes.Unchanged, dst);
    }
}

public sealed class MatCompressorPngGreyScale : MatCompressor
{
    /// <summary>
    /// Instance of <see cref="MatCompressorPng"/>.
    /// </summary>
    public static readonly MatCompressorPngGreyScale Instance = new();

    private MatCompressorPngGreyScale()
    {
    }

    /// <inheritdoc />
    public override byte[] Compress(Mat src, object? argument = null)
    {
        return src.GetPngByes();
    }

    /// <inheritdoc />
    public override void Decompress(byte[] compressedBytes, Mat dst, object? argument = null)
    {
        CvInvoke.Imdecode(compressedBytes, ImreadModes.Grayscale, dst);
    }

    public override string ToString()
    {
        return "PNG";
    }
}
#endregion

#region Deflate
public sealed class MatCompressorDeflate : MatCompressor
{
    /// <summary>
    /// Instance of <see cref="MatCompressorDeflate"/>.
    /// </summary>
    public static readonly MatCompressorDeflate Instance = new();

    private MatCompressorDeflate()
    {
    }

/// <inheritdoc />
public override byte[] Compress(Mat src, object? argument = null)
    {
        UnmanagedMemoryStream srcStream;
        if (src.IsContinuous)
        {
            srcStream = src.GetUnmanagedMemoryStream(FileAccess.Read);
        }
        else
        {
            var bytes = src.GetBytes(); // Need to copy the submatrix to get the full data in a contiguous block
            unsafe
            {
                fixed (byte* p = bytes)
                {
                    srcStream = new UnmanagedMemoryStream(p, bytes.Length);
                }
            }
        }


        using var compressedStream = StreamExtensions.RecyclableMemoryStreamManager.GetStream();
        using (var deflateStream = new DeflateStream(compressedStream, CompressionLevel.Fastest, true))
        {
            srcStream.CopyTo(deflateStream);
        }

        srcStream.Dispose();

        return compressedStream.ToArray();
    }

    /// <inheritdoc />
    public override void Decompress(byte[] compressedBytes, Mat dst, object? argument = null)
    {
        unsafe
        {
            fixed (byte* pBuffer = compressedBytes)
            {
                using var compressedStream = new UnmanagedMemoryStream(pBuffer, compressedBytes.Length);
                using var matStream = dst.GetUnmanagedMemoryStream(FileAccess.Write);
                using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
                deflateStream.CopyTo(matStream);
            }
        }
    }

    public override string ToString()
    {
        return "Deflate";
    }
}
#endregion

#region GZip
public sealed class MatCompressorGZip : MatCompressor
{
    /// <summary>
    /// Instance of <see cref="MatCompressorGZip"/>.
    /// </summary>
    public static readonly MatCompressorGZip Instance = new();

    private MatCompressorGZip()
    {
    }

    /// <inheritdoc />
    public override byte[] Compress(Mat src, object? argument = null)
    {
        UnmanagedMemoryStream srcStream;
        if (src.IsContinuous)
        {
            srcStream = src.GetUnmanagedMemoryStream(FileAccess.Read);
        }
        else
        {
            var bytes = src.GetBytes(); // Need to copy the submatrix to get the full data in a contiguous block
            unsafe
            {
                fixed (byte* p = bytes)
                {
                    srcStream = new UnmanagedMemoryStream(p, bytes.Length);
                }
            }
        }


        using var compressedStream = StreamExtensions.RecyclableMemoryStreamManager.GetStream();
        using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Fastest, true))
        {
            srcStream.CopyTo(gzipStream);
        }

        srcStream.Dispose();

        return compressedStream.ToArray();
    }

    /// <inheritdoc />
    public override void Decompress(byte[] compressedBytes, Mat dst, object? argument = null)
    {
        unsafe
        {
            fixed (byte* pBuffer = compressedBytes)
            {
                using var compressedStream = new UnmanagedMemoryStream(pBuffer, compressedBytes.Length);
                using var matStream = dst.GetUnmanagedMemoryStream(FileAccess.Write);
                using var gZipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                gZipStream.CopyTo(matStream);
            }
        }
    }

    public override string ToString()
    {
        return "GZip";
    }
}
#endregion

#region ZLib
public sealed class MatCompressorZLib : MatCompressor
{
    /// <summary>
    /// Instance of <see cref="MatCompressorZLib"/>.
    /// </summary>
    public static readonly MatCompressorZLib Instance = new();

    private MatCompressorZLib()
    {
    }

    /// <inheritdoc />
    public override byte[] Compress(Mat src, object? argument = null)
    {
        UnmanagedMemoryStream srcStream;
        if (src.IsContinuous)
        {
            srcStream = src.GetUnmanagedMemoryStream(FileAccess.Read);
        }
        else
        {
            var bytes = src.GetBytes(); // Need to copy the submatrix to get the full data in a contiguous block
            unsafe
            {
                fixed (byte* p = bytes)
                {
                    srcStream = new UnmanagedMemoryStream(p, bytes.Length);
                }
            }
        }


        using var compressedStream = StreamExtensions.RecyclableMemoryStreamManager.GetStream();
        using (var zLibStream = new ZLibStream(compressedStream, CompressionLevel.Fastest, true))
        {
            srcStream.CopyTo(zLibStream);
        }

        srcStream.Dispose();

        return compressedStream.ToArray();
    }

    /// <inheritdoc />
    public override void Decompress(byte[] compressedBytes, Mat dst, object? argument = null)
    {
        unsafe
        {
            fixed (byte* pBuffer = compressedBytes)
            {
                using var compressedStream = new UnmanagedMemoryStream(pBuffer, compressedBytes.Length);
                using var matStream = dst.GetUnmanagedMemoryStream(FileAccess.Write);
                using var zLibStream = new ZLibStream(compressedStream, CompressionMode.Decompress);
                zLibStream.CopyTo(matStream);
            }
        }
    }

    public override string ToString()
    {
        return "ZLib";
    }
}
#endregion

#region Brotli
public sealed class MatCompressorBrotli : MatCompressor
{
    /// <summary>
    /// Instance of <see cref="MatCompressorBrotli"/>.
    /// </summary>
    public static readonly MatCompressorBrotli Instance = new();

    private MatCompressorBrotli()
    {
    }

    /// <inheritdoc />
    public override byte[] Compress(Mat src, object? argument = null)
    {
        ReadOnlySpan<byte> srcSpan = src.IsContinuous
            ? src.GetDataByteReadOnlySpan()
            : src.GetBytes(); // Need to copy the submatrix to get the full data in a contiguous block

        var rent = ArrayPool<byte>.Shared.Rent(srcSpan.Length - 1);

        bool result = BrotliEncoder.TryCompress(srcSpan, rent, out var encodedLength, 0, 22);
        if (!result) // Throw an exception if compression failed and let CMat handle it and use uncompressed data
        {
            ArrayPool<byte>.Shared.Return(rent);
            throw new Exception("Failed to compress, buffer is too short?");
        }

        var compressedBytes = GC.AllocateUninitializedArray<byte>(encodedLength);
        Buffer.BlockCopy(rent, 0, compressedBytes, 0, encodedLength);

        ArrayPool<byte>.Shared.Return(rent);

        return compressedBytes;

        /*var compressedData = GC.AllocateUninitializedArray<byte>(srcSpan.Length - 1);
        bool result = BrotliEncoder.TryCompress(srcSpan, compressedData, out var encodedLength, 0, 22);
        if (!result) // Throw an exception if compression failed and let CMat handle it and use uncompressed data
        {
            throw new Exception("Failed to compress, buffer is too short?");
        }
        return compressedData.AsSpan()[..encodedLength].ToArray();*/
    }

    /// <inheritdoc />
    public override void Decompress(byte[] compressedBytes, Mat dst, object? argument = null)
    {
        BrotliDecoder.TryDecompress(new ReadOnlySpan<byte>(compressedBytes), dst.GetDataByteSpan(), out var bytesWritten);
    }

    public override string ToString()
    {
        return "Brotli";
    }
}
#endregion

#region LZ4
public sealed class MatCompressorLz4 : MatCompressor
{
    /// <summary>
    /// Instance of <see cref="MatCompressorLz4"/>.
    /// </summary>
    public static readonly MatCompressorLz4 Instance = new();

    private MatCompressorLz4()
    {
    }

    /// <inheritdoc />
    public override byte[] Compress(Mat src, object? argument = null)
    {
        ReadOnlySpan<byte> srcSpan = src.IsContinuous
            ? src.GetDataByteReadOnlySpan()
            : src.GetBytes(); // Need to copy the submatrix to get the full data in a contiguous block

        // Method 1 - ArrayPool
        /*var rent = ArrayPool<byte>.Shared.Rent(srcSpan.Length - 1);
        try
        {
            var encodedLength = LZ4Codec.Encode(srcSpan, rent);
            var compressedBytes = GC.AllocateUninitializedArray<byte>(encodedLength);
            Buffer.BlockCopy(rent, 0, compressedBytes, 0, encodedLength);
            return compressedBytes;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rent);
        }
        */

        // Method 2 - Direct allocation
        //var compressedData = GC.AllocateUninitializedArray<byte>(srcSpan.Length - 1);
        //var encodedLength = LZ4Codec.Encode(srcSpan, compressedData);
        //return compressedData.AsSpan()[..encodedLength].ToArray();

        // Method 3 - Streams
        //using var stream = StreamExtensions.RecyclableMemoryStreamManager.GetStream(); // caller owns & disposes
        //using var lz4 = LZ4Stream.Encode(stream, LZ4Level.L00_FAST, leaveOpen: true);
        //lz4.Write(srcSpan);
        //var buffer = stream.ToArray();
        //return buffer;

        // Method 4 - Pickle
        return LZ4Pickler.Pickle(srcSpan);

    }

    /// <inheritdoc />
    public override void Decompress(byte[] compressedBytes, Mat dst, object? argument = null)
    {
        //LZ4Codec.Decode(new ReadOnlySpan<byte>(compressedBytes), dst.GetDataByteSpan());
        LZ4Pickler.Unpickle(compressedBytes, dst.GetDataByteSpan());
    }

    public override string ToString()
    {
        return "LZ4";
    }
}
#endregion
