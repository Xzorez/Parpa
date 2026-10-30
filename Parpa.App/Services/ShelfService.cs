using System.Diagnostics;
using System.IO.Compression;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Parpa.Services;

/// <summary>Archivo o carpeta en la Bandeja (una referencia: el original no se mueve).</summary>
public sealed record ShelfItem(string Path, DateTime Added)
{
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd('\\'));

    public bool IsFolder => Directory.Exists(Path);

    public string Extension => System.IO.Path.GetExtension(Path).TrimStart('.').ToLowerInvariant();

    public bool IsImage => Extension is "png" or "jpg" or "jpeg" or "gif" or "bmp" or "webp" or "tif" or "tiff" or "heic";

    public bool IsPdf => Extension == "pdf";

    /// <summary>Etiqueta corta de la tarjeta: IMG, DIR, PDF, ZIP o la extensión.</summary>
    public string Kind => IsFolder ? "DIR" : IsImage ? "IMG" : Extension.Length is > 0 and <= 4 ? Extension.ToUpperInvariant() : "FILE";
}

/// <summary>Acciones rápidas de la Bandeja (cada una se activa en Ajustes › Ventanas › Acciones rápidas).</summary>
public enum ShelfAction { CopyPath, OpenFolder, ExtractText, Resize, Compress, ToPng, MergePdf, Zip, Remove }

/// <summary>Bandeja de archivos (%APPDATA%\Parpa\shelf.json): se guardan hasta que se sacan.</summary>
public sealed class ShelfService
{
    private const double JpegQuality = 0.7;
    private const double ResizeQuality = 0.85;

    /// <summary>Lado mayor de las imágenes redimensionadas (Full HD).</summary>
    public const int ResizeMax = 1920;

    private readonly ParpaSettings _s;
    private readonly ClipboardService _clipboard;
    private readonly JsonStore<ShelfItem> _store = new("shelf");

    public IReadOnlyList<ShelfItem> Items => _store.Items;

    public event Action? Changed;

    public ShelfService(ParpaSettings settings, ClipboardService clipboard)
    {
        _s = settings;
        _clipboard = clipboard;
        // Lo que se borró o movió fuera de Parpa ya no está en la bandeja; con "Vaciar al reiniciar", nada.
        var removed = settings.ShelfClear
            ? _store.Items.RemoveAll(_ => true)
            : _store.Items.RemoveAll(i => !File.Exists(i.Path) && !Directory.Exists(i.Path));
        if (removed > 0) _store.Save();
    }

    /// <returns>Índice del primer elemento añadido (para seleccionarlo).</returns>
    public int Add(IEnumerable<string> paths)
    {
        var first = _store.Items.Count;
        foreach (var path in paths)
        {
            if (_store.Items.Any(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
            if (!File.Exists(path) && !Directory.Exists(path)) continue;
            _store.Items.Add(new ShelfItem(path, DateTime.Now));
        }
        _store.Save();
        Changed?.Invoke();
        return first;
    }

    public void Remove(ShelfItem item)
    {
        if (!_store.Items.Remove(item)) return;
        _store.Save();
        Changed?.Invoke();
    }

    /// <summary>Acciones disponibles para un elemento, según su tipo y los ajustes.</summary>
    public IReadOnlyList<ShelfAction> ActionsFor(ShelfItem item)
    {
        bool On(string key) => _s.Actions.GetValueOrDefault(key, true);
        var actions = new List<ShelfAction>();
        if (On("TODOS · COPIAR RUTA")) actions.Add(ShelfAction.CopyPath);
        actions.Add(ShelfAction.OpenFolder);
        if (item.IsImage && On("IMÁGENES · EXTRAER TEXTO")) actions.Add(ShelfAction.ExtractText);
        if (item.IsImage && On("IMÁGENES · REDIMENSIONAR")) actions.Add(ShelfAction.Resize);
        if (item.IsImage && On("IMÁGENES · COMPRIMIR")) actions.Add(ShelfAction.Compress);
        if (item.IsImage && item.Extension != "png" && On("IMÁGENES · A PNG")) actions.Add(ShelfAction.ToPng);
        if (item.IsPdf && On("PDF · UNIR")) actions.Add(ShelfAction.MergePdf);
        if (item.IsFolder && On("CARPETAS · ZIP")) actions.Add(ShelfAction.Zip);
        actions.Add(ShelfAction.Remove);
        return actions;
    }

    public static string LabelKey(ShelfAction action) => action switch
    {
        ShelfAction.CopyPath => "shelf_act_copy_path",
        ShelfAction.OpenFolder => "peek_show_folder",
        ShelfAction.ExtractText => "shelf_act_ocr",
        ShelfAction.Resize => "shelf_act_resize",
        ShelfAction.Compress => "shelf_act_compress",
        ShelfAction.ToPng => "shelf_act_png",
        ShelfAction.MergePdf => "shelf_act_merge_pdf",
        ShelfAction.Zip => "shelf_act_zip",
        _ => "shelf_act_remove",
    };

    public static string VerbKey(ShelfAction action) => action switch
    {
        ShelfAction.ToPng => "shelf_verb_png",
        ShelfAction.MergePdf => "shelf_verb_merge",
        ShelfAction.Zip => "shelf_verb_zip",
        ShelfAction.ExtractText => "shelf_verb_ocr",
        ShelfAction.Resize => "shelf_verb_resize",
        _ => "shelf_verb_compress",
    };

    /// <summary>Ejecuta una acción. Devuelve el mensaje de éxito; lanza si falla.</summary>
    public async Task<string> RunAsync(ShelfAction action, ShelfItem item)
    {
        switch (action)
        {
            case ShelfAction.CopyPath:
                _clipboard.Copy(item.Path);
                return Loc.Get("shelf_done_path");
            case ShelfAction.OpenFolder:
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.Path}\"") { UseShellExecute = true });
                return Loc.Get("shelf_done_folder");
            case ShelfAction.Remove:
                Remove(item);
                return string.Empty;
            case ShelfAction.MergePdf when _store.Items.Count(i => i.IsPdf) < 2:
                return Loc.Get("shelf_need_two_pdf");
            case ShelfAction.ExtractText:
                return (await CopyTextAsync(item.Path)).Message;
            case ShelfAction.Resize:
                // Como UNIR PDF: todas las imágenes de la bandeja a la vez, empezando por la elegida.
                return await ResizeAndAddAsync(new[] { item }.Concat(_store.Items.Where(i => i.IsImage && i != item)).Select(i => i.Path));
        }

