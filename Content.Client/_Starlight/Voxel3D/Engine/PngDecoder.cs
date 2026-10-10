// This file is part of the voxel 3D view. The engine files in this folder have no game dependencies and are
// kept identical to the standalone Voxel3D library (which has its own tests); only the namespace differs.
// They only use APIs that the client sandbox allows.
using System.IO;
using System.IO.Compression;

namespace Content.Client._Starlight.Voxel3D.Engine;

/// <summary>
/// Reads PNG files using only .NET pieces that the game's sandbox allows (streams and Deflate; no ZLibStream, no files).
/// Handles every non-interlaced color type and bit depth, including palettes with transparency. This is how sprite pixels
/// are read, because the game's image classes can write pixels but not read them.
/// </summary>
public static class PngDecoder
{
    public static RgbaImage Decode(Stream stream)
    {
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        var data = ms.ToArray();
        return Decode(data);
    }

    public static RgbaImage Decode(byte[] data)
    {
        if (data.Length < 8 || data[0] != 137 || data[1] != 80 || data[2] != 78 || data[3] != 71 || data[4] != 13 || data[5] != 10 || data[6] != 26 || data[7] != 10)
            throw new InvalidOperationException("Not a PNG file.");

        int width = 0, height = 0, depth = 0, colorType = 0, interlace = 0;
        var sawHeader = false;
        byte[]? plte = null, trns = null;
        var idat = new MemoryStream();

        var p = 8;
        while (p + 12 <= data.Length)
        {
            var len = ReadInt(data, p);
            var start = p + 8;
            if (len < 0 || (long)start + len + 4 > data.Length) throw new InvalidOperationException("Truncated PNG chunk.");

            if (Is(data, p + 4, 'I', 'H', 'D', 'R'))
            {
                width = ReadInt(data, start);
                height = ReadInt(data, start + 4);
                depth = data[start + 8];
                colorType = data[start + 9];
                interlace = data[start + 12];
                sawHeader = true;
            }
            else if (Is(data, p + 4, 'P', 'L', 'T', 'E'))
            {
                plte = Slice(data, start, len);
            }
            else if (Is(data, p + 4, 't', 'R', 'N', 'S'))
            {
                trns = Slice(data, start, len);
            }
            else if (Is(data, p + 4, 'I', 'D', 'A', 'T'))
            {
                idat.Write(data, start, len);
            }

            var end = Is(data, p + 4, 'I', 'E', 'N', 'D');
            p = start + len + 4;
            if (end) break;
        }

        if (!sawHeader || width <= 0 || height <= 0) throw new InvalidOperationException("PNG has no valid header.");
        if (interlace != 0) throw new NotSupportedException("Interlaced PNGs are not supported.");

        var channels = colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new InvalidOperationException("Bad PNG color type.") };
        var depthOk = colorType switch
        {
            0 => depth is 1 or 2 or 4 or 8 or 16,
            3 => depth is 1 or 2 or 4 or 8,
            _ => depth is 8 or 16,
        };
        if (!depthOk) throw new InvalidOperationException("Bad PNG bit depth.");

        var bitsPerPixel = channels * depth;
        var bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
        var rowBytes = ((width * bitsPerPixel) + 7) / 8;

        // The image data is a zlib stream: a 2 byte header, deflate data, a 4 byte checksum. Deflate is allowed, zlib is not,
        // so skip the header and let the deflate reader stop where the deflate data ends.
        var z = idat.ToArray();
        if (z.Length < 6 || (z[0] & 0x0F) != 8 || (z[1] & 0x20) != 0) throw new InvalidOperationException("Unsupported PNG compression.");

        var raw = new byte[(rowBytes + 1) * height];
        using (var inflate = new DeflateStream(new MemoryStream(z, 2, z.Length - 2), CompressionMode.Decompress))
        {
            var got = 0;
            while (got < raw.Length)
            {
                var n = inflate.Read(raw, got, raw.Length - got);
                if (n <= 0) throw new InvalidOperationException("PNG image data is truncated.");
                got += n;
            }
        }

