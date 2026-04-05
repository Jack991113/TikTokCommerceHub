using System.Drawing;
using System.Drawing.Printing;
using Microsoft.Extensions.Options;
using TikTokOrderPrinter.Models;
using TikTokOrderPrinter.Options;

namespace TikTokOrderPrinter.Services;

public sealed class WindowsPrintService
{
    private readonly RuntimeStateStore _stateStore;
    private readonly PrintingOptions _options;

    public WindowsPrintService(RuntimeStateStore stateStore, IOptions<PrintingOptions> options)
    {
        _stateStore = stateStore;
        _options = options.Value;
    }

    public async Task<DateTimeOffset?> PrintAsync(OrderPrintArtifacts artifacts, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Direct printer output is only implemented for Windows in this project.");
        }

        var state = await _stateStore.GetSnapshotAsync(cancellationToken);
        var printerName = string.IsNullOrWhiteSpace(state.PrinterName)
            ? _options.PrinterName
            : state.PrinterName;
        var paperSize = LabelPaperProfiles.Resolve(
            string.IsNullOrWhiteSpace(state.PaperSize) ? _options.PaperSize : state.PaperSize,
            state.CustomPaperWidthMm ?? _options.CustomPaperWidthMm,
            state.CustomPaperHeightMm ?? _options.CustomPaperHeightMm,
            state.PaperWidthCharacters ?? _options.PaperWidthCharacters,
            state.BaseFontSize ?? _options.BaseFontSize,
            state.MinFontSize ?? _options.MinFontSize,
            state.MarginMm ?? _options.MarginMm);

        await Task.Run(() =>
        {
            using var document = new PrintDocument();
            document.DefaultPageSettings.Landscape = false;
            document.DefaultPageSettings.Margins = CreateMargins(paperSize);
            document.DefaultPageSettings.PaperSize = new PaperSize(
                paperSize.DisplayName,
                MmToHundredthsOfInch(paperSize.WidthMm),
                MmToHundredthsOfInch(paperSize.HeightMm));
            document.OriginAtMargins = true;

            if (!string.IsNullOrWhiteSpace(printerName))
            {
                document.PrinterSettings.PrinterName = printerName;
            }

            if (!document.PrinterSettings.IsValid)
            {
                throw new InvalidOperationException("The configured printer is invalid or unavailable.");
            }

            document.DocumentName = $"Packing Slip {artifacts.OrderId}";
            document.PrintController = new StandardPrintController();

            var lines = artifacts.TicketContent.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            document.PrintPage += (_, eventArgs) =>
            {
                var graphics = eventArgs.Graphics ?? throw new InvalidOperationException("Print graphics context is unavailable.");
                graphics.PageUnit = GraphicsUnit.Display;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var fontSize = FindBestFontSize(graphics, lines, eventArgs.MarginBounds.Width, eventArgs.MarginBounds.Height, paperSize);
                using var font = new Font("Microsoft YaHei UI", fontSize, FontStyle.Regular, GraphicsUnit.Point);
                using var brush = new SolidBrush(Color.Black);
                using var format = new StringFormat(StringFormat.GenericTypographic)
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Near,
                    Trimming = StringTrimming.None,
                    FormatFlags = StringFormatFlags.NoClip
                };

                var y = (float)eventArgs.MarginBounds.Top;
                foreach (var rawLine in lines)
                {
                    var line = string.IsNullOrWhiteSpace(rawLine) ? " " : rawLine;
                    var layout = new RectangleF(eventArgs.MarginBounds.Left, y, eventArgs.MarginBounds.Width, 10000f);
                    var size = graphics.MeasureString(line, font, new SizeF(eventArgs.MarginBounds.Width, 10000f), format);
                    graphics.DrawString(line, font, brush, layout, format);
                    y += size.Height;

                    if (y > eventArgs.MarginBounds.Bottom)
                    {
                        break;
                    }
                }

                eventArgs.HasMorePages = false;
            };

            cancellationToken.ThrowIfCancellationRequested();
            document.Print();
        }, cancellationToken);

        return DateTimeOffset.UtcNow;
    }

    private static Margins CreateMargins(LabelPaperProfiles.LabelPaperProfile paperSize)
    {
        var margin = MmToHundredthsOfInch(paperSize.MarginMm);
        return new Margins(margin, margin, margin, margin);
    }

    private static int MmToHundredthsOfInch(double millimeters) =>
        (int)Math.Round(millimeters / 25.4d * 100d, MidpointRounding.AwayFromZero);

    private static float FindBestFontSize(
        Graphics graphics,
        IReadOnlyList<string> lines,
        int maxWidth,
        int maxHeight,
        LabelPaperProfiles.LabelPaperProfile paperSize)
    {
        for (var fontSize = paperSize.BaseFontSize; fontSize >= paperSize.MinFontSize; fontSize -= 0.25f)
        {
            using var font = new Font("Microsoft YaHei UI", fontSize, FontStyle.Regular, GraphicsUnit.Point);
            using var format = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.None,
                FormatFlags = StringFormatFlags.NoClip
            };

            var totalHeight = 0f;
            foreach (var rawLine in lines)
            {
                var line = string.IsNullOrWhiteSpace(rawLine) ? " " : rawLine;
                var size = graphics.MeasureString(line, font, new SizeF(maxWidth, 10000f), format);
                totalHeight += size.Height;
                if (totalHeight > maxHeight)
                {
                    break;
                }
            }

            if (totalHeight <= maxHeight)
            {
                return fontSize;
            }
        }

        return paperSize.MinFontSize;
    }
}
