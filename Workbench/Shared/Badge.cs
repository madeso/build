using System.ComponentModel;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml;
using Workbench.Commands.Build;

namespace Workbench.Shared;

[TypeConverter(typeof(EnumTypeConverter<BadgeColor>))]
[JsonConverter(typeof(EnumJsonConverter<BadgeColor>))]
public enum BadgeColor
{
    [EnumString("green")]
    Green,

    [EnumString("light-green")]
    LightGreen,

    [EnumString("yellow")]
    Yellow,

    [EnumString("red")]
    Red,

    [EnumString("light-grey")]
    LightGrey,

    [EnumString("grey")]
    Grey
}

public class Badge
{
    private const string SVG_NAMESPACE = "http://www.w3.org/2000/svg";

    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string FontFamily { get; set; } = "Verdana";
    public float FontSize { get; set; } = 11.0f;
    public BadgeColor LabelColor { get; set; } = BadgeColor.Grey;
    public BadgeColor ValueColor { get; set; } = BadgeColor.Green;
    public string TextColor { get; set; } = "#fff";

    public int VerticalPadding { get; set; } = 10;
    public int HorizontalPadding { get; set; } = 10;

    // set tp null to make corners sharp
    public float? CornerRadius { get; set; } = 6.0f;

    private enum Side { Left, Right }
    private sealed record RoundedSide(float Radius, Side Side);

    private static string ToHexColor(BadgeColor c)
        => c switch
        {
            BadgeColor.Green => "#4c1",
            BadgeColor.LightGreen => "#a3c51c",
            BadgeColor.Yellow => "#dfb317",
            BadgeColor.Red => "#e05d44",
            BadgeColor.LightGrey => "#9f9f9f",
            BadgeColor.Grey => "#555",
            _ => "#f00"
        };

    private static float EstimateTextWidth(string text, float font_size_in_pixels)
    {
        var average_char_width = font_size_in_pixels * 0.6f;
        return text.Length * average_char_width;
    }

    private static float EstimateTextHeight(float font_size_in_pixels)
    {
        return font_size_in_pixels;
    }

    private static float EstimateTextDescent(float font_size_in_pixels)
    {
        return font_size_in_pixels * 0.25f;
    }

    public string GenerateSvg()
    {
        var pixel_size = FontSize * (96.0f / 72.0f); // Convert points to pixels (assuming 96 DPI)

        var label_width = EstimateTextWidth(Label, pixel_size) + HorizontalPadding;
        var value_width = EstimateTextWidth(Value, pixel_size) + HorizontalPadding;
        var height = EstimateTextHeight(pixel_size) + VerticalPadding;
        var descent = EstimateTextDescent(pixel_size);

        var doc = new XmlDocument();
        var svg = doc.CreateElement("svg", SVG_NAMESPACE);
        svg.SetAttribute("width", Svg.S(label_width + value_width));
        svg.SetAttribute("height", Svg.S(height));

        svg.AppendChild(CreateBackgroundRect(doc, LabelColor, 0, 0, label_width, height, make(CornerRadius, Side.Left)));

        svg.AppendChild(CreateBackgroundRect(doc, ValueColor, label_width, 0, value_width, height, make(CornerRadius, Side.Right)));

        svg.AppendChild(CreateText(doc, Label, HorizontalPadding / 2.0f, height - (VerticalPadding + descent) / 2.0f, label_width - HorizontalPadding));
        svg.AppendChild(CreateText(doc, Value, label_width + HorizontalPadding / 2.0f, height - (VerticalPadding + descent) / 2.0f, value_width - HorizontalPadding));

        doc.AppendChild(svg);

        return XmlToString(doc);

        static RoundedSide? make(float? radius, Side side)
            => radius == null ? null : new(radius.Value, side);
    }

    private static XmlElement CreateBackgroundRect(XmlDocument doc, BadgeColor color, float x, float y, float width, float height, RoundedSide? rounded)
    {
        var elem = rounded != null ? CreateRoundedRect(doc, x, y, width, height, rounded.Radius, rounded.Side == Side.Left, rounded.Side == Side.Right) : CreateStraightRect(doc, x, y, width, height);
        elem.SetAttribute("fill", ToHexColor(color));
        return elem;
    }

    private static XmlElement CreateStraightRect(XmlDocument doc, float x, float y, float width, float height)
    {
        var elem = doc.CreateElement("rect", SVG_NAMESPACE);
        elem.SetAttribute("x", Svg.S(x));
        elem.SetAttribute("y", Svg.S(y));
        elem.SetAttribute("width", Svg.S(width));
        elem.SetAttribute("height", Svg.S(height));
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

    private static XmlElement CreateRoundedRect(XmlDocument doc, float x, float y, float width, float height, float radius, bool is_left_rounded, bool is_right_rounded)
    {
        var path = new SvgPathBuilder();
        path.MoveToAbsolute(x + radius, y);
        path.HorizontalLine(width - 2 * radius);
        if (is_right_rounded)
        {
            path.Arc(radius, radius, 0, 0, 1, radius , radius);
            path.VerticalLine(height - 2 * radius);
            path.Arc(radius, radius, 0, 0, 1, -radius, radius);
        }
        else
        {
            path.HorizontalLine(radius);
            path.VerticalLine(height);
            path.HorizontalLine(-radius);
        }
        path.HorizontalLine(-(width - 2 * radius));
        if (is_left_rounded)
        {
            path.Arc(radius, radius, 0, 0, 1, -radius, -radius);
            path.VerticalLine(-(height - 2 * radius));
            path.Arc(radius, radius, 0 ,0, 1, radius, -radius);
        }
        else
        {
            path.HorizontalLine(-radius);
            path.VerticalLine(-height);
            path.HorizontalLine(radius);
        }
        path.ClosePath();

        var elem = doc.CreateElement("path", SVG_NAMESPACE);

        elem.SetAttribute("d", path.ToString());

        return elem;
    }

    private XmlElement CreateText(XmlDocument doc, string text, float x, float y, float width)
    {
        var elem = doc.CreateElement("text", SVG_NAMESPACE);
        elem.SetAttribute("x", Svg.S(x));
        elem.SetAttribute("y", Svg.S(y));
        elem.SetAttribute("fill", TextColor);
        elem.SetAttribute("font-family", FontFamily);
        elem.SetAttribute("font-size", Svg.S(FontSize));
        elem.SetAttribute("textLength", Svg.S(width));
        elem.InnerText = text;
        return elem;
    }
}

internal class SvgPathBuilder
{
    private readonly List<string> path = new ();

    public override string ToString()
    {
        return string.Join(' ', path);
    }

    public void MoveToAbsolute(float x, float y)
    {
        path.Add($"M{Svg.S(x)},{Svg.S(y)}");
    }

    public void HorizontalLine(float dx)
    {
        path.Add($"h{Svg.S(dx)}");
    }

    public void Arc(float rx, float ry, int x_axis_rotation, int large_arc_flag, int sweep_flag, float dx, float dy)
    {
        path.Add($"a{Svg.S(rx)},{Svg.S(ry)} {x_axis_rotation} {large_arc_flag} {sweep_flag} {Svg.S(dx)},{Svg.S(dy)}");
    }

    public void VerticalLine(float dy)
    {
        path.Add($"v{Svg.S(dy)}");
    }

    public void ClosePath()
    {
        path.Add("z");
    }
}

internal static class Svg
{
    public static string S(float f)
    {
        return ((int)f).ToString();
    }
}
