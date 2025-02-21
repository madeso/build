using System.Text;
using System.Xml;
using SkiaSharp;

namespace Workbench.Shared;

public enum BadgeColor
{
    Green,
    LightGreen,
    Yellow,
    Red,
    LightGrey,
    Grey
}

public class Badge
{
    private const string SVG_NAMESPACE = "http://www.w3.org/2000/svg";

    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string Font { get; set; } = "Verdana";
    public float FontSize { get; set; } = 11.0f;
    public BadgeColor LabelColor { get; set; } = BadgeColor.Grey;
    public BadgeColor ValueColor { get; set; } = BadgeColor.Green;
    public string TextColor { get; set; } = "#fff";

    public int VerticalPadding { get; set; } = 10;
    public int HorizontalPadding { get; set; } = 10;
    public float CornerRadius { get; set; } = 6.0f;

    private enum RoundSide { Left, Right}

    private static string ToHexColor(BadgeColor c)
        => c switch
        {
            BadgeColor.Green => "#4c1",
            BadgeColor.LightGreen => "#a3c51c",
            BadgeColor.Yellow => "#dfb317",
            BadgeColor.Red => "#e05d44",
            BadgeColor.LightGrey => "#9f9f9f",
            BadgeColor.Grey => "#555",
            _ => ""
        };

    private static SKRect MeasureString(string text, SKPaint paint)
    {
        var bounds = new SKRect();
        paint.MeasureText(text, ref bounds);
        return bounds;
    }

    private static string S(float f)
    {
        return ((int)f).ToString();
    }

    public string GenerateSvg()
    {
        using var paint = new SKPaint();
        paint.TextSize = FontSize * (96.0f / 72.0f); // Convert points to pixels (assuming 96 DPI)
        paint.Typeface = SKTypeface.FromFamilyName(Font);
        var metrics = paint.FontMetrics;

        var label_rect = MeasureString(Label, paint);
        var value_rect = MeasureString(Value, paint);

        var label_width = label_rect.Width + HorizontalPadding;
        var value_width = value_rect.Width + HorizontalPadding;
        var height = Math.Max(label_rect.Height, value_rect.Height) + VerticalPadding;

        var doc = new XmlDocument();
        var svg = doc.CreateElement("svg", SVG_NAMESPACE);
        svg.SetAttribute("width", S(label_width + value_width));
        svg.SetAttribute("height", S(height));

        svg.AppendChild(CreateRect(doc, LabelColor, 0, 0, label_width, height, CornerRadius, RoundSide.Left));
        svg.AppendChild(CreateRect(doc, ValueColor, label_width, 0, value_width, height, CornerRadius, RoundSide.Right));

        svg.AppendChild(CreateText(doc, Label, HorizontalPadding / 2.0f, height - (VerticalPadding+ metrics.Descent) / 2.0f, label_width - HorizontalPadding));
        svg.AppendChild(CreateText(doc, Value, label_width + HorizontalPadding / 2.0f, height - (VerticalPadding + metrics.Descent) / 2.0f, value_width - HorizontalPadding));

        doc.AppendChild(svg);

        return XmlToString(doc);
    }

    private XmlElement CreateRect(XmlDocument doc, BadgeColor color, float x, float y, float width, float height, float radius, RoundSide round_round_side)
    {
        var path = CreateRoundedRectPath(x, y, width, height, radius, round_round_side == RoundSide.Left, round_round_side == RoundSide.Right);

        var elem = doc.CreateElement("path", SVG_NAMESPACE);
        elem.SetAttribute("d", path);
        elem.SetAttribute("fill", ToHexColor(color));
        return elem;
    }

    private static string XmlToString(XmlDocument doc)
    {
        var builder = new StringBuilder();
        var settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true };
        using (var writer = XmlWriter.Create(builder, settings))
        {
            doc.Save(writer);
        }

        return builder.ToString();
    }

    private string CreateRoundedRectPath(float x, float y, float width, float height, float radius, bool is_left_rounded, bool is_right_rounded)
    {
        var path = new StringBuilder();
        path.Append($"M{S(x + radius)},{S(y)} ");
        path.Append($"h{S(width - 2 * radius)} ");
        if (is_right_rounded)
        {
            path.Append($"a{S(radius)},{S(radius)} 0 0 1 {S(radius)},{S(radius)} ");
            path.Append($"v{S(height - 2 * radius)} ");
            path.Append($"a{S(radius)},{S(radius)} 0 0 1 -{S(radius)},{S(radius)} ");
        }
        else
        {
            path.Append($"h{S(radius)} ");
            path.Append($"v{S(height)} ");
            path.Append($"h-{S(radius)} ");
        }
        path.Append($"h-{S(width - 2 * radius)} ");
        if (is_left_rounded)
        {
            path.Append($"a{S(radius)},{S(radius)} 0 0 1 -{S(radius)},-{S(radius)} ");
            path.Append($"v-{S(height - 2 * radius)} ");
            path.Append($"a{S(radius)},{S(radius)} 0 0 1 {S(radius)},-{S(radius)} ");
        }
        else
        {
            path.Append($"h-{S(radius)} ");
            path.Append($"v-{S(height)} ");
            path.Append($"h{S(radius)} ");
        }
        path.Append("z");
        return path.ToString();
    }

    private XmlElement CreateText(XmlDocument doc, string text, float x, float y, float width)
    {
        var elem = doc.CreateElement("text", SVG_NAMESPACE);
        elem.SetAttribute("x", S(x));
        elem.SetAttribute("y", S(y));
        elem.SetAttribute("fill", TextColor);
        elem.SetAttribute("font-family", Font);
        elem.SetAttribute("font-size", S(FontSize));
        elem.SetAttribute("textLength", S(width));
        elem.InnerText = text;
        return elem;
    }
}

