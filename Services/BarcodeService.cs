using InvenTrack.Data;
using InvenTrack.Models;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using System.Text;

namespace InvenTrack.Services
{
    public class BarcodeService
    {
        private readonly InvenTrackContext _context;

        private static readonly Dictionary<char, string> Code39Patterns = new()
        {
            ['0'] = "nnnwwnwnn", ['1'] = "wnnwnnnnw", ['2'] = "nnwwnnnnw", ['3'] = "wnwwnnnnn",
            ['4'] = "nnnwwnnnw", ['5'] = "wnnwwnnnn", ['6'] = "nnwwwnnnn", ['7'] = "nnnwnnwnw",
            ['8'] = "wnnwnnwnn", ['9'] = "nnwwnnwnn", ['A'] = "wnnnnwnnw", ['B'] = "nnwnnwnnw",
            ['C'] = "wnwnnwnnn", ['D'] = "nnnnwwnnw", ['E'] = "wnnnwwnnn", ['F'] = "nnwnwwnnn",
            ['G'] = "nnnnnwwnw", ['H'] = "wnnnnwwnn", ['I'] = "nnwnnwwnn", ['J'] = "nnnnwwwnn",
            ['K'] = "wnnnnnnww", ['L'] = "nnwnnnnww", ['M'] = "wnwnnnnwn", ['N'] = "nnnnwnnww",
            ['O'] = "wnnnwnnwn", ['P'] = "nnwnwnnwn", ['Q'] = "nnnnnnwww", ['R'] = "wnnnnnwwn",
            ['S'] = "nnwnnnwwn", ['T'] = "nnnnwnwwn", ['U'] = "wwnnnnnnw", ['V'] = "nwwnnnnnw",
            ['W'] = "wwwnnnnnn", ['X'] = "nwnnwnnnw", ['Y'] = "wwnnwnnnn", ['Z'] = "nwwnwnnnn",
            ['-'] = "nwnnnnwnw", ['.'] = "wwnnnnwnn", [' '] = "nwwnnnwnn", ['$'] = "nwnwnwnnn",
            ['/'] = "nwnwnnnwn", ['+'] = "nwnnnwnwn", ['%'] = "nnnwnwnwn", ['*'] = "nwnnwnwnn"
        };

        public BarcodeService(InvenTrackContext context)
        {
            _context = context;
        }

        public static string NormalizeBarcode(string? value)
        {
            return new string((value ?? string.Empty).Trim().ToUpperInvariant().Where(ch => Code39Patterns.ContainsKey(ch) && ch != '*').ToArray());
        }

        public async Task<string> GenerateUniqueBarcodeAsync()
        {
            while (true)
            {
                var code = $"ITM-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
                code = NormalizeBarcode(code);
                if (!await _context.InventoryItems.AnyAsync(i => i.Barcode == code))
                    return code;
            }
        }

        public async Task BackfillMissingBarcodesAsync()
        {
            var items = await _context.InventoryItems.Where(i => i.Barcode == null || i.Barcode == "").ToListAsync();
            if (!items.Any()) return;
            foreach (var item in items)
            {
                item.Barcode = await GenerateUniqueBarcodeAsync();
            }
            await _context.SaveChangesAsync();
        }

        public byte[] RenderCode39Png(string barcodeValue, int height = 140)
        {
            var normalized = NormalizeBarcode(barcodeValue);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new InvalidOperationException("Barcode value is empty.");

            var full = $"*{normalized}*";
            var modules = BuildModules(full);
            int quiet = 14;
            int moduleWidth = 3;
            int width = (modules.Sum(x => x) + quiet * 2) * moduleWidth;
            int textArea = 38;

            using var surface = SKSurface.Create(new SKImageInfo(width, height + textArea));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill, IsAntialias = false };
            int x = quiet * moduleWidth;
            bool drawBar = true;
            foreach (var m in modules)
            {
                var px = m * moduleWidth;
                if (drawBar)
                    canvas.DrawRect(x, 8, px, height - 16, paint);
                x += px;
                drawBar = !drawBar;
            }

            using var textPaint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true,
                TextSize = 22,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Arial")
            };
            canvas.DrawText(normalized, width / 2f, height + 26, textPaint);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        private static List<int> BuildModules(string full)
        {
            var modules = new List<int>();
            for (int i = 0; i < full.Length; i++)
            {
                var pattern = Code39Patterns[full[i]];
                foreach (var c in pattern)
                    modules.Add(c == 'w' ? 3 : 1);
                if (i < full.Length - 1)
                    modules.Add(1);
            }
            return modules;
        }
    }
}
