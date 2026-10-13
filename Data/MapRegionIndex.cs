using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UfgEnvios.Models;

namespace UfgEnvios.Data;

/// <summary>
/// Separa las áreas blancas encerradas por los bordes de la imagen de referencia.
/// El resultado permite teñir municipios completos sin alterar el archivo original.
/// </summary>
public sealed class MapRegionIndex
{
    private const int Width = 775;
    private const int Height = 780;
    private readonly int[] _labels = new int[Width * Height];
    private readonly Dictionary<string, int> _municipalLabels = new();
    private readonly HashSet<int> _allMunicipalLabels = new();

    public MapRegionIndex(BitmapSource source, IEnumerable<HubNode> nodes)
    {
        if (source.PixelWidth != Width || source.PixelHeight < Height)
            throw new InvalidDataException("La imagen de referencia cambió de tamaño.");

        var bitmap = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = Width * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        bool IsInterior(int index)
        {
            var x = index % Width;
            var y = index / Width;
            var pixel = y * stride + x * 4;
            return pixels[pixel] > 185 && pixels[pixel + 1] > 185 && pixels[pixel + 2] > 185;
        }

        var sizes = new List<int> { 0 };
        var queue = new Queue<int>();
        for (var index = 0; index < _labels.Length; index++)
        {
            if (_labels[index] != 0 || !IsInterior(index)) continue;
            var label = sizes.Count;
            var size = 0;
            _labels[index] = label;
            queue.Enqueue(index);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                size++;
                var x = current % Width;
                var y = current / Width;
                if (x > 0) TryAdd(current - 1, label);
                if (x < Width - 1) TryAdd(current + 1, label);
                if (y > 0) TryAdd(current - Width, label);
                if (y < Height - 1) TryAdd(current + Width, label);
            }
            sizes.Add(size);
        }

        var outside = _labels[0];
        foreach (var node in nodes)
        {
            var label = _labels[node.RegionY * Width + node.RegionX];
            if (label == 0 || label == outside || sizes[label] < 200 || !_allMunicipalLabels.Add(label))
                throw new InvalidDataException($"No se pudo identificar el contorno de {node.Name}.");
            _municipalLabels.Add(node.Id, label);
        }

        void TryAdd(int neighbor, int label)
        {
            if (_labels[neighbor] != 0 || !IsInterior(neighbor)) return;
            _labels[neighbor] = label;
            queue.Enqueue(neighbor);
        }
    }

    public BitmapSource? CreateOverlay(HubNode? origin, HubNode? destination, bool shadeOthers)
    {
        if (origin == null && destination == null) return null;
        var originLabel = origin == null ? 0 : _municipalLabels[origin.Id];
        var destinationLabel = destination == null ? 0 : _municipalLabels[destination.Id];
        var buffer = new byte[Width * Height * 4];
        for (var index = 0; index < _labels.Length; index++)
        {
            var label = _labels[index];
            var pixel = index * 4;
            if (label == originLabel)
            {
                buffer[pixel] = 181; buffer[pixel + 1] = 117; buffer[pixel + 2] = 42; buffer[pixel + 3] = 150;
            }
            else if (label == destinationLabel)
            {
                buffer[pixel] = 187; buffer[pixel + 1] = 96; buffer[pixel + 2] = 160; buffer[pixel + 3] = 150;
            }
            else if (shadeOthers && _allMunicipalLabels.Contains(label))
            {
                buffer[pixel] = 184; buffer[pixel + 1] = 184; buffer[pixel + 2] = 184; buffer[pixel + 3] = 55;
            }
        }
        var result = BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Bgra32, null, buffer, Width * 4);
        result.Freeze();
        return result;
    }
}