        var img = Unfilter(raw, rowBytes, height, bytesPerPixel);
        return ToRgba(img, width, height, rowBytes, depth, colorType, plte, trns);
    }

    private static bool Is(byte[] d, int at, char a, char b, char c, char e) =>
        d[at] == a && d[at + 1] == b && d[at + 2] == c && d[at + 3] == e;

    private static int ReadInt(byte[] d, int at) => (d[at] << 24) | (d[at + 1] << 16) | (d[at + 2] << 8) | d[at + 3];

    private static byte[] Slice(byte[] d, int start, int len)
    {
        var r = new byte[len];
        Array.Copy(d, start, r, 0, len);
        return r;
    }

    private static byte[] Unfilter(byte[] raw, int rowBytes, int height, int bpp)
    {
        var img = new byte[rowBytes * height];
        for (var y = 0; y < height; y++)
        {
            int filter = raw[y * (rowBytes + 1)];
            var src = (y * (rowBytes + 1)) + 1;
            var dst = y * rowBytes;
            for (var i = 0; i < rowBytes; i++)
            {
                int x = raw[src + i];
                var a = i >= bpp ? img[dst + i - bpp] : 0;
                var b = y > 0 ? img[dst - rowBytes + i] : 0;
                var c = (y > 0 && i >= bpp) ? img[dst - rowBytes + i - bpp] : 0;
                var value = filter switch
                {
                    0 => x,
                    1 => x + a,
                    2 => x + b,
                    3 => x + ((a + b) >> 1),
                    4 => x + Paeth(a, b, c),
                    _ => throw new InvalidOperationException("Bad PNG filter type."),
                };
                img[dst + i] = (byte)value;
            }
        }
        return img;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        return pb <= pc ? b : c;
    }

    private static RgbaImage ToRgba(byte[] img, int width, int height, int rowBytes, int depth, int colorType, byte[]? plte, byte[]? trns)
    {
        int Sample(int y, int index)
        {
            var o = y * rowBytes;
            if (depth == 8) return img[o + index];
            if (depth == 16) return (img[o + (2 * index)] << 8) | img[o + (2 * index) + 1];
            var bit = index * depth;
            var shift = 8 - depth - (bit % 8);
            return (img[o + (bit / 8)] >> shift) & ((1 << depth) - 1);
        }

        int To8(int v) => depth switch { 16 => v >> 8, 8 => v, _ => v * 255 / ((1 << depth) - 1) };

        int trnsGray = -1, trnsR = -1, trnsG = -1, trnsB = -1;
        if (trns is not null)
        {
            if (colorType == 0 && trns.Length >= 2) trnsGray = (trns[0] << 8) | trns[1];
            if (colorType == 2 && trns.Length >= 6)
            {
                trnsR = (trns[0] << 8) | trns[1];
                trnsG = (trns[2] << 8) | trns[3];
                trnsB = (trns[4] << 8) | trns[5];
            }
        }

        var result = new RgbaImage(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                Rgba px;
                switch (colorType)
                {
                    case 0:
                    {
                        var v = Sample(y, x);
                        var g = (byte)To8(v);
                        px = new Rgba(g, g, g, (byte)(v == trnsGray ? 0 : 255));
                        break;
                    }
                    case 2:
                    {
                        int r = Sample(y, x * 3), g = Sample(y, (x * 3) + 1), b = Sample(y, (x * 3) + 2);
                        var transparent = r == trnsR && g == trnsG && b == trnsB;
                        px = new Rgba((byte)To8(r), (byte)To8(g), (byte)To8(b), (byte)(transparent ? 0 : 255));
                        break;
                    }
                    case 3:
                    {
                        var idx = Sample(y, x);
                        if (plte is null || (idx * 3) + 2 >= plte.Length) throw new InvalidOperationException("PNG palette index out of range.");
                        var alpha = trns is not null && idx < trns.Length ? trns[idx] : (byte)255;
                        px = new Rgba(plte[idx * 3], plte[(idx * 3) + 1], plte[(idx * 3) + 2], alpha);
                        break;
                    }
                    case 4:
                    {
                        var g = (byte)To8(Sample(y, x * 2));
                        px = new Rgba(g, g, g, (byte)To8(Sample(y, (x * 2) + 1)));
                        break;
                    }
                    default:
                        px = new Rgba((byte)To8(Sample(y, x * 4)), (byte)To8(Sample(y, (x * 4) + 1)),
                            (byte)To8(Sample(y, (x * 4) + 2)), (byte)To8(Sample(y, (x * 4) + 3)));
                        break;
                }
                result.Pixels[(y * width) + x] = px;
            }
        }
        return result;
    }
}