        var output = action switch
        {
            ShelfAction.Compress => await ReencodeAsync(item.Path, BitmapEncoder.JpegEncoderId, "-min", ".jpg", JpegQuality),
            ShelfAction.ToPng => await ReencodeAsync(item.Path, BitmapEncoder.PngEncoderId, string.Empty, ".png", null),
            ShelfAction.MergePdf => await Task.Run(() => MergePdfs(item)),
            ShelfAction.Zip => await Task.Run(() => ZipFolder(item.Path)),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        Add([output]);
        return Loc.Format("shelf_done_created", ("file", Loc.Upper(Path.GetFileName(output))));
    }

    private static async Task<string> ReencodeAsync(string source, Guid encoderId, string suffix, string extension, double? quality)
    {
        var target = UniquePath(Path.Combine(Path.GetDirectoryName(source)!, Path.GetFileNameWithoutExtension(source) + suffix + extension));
        var input = await StorageFile.GetFileFromPathAsync(source);
        using var inStream = await input.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(inStream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        await File.WriteAllBytesAsync(target, []);
        var outFile = await StorageFile.GetFileFromPathAsync(target);
        try
        {
            using var outStream = await outFile.OpenAsync(FileAccessMode.ReadWrite);
            BitmapEncoder encoder;
            if (quality is { } q)
            {
                var options = new BitmapPropertySet { ["ImageQuality"] = new BitmapTypedValue(q, Windows.Foundation.PropertyType.Single) };
                encoder = await BitmapEncoder.CreateAsync(encoderId, outStream, options);
            }
            else encoder = await BitmapEncoder.CreateAsync(encoderId, outStream);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
        }
        catch
        {
            File.Delete(target);
            throw;
        }
        return target;
    }

    // ---- Texto de una imagen (OCR de Windows, sin conexión) ----

    /// <summary>Lee el texto de la imagen, lo copia y lo deja el primero del Portapapeles de Parpa.</summary>
    public async Task<(bool Copied, string Message)> CopyTextAsync(string path)
    {
        var text = await ExtractTextAsync(path);
        if (string.IsNullOrWhiteSpace(text)) return (false, Loc.Get("shelf_no_text"));
        _clipboard.Copy(text);
        _clipboard.Add(text, announce: false);
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return (true, Loc.Format("shelf_done_text", ("n", words)));
    }

    /// <summary>
    /// Texto de una imagen con el reconocedor de Windows en los idiomas del usuario (Windows.Media.Ocr):
    /// todo se hace en el PC. Las líneas se conservan; <c>null</c> si no hay reconocedor para esos idiomas.
    /// </summary>
    public static async Task<string?> ExtractTextAsync(string path)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
        {
            Log.Info("OCR: sin reconocedor para los idiomas del usuario");
            return null;
        }
        using var bitmap = await DecodeAsync(path);
        using var scaled = await FitAsync(bitmap, (int)OcrEngine.MaxImageDimension);
        var result = await engine.RecognizeAsync(scaled);
        return string.Join(Environment.NewLine, result.Lines.Select(l => l.Text)).Trim();
    }

    // ---- Redimensionar ----

    /// <summary>Redimensiona las imágenes, las añade a la bandeja y devuelve el mensaje para el usuario.</summary>
    public async Task<string> ResizeAndAddAsync(IEnumerable<string> paths)
    {
        var outputs = new List<string>();
        foreach (var path in paths)
            if (await ResizeAsync(path, ResizeMax) is { } output) outputs.Add(output);
        if (outputs.Count == 0) return Loc.Format("shelf_resize_small", ("px", ResizeMax));
        Add(outputs);
        return Loc.Format("shelf_done_resized", ("n", outputs.Count), ("px", ResizeMax));
    }

