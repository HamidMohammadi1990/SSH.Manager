using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var outputDir = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets");

Directory.CreateDirectory(outputDir);

var pngPath = Path.Combine(outputDir, "app-icon.png");
var icoPath = Path.Combine(outputDir, "app-icon.ico");

using (var bitmap = CreateIconBitmap(512))
{
    bitmap.Save(pngPath, ImageFormat.Png);
    SaveAsIcon(bitmap, icoPath);
}

Console.WriteLine($"Created: {pngPath}");
Console.WriteLine($"Created: {icoPath}");

static Bitmap CreateIconBitmap(int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.Clear(Color.Transparent);

    var margin = size * 0.08f;
    var rect = new RectangleF(margin, margin, size - margin * 2, size - margin * 2);
    var radius = size * 0.18f;

    using (var bgBrush = new LinearGradientBrush(
               rect,
               Color.FromArgb(255, 30, 30, 46),
               Color.FromArgb(255, 20, 20, 32),
               LinearGradientMode.ForwardDiagonal))
    {
        FillRoundedRect(g, bgBrush, rect, radius);
    }

    using var borderPen = new Pen(Color.FromArgb(80, 124, 77, 255), size * 0.012f);
    DrawRoundedRect(g, borderPen, rect, radius);

    var accent = Color.FromArgb(255, 124, 77, 255);
    var accentLight = Color.FromArgb(255, 179, 136, 255);
    var termRect = new RectangleF(
        rect.X + size * 0.14f,
        rect.Y + size * 0.18f,
        rect.Width * 0.72f,
        rect.Height * 0.52f);

    using (var termBrush = new SolidBrush(Color.FromArgb(255, 37, 37, 54)))
        FillRoundedRect(g, termBrush, termRect, size * 0.06f);

    using var titleBrush = new SolidBrush(accent);
    var dotY = termRect.Y + size * 0.055f;
    var dotR = size * 0.018f;
    g.FillEllipse(titleBrush, termRect.X + size * 0.05f, dotY, dotR * 2, dotR * 2);
    g.FillEllipse(new SolidBrush(Color.FromArgb(255, 255, 193, 7)), termRect.X + size * 0.11f, dotY, dotR * 2, dotR * 2);
    g.FillEllipse(new SolidBrush(Color.FromArgb(255, 76, 175, 80)), termRect.X + size * 0.17f, dotY, dotR * 2, dotR * 2);

    using var linePen = new Pen(accentLight, size * 0.022f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
    var lx = termRect.X + size * 0.06f;
    var ly = termRect.Y + size * 0.16f;
    g.DrawLine(linePen, lx, ly, lx + size * 0.22f, ly);
    g.DrawLine(new Pen(Color.FromArgb(200, 160, 160, 184), size * 0.018f), lx, ly + size * 0.09f, lx + size * 0.30f, ly + size * 0.09f);
    g.DrawLine(new Pen(Color.FromArgb(160, 160, 160, 184), size * 0.018f), lx, ly + size * 0.17f, lx + size * 0.18f, ly + size * 0.17f);

    using var promptBrush = new SolidBrush(accent);
    var font = new Font("Consolas", size * 0.09f, FontStyle.Bold, GraphicsUnit.Pixel);
    g.DrawString(">", font, promptBrush, lx, ly - size * 0.02f);

    var nodeY = rect.Bottom - size * 0.20f;
    var nodes = new[]
    {
        new PointF(rect.X + size * 0.22f, nodeY),
        new PointF(rect.X + size * 0.50f, nodeY - size * 0.05f),
        new PointF(rect.Right - size * 0.22f, nodeY)
    };

    using var linkPen = new Pen(accent, size * 0.025f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
    g.DrawLine(linkPen, nodes[0], nodes[1]);
    g.DrawLine(linkPen, nodes[1], nodes[2]);

    var nodeR = size * 0.045f;
    foreach (var node in nodes)
    {
        using var glow = new SolidBrush(Color.FromArgb(60, accent));
        g.FillEllipse(glow, node.X - nodeR * 1.4f, node.Y - nodeR * 1.4f, nodeR * 2.8f, nodeR * 2.8f);
        g.FillEllipse(new SolidBrush(accentLight), node.X - nodeR, node.Y - nodeR, nodeR * 2, nodeR * 2);
        g.FillEllipse(new SolidBrush(Color.White), node.X - nodeR * 0.35f, node.Y - nodeR * 0.35f, nodeR * 0.7f, nodeR * 0.7f);
    }

    return bmp;
}

static void FillRoundedRect(Graphics g, Brush brush, RectangleF rect, float radius)
{
    using var path = CreateRoundedRect(rect, radius);
    g.FillPath(brush, path);
}

static void DrawRoundedRect(Graphics g, Pen pen, RectangleF rect, float radius)
{
    using var path = CreateRoundedRect(rect, radius);
    g.DrawPath(pen, path);
}

static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
{
    var path = new GraphicsPath();
    var d = radius * 2;
    path.AddArc(rect.X, rect.Y, d, d, 180, 90);
    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
    path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}

static void SaveAsIcon(Bitmap source, string path)
{
    using var resized = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(resized))
    {
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(source, 0, 0, 256, 256);
    }

    using var icon = Icon.FromHandle(resized.GetHicon());
    using var fs = File.Create(path);
    icon.Save(fs);
}
