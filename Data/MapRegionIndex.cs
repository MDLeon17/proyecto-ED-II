using UfgEnvios.Algorithms;
using System.IO;
using System.Windows;
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
    private readonly TablaDispersion<string, int> _municipalLabels = new();
    private readonly Conjunto<int> _allMunicipalLabels = new();
    private readonly TablaDispersion<string, StreamGeometry> _contours = new();

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

        var sizes = new ListaDinamica<int> { 0 };
        var queue = new Cola<int>();
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

        BuildContours(outside);

        void TryAdd(int neighbor, int label)
        {
            if (_labels[neighbor] != 0 || !IsInterior(neighbor)) return;
            _labels[neighbor] = label;
            queue.Enqueue(neighbor);
        }
    }

    // Recupera el centro de los bordes gruesos y elimina los números del JPG.
    // El dibujo final usa geometrías, por lo que conserva la nitidez al escalar.
    private void BuildContours(int outside)
    {
        var regions = new int[_labels.Length];
        var pending = new Cola<int>();
        for (var i = 0; i < regions.Length; i++)
        {
            if (!_allMunicipalLabels.Contains(_labels[i]) && _labels[i] != outside) continue;
            regions[i] = _labels[i];
            pending.Enqueue(i);
        }
        while (pending.Count > 0)
        {
            var i = pending.Dequeue();
            var x = i % Width;
            var y = i / Width;
            if (x > 0) Spread(i - 1, regions[i]);
            if (x + 1 < Width) Spread(i + 1, regions[i]);
            if (y > 0) Spread(i - Width, regions[i]);
            if (y + 1 < Height) Spread(i + Width, regions[i]);
        }

        foreach (var (id, label) in _municipalLabels)
        {
            var edges = new TablaDispersion<int, ListaDinamica<int>>();
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var i = y * Width + x;
                if (regions[i] != label) continue;
                var a = y * (Width + 1) + x;
                var b = a + 1;
                var d = a + Width + 1;
                var c = d + 1;
                if (y == 0 || regions[i - Width] != label) Add(a, b);
                if (x == Width - 1 || regions[i + 1] != label) Add(b, c);
                if (y == Height - 1 || regions[i + Width] != label) Add(c, d);
                if (x == 0 || regions[i - 1] != label) Add(d, a);
            }
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                while (edges.Count > 0)
                {
                    var start = edges.First().Key;
                    var current = start;
                    var points = new ListaDinamica<Point>();
                    do
                    {
                        points.Add(new Point(current % (Width + 1), current / (Width + 1)));
                        var next = edges[current];
                        var end = next[^1];
                        next.RemoveAt(next.Count - 1);
                        if (next.Count == 0) edges.Remove(current);
                        current = end;
                    } while (current != start);
                    // Suavizado simétrico suave; no cambia la ubicación de los municipios.
                    var smooth = points.Select((p, i) =>
                    {
                        var previous = points[(i + points.Count - 2) % points.Count];
                        var following = points[(i + 2) % points.Count];
                        return new Point((previous.X + 2 * p.X + following.X) / 4,
                            (previous.Y + 2 * p.Y + following.Y) / 4);
                    }).ToArray();
                    context.BeginFigure(smooth[0], true, true);
                    context.PolyLineTo(smooth.Skip(1).ToArray(), true, false);
                }
            }
            geometry.Freeze();
            _contours.Add(id, geometry);

            void Add(int from, int to)
            {
                if (!edges.TryGetValue(from, out var next)) edges[from] = next = new ListaDinamica<int>();
                next.Add(to);
            }
        }

        void Spread(int index, int label)
        {
            if (regions[index] != 0) return;
            regions[index] = label;
            pending.Enqueue(index);
        }
    }

    public DrawingImage CreateMap(HubNode? origin, HubNode? destination, bool shadeOthers)
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, Width, Height));
            foreach (var (id, contour) in _contours)
            {
                var color = id == origin?.Id ? Color.FromRgb(174, 207, 226)
                    : id == destination?.Id ? Color.FromRgb(217, 184, 219)
                    : shadeOthers ? Color.FromRgb(237, 241, 241)
                    : Color.FromRgb(250, 252, 251);
                context.DrawGeometry(new SolidColorBrush(color), null, contour);
            }
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(111, 137, 142)), 1.15)
            { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            foreach (var contour in _contours.Values) context.DrawGeometry(null, pen, contour);
        }
        drawing.Freeze();
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
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
            if (origin != null && label == originLabel)
            {
                buffer[pixel] = 181; buffer[pixel + 1] = 117; buffer[pixel + 2] = 42; buffer[pixel + 3] = 150;
            }
            else if (destination != null && label == destinationLabel)
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
