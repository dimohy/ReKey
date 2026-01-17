using System.Drawing;
using System.Drawing.Imaging;
using Svg;

internal static partial class Program
{
    public static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: IconBuilder <input.svg> <output.ico> [size]");
            return;
        }

        var inputPath = args[0];
        var outputPath = args[1];
        var size = args.Length >= 3 && int.TryParse(args[2], out var parsed) ? parsed : 256;

        var svgDoc = SvgDocument.Open<SvgDocument>(inputPath);
        svgDoc.Width = size;
        svgDoc.Height = size;

        using var bitmap = svgDoc.Draw(size, size);
        using var pngStream = new MemoryStream();
        bitmap.Save(pngStream, ImageFormat.Png);

        using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        WritePngIcon(stream, pngStream.ToArray(), size);

        Console.WriteLine($"Icon written: {outputPath}");
    }

    private static void WritePngIcon(Stream output, byte[] pngData, int size)
    {
        using var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);

        // ICONDIR
        writer.Write((ushort)0); // Reserved
        writer.Write((ushort)1); // Type: 1 = icon
        writer.Write((ushort)1); // Count

        // ICONDIRENTRY
        writer.Write((byte)(size >= 256 ? 0 : size)); // Width
        writer.Write((byte)(size >= 256 ? 0 : size)); // Height
        writer.Write((byte)0); // Color count
        writer.Write((byte)0); // Reserved
        writer.Write((ushort)1); // Planes
        writer.Write((ushort)32); // Bit count
        writer.Write(pngData.Length); // Bytes in resource
        writer.Write(6 + 16); // Image offset

        // PNG image data
        writer.Write(pngData);
    }
}
