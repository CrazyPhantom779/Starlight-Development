// This file is part of the voxel 3D view. The engine files in this folder have no game dependencies and are
// kept identical to the standalone Voxel3D library (which has its own tests); only the namespace differs.
// They only use APIs that the client sandbox allows.
namespace Content.Client._Starlight.Voxel3D.Engine;

/// <summary>A plain RGBA raster. Origin is the top-left corner, y grows downward (normal image convention).</summary>
public sealed class RgbaImage
{
    public int Width { get; }
    public int Height { get; }

    /// <summary>Row-major, top row first. Length is Width * Height.</summary>
    public Rgba[] Pixels { get; }

    public RgbaImage(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        Pixels = new Rgba[checked(width * height)];
    }

    public RgbaImage(int width, int height, Rgba[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (pixels is null) throw new ArgumentException("Pixels are required.", nameof(pixels));
        if (pixels.Length != checked(width * height))
            throw new ArgumentException("Pixel array length must equal width * height.", nameof(pixels));
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public Rgba this[int x, int y]
    {
        get => Pixels[y * Width + x];
        set => Pixels[y * Width + x] = value;
    }

    /// <summary>Builds an image from tightly packed R,G,B,A bytes (e.g. straight from an image library).</summary>
    public static RgbaImage FromRgbaBytes(int width, int height, ReadOnlySpan<byte> rgba)
    {
        if (rgba.Length != checked(width * height * 4))
            throw new ArgumentException("Expected width * height * 4 bytes.", nameof(rgba));
        var img = new RgbaImage(width, height);
        for (int i = 0; i < img.Pixels.Length; i++)
            img.Pixels[i] = new Rgba(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);
        return img;
    }

    /// <summary>Returns a copy of the sub-rectangle (used to cut one frame/direction out of an RSI sheet).</summary>
    public RgbaImage Crop(int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > Width || y + height > Height)
            throw new ArgumentOutOfRangeException(nameof(x), "Crop rectangle is outside the image.");
        var result = new RgbaImage(width, height);
        for (int row = 0; row < height; row++)
            Array.Copy(Pixels, (y + row) * Width + x, result.Pixels, row * width, width);
        return result;
    }
}