    /// <summary>
    /// Copia con el lado mayor a <paramref name="max"/> px junto al original ("foto-1920.jpg"). Los PNG siguen
    /// siendo PNG; el resto pasa a JPEG. <c>null</c> si ya era igual o más pequeña.
    /// </summary>
    private static async Task<string?> ResizeAsync(string source, int max)
    {
        using var bitmap = await DecodeAsync(source);
        if (Math.Max(bitmap.PixelWidth, bitmap.PixelHeight) <= max) return null;
        var png = Path.GetExtension(source).Equals(".png", StringComparison.OrdinalIgnoreCase);
        var target = UniquePath(Path.Combine(Path.GetDirectoryName(source)!, $"{Path.GetFileNameWithoutExtension(source)}-{max}{(png ? ".png" : ".jpg")}"));

        await File.WriteAllBytesAsync(target, []);
        var outFile = await StorageFile.GetFileFromPathAsync(target);
        try
        {
            using var outStream = await outFile.OpenAsync(FileAccessMode.ReadWrite);
            await EncodeAsync(bitmap, outStream, png ? BitmapEncoder.PngEncoderId : BitmapEncoder.JpegEncoderId, max, png ? null : ResizeQuality);
        }
        catch
        {
            File.Delete(target);
            throw;
        }
        return target;
    }

    /// <summary>Imagen ya girada según su EXIF (las fotos del móvil vienen de lado).</summary>
    private static async Task<SoftwareBitmap> DecodeAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            new BitmapTransform(), ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);
    }

    /// <summary>La misma imagen reducida (en memoria) para que su lado mayor no pase de <paramref name="max"/>.</summary>
    private static async Task<SoftwareBitmap> FitAsync(SoftwareBitmap bitmap, int max)
    {
        if (Math.Max(bitmap.PixelWidth, bitmap.PixelHeight) <= max) return SoftwareBitmap.Copy(bitmap);
        using var memory = new InMemoryRandomAccessStream();
        await EncodeAsync(bitmap, memory, BitmapEncoder.BmpEncoderId, max, null);
        memory.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(memory);
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }

    private static async Task EncodeAsync(SoftwareBitmap bitmap, IRandomAccessStream stream, Guid encoderId, int max, double? quality)
    {
        var encoder = quality is { } q
            ? await BitmapEncoder.CreateAsync(encoderId, stream, new BitmapPropertySet { ["ImageQuality"] = new BitmapTypedValue(q, Windows.Foundation.PropertyType.Single) })
            : await BitmapEncoder.CreateAsync(encoderId, stream);
        encoder.SetSoftwareBitmap(bitmap);
        var scale = Math.Min(1, max / (double)Math.Max(bitmap.PixelWidth, bitmap.PixelHeight));
        encoder.BitmapTransform.ScaledWidth = (uint)Math.Max(1, Math.Round(bitmap.PixelWidth * scale));
        encoder.BitmapTransform.ScaledHeight = (uint)Math.Max(1, Math.Round(bitmap.PixelHeight * scale));
        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        await encoder.FlushAsync();
    }

    /// <summary>Une el PDF elegido con el resto de PDF de la bandeja, en ese orden.</summary>
    private string MergePdfs(ShelfItem first)
    {
        var sources = new[] { first }.Concat(_store.Items.Where(i => i.IsPdf && i != first)).ToList();
        var target = UniquePath(Path.Combine(Path.GetDirectoryName(first.Path)!,
            $"{Path.GetFileNameWithoutExtension(first.Path)} (+{sources.Count - 1}).pdf"));
        using var output = new PdfDocument();
        foreach (var source in sources)
        {
            using var input = PdfReader.Open(source.Path, PdfDocumentOpenMode.Import);
            foreach (var page in input.Pages) output.AddPage(page);
        }
        output.Save(target);
        return target;
    }

    private static string ZipFolder(string folder)
    {
        var target = UniquePath(folder.TrimEnd('\\') + ".zip");
        ZipFile.CreateFromDirectory(folder, target, CompressionLevel.Optimal, includeBaseDirectory: true);
        return target;
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var n = 2; ; n++)
        {
            var candidate = Path.Combine(dir, $"{name} ({n}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }

    /// <summary>"2,4 MB" con la cultura activa, o CARPETA.</summary>
    public static string SizeText(ShelfItem item)
    {
        if (item.IsFolder) return Loc.Get("shelf_folder");
        try
        {
            double size = new FileInfo(item.Path).Length;
            string[] units = ["B", "KB", "MB", "GB"];
            var unit = 0;
            while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
            return string.Format(Loc.Culture, unit == 0 ? "{0:0} {1}" : "{0:0.#} {1}", size, units[unit]);
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }
}
