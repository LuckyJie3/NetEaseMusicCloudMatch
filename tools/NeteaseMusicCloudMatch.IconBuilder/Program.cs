using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: IconBuilder <source.png> <output.ico>");
    return 1;
}

var sourcePath = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);
var source = LoadBitmap(sourcePath);
var sizes = new[] { 256, 64, 48, 32, 24, 16 };
var frames = sizes.Select(size => EncodePng(Resize(source, size))).ToArray();

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
using var output = File.Create(outputPath);
using var writer = new BinaryWriter(output);
writer.Write((ushort)0);
writer.Write((ushort)1);
writer.Write((ushort)frames.Length);

var dataOffset = 6 + (16 * frames.Length);
for (var index = 0; index < frames.Length; index++)
{
    var size = sizes[index];
    writer.Write(size == 256 ? (byte)0 : (byte)size);
    writer.Write(size == 256 ? (byte)0 : (byte)size);
    writer.Write((byte)0);
    writer.Write((byte)0);
    writer.Write((ushort)1);
    writer.Write((ushort)32);
    writer.Write((uint)frames[index].Length);
    writer.Write((uint)dataOffset);
    dataOffset += frames[index].Length;
}

foreach (var frame in frames)
{
    writer.Write(frame);
}

return 0;

static BitmapSource LoadBitmap(string path)
{
    using var stream = File.OpenRead(path);
    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
    var frame = decoder.Frames[0];
    frame.Freeze();
    return frame;
}

static BitmapSource Resize(BitmapSource source, int size)
{
    var visual = new DrawingVisual();
    RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
    using (var drawingContext = visual.RenderOpen())
    {
        drawingContext.DrawImage(source, new Rect(0, 0, size, size));
    }

    var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(visual);
    bitmap.Freeze();
    return bitmap;
}

static byte[] EncodePng(BitmapSource source)
{
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(source));
    using var stream = new MemoryStream();
    encoder.Save(stream);
    return stream.ToArray();
}
