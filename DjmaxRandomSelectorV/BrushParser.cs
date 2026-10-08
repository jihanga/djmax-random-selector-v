using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace DjmaxRandomSelectorV
{
    // CSS background 문법의 부분집합을 WPF 브러시로 변환한다. (스프레드시트의 styles 탭에서 사용)
    //   #rgb  #rgba  #rrggbb  #rrggbbaa  rgb()  rgba()  이름 있는 색(white, transparent ...)
    //   linear-gradient([각도 | to top/right/bottom/left,] 색 [위치%] [위치%], ...)
    //   @ResourceKey : 앱 리소스(ColorDictionary.xaml)에 정의된 브러시를 가리킨다.
    // 지원하지 않는 문법(radial-gradient, 여러 겹의 배경 등)은 FormatException을 던진다.
    public static class BrushParser
    {
        // 버튼의 대략적인 크기. CSS의 각도를 WPF의 상대 좌표로 옮길 때 비율을 맞추는 데 쓴다.
        private const double ButtonWidth = 110;
        private const double ButtonHeight = 30;
        private const double Epsilon = 0.0001;

        public static Brush Parse(string css, Func<string, object> findResource = null)
        {
            string text = (css ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                throw new FormatException("empty value.");
            }
            if (text[0] == '@')
            {
                string key = text[1..].Trim();
                return findResource?.Invoke(key) as Brush
                    ?? throw new FormatException($"resource '{key}' was not found.");
            }
            if (text.StartsWith("linear-gradient(", StringComparison.OrdinalIgnoreCase) && text.EndsWith(')'))
            {
                return ParseLinearGradient(text["linear-gradient(".Length..^1]);
            }
            if (text.Contains("gradient", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException("only linear-gradient is supported (use @ResourceKey for the others).");
            }
            var brush = new SolidColorBrush(ParseColor(text));
            brush.Freeze();
            return brush;
        }

        // 그라데이션이면 첫 색, 단색이면 그 색. 그 외(DrawingBrush 등)는 null.
        public static Color? GetFirstColor(Brush brush)
        {
            return brush switch
            {
                SolidColorBrush solid => solid.Color,
                GradientBrush gradient when gradient.GradientStops.Count > 0 => gradient.GradientStops[0].Color,
                _ => null
            };
        }

        // 꺼져 있을 때 쓰는 글자색: 배경의 색조는 유지하고 밝기를 낮춘다. (앱의 Fg* 색과 비슷한 어두운 톤)
        public static Brush DeriveForeground(Brush background)
        {
            if (GetFirstColor(background) is not Color color)
            {
                return null;
            }
            var (h, s, _) = ToHsl(color);
            var brush = new SolidColorBrush(FromHsl(h, s * 0.7, 0.22));
            brush.Freeze();
            return brush;
        }

        private static Brush ParseLinearGradient(string args)
        {
            var parts = SplitTopLevel(args, ',');
            double angle = 180;
            int first = 0;
            if (parts.Count > 0 && TryParseDirection(parts[0], out double parsed))
            {
                angle = parsed;
                first = 1;
            }
            var stopParts = parts.Skip(first).ToList();
            if (stopParts.Count < 2)
            {
                throw new FormatException("a gradient needs at least two color stops.");
            }

            // (색, 위치) 목록. 위치가 없으면 null
            var stops = new List<(Color Color, double? Pos)>();
            foreach (string part in stopParts)
            {
                var tokens = SplitTopLevel(part, ' ');
                if (tokens.Count == 0)
                {
                    throw new FormatException("empty color stop.");
                }
                Color color = ParseColor(tokens[0]);
                var positions = tokens.Skip(1).Select(ParsePosition).ToList();
                if (positions.Count == 0)
                {
                    stops.Add((color, null));
                }
                else if (positions.Count <= 2)
                {
                    foreach (double pos in positions)
                    {
                        stops.Add((color, pos));
                    }
                }
                else
                {
                    throw new FormatException($"too many positions in '{part.Trim()}'.");
                }
            }

            double[] offsets = ResolvePositions(stops.Select(s => s.Pos).ToList());
            var collection = new GradientStopCollection();
            for (int i = 0; i < stops.Count; i++)
            {
                collection.Add(new GradientStop(stops[i].Color, offsets[i]));
            }

            // CSS gradient line: 중심을 지나고, 길이는 |W sin| + |H cos|
            double rad = angle * Math.PI / 180;
            double sin = Math.Sin(rad), cos = Math.Cos(rad);
            double length = Math.Abs(ButtonWidth * sin) + Math.Abs(ButtonHeight * cos);
            double dx = sin * length / 2 / ButtonWidth;
            double dy = -cos * length / 2 / ButtonHeight;
            var brush = new LinearGradientBrush(collection,
                                                new System.Windows.Point(0.5 - dx, 0.5 - dy),
                                                new System.Windows.Point(0.5 + dx, 0.5 + dy));
            brush.Freeze();
            return brush;
        }

        // CSS 규칙대로 위치를 정리한다: 앞의 위치보다 작으면 앞의 값으로 올리고, 비어 있는 위치는 이웃 사이를 균등 분할한다.
        private static double[] ResolvePositions(List<double?> raw)
        {
            int n = raw.Count;
            var pos = raw.ToArray();
            pos[0] ??= 0;
            pos[n - 1] ??= 1;
            double max = double.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                if (pos[i] is double p)
                {
                    max = Math.Max(max, p);
                    pos[i] = max;
                }
            }
            for (int i = 0; i < n; i++)
            {
                if (pos[i] is not null)
                {
                    continue;
                }
                int prev = i - 1;
                int next = i;
                while (pos[next] is null)
                {
                    next++;
                }
                for (int k = i; k < next; k++)
                {
                    pos[k] = pos[prev] + (pos[next] - pos[prev]) * (k - prev) / (next - prev);
                }
            }

            var result = new double[n];
            for (int i = 0; i < n; i++)
            {
                double value = Math.Min(Math.Max(pos[i]!.Value, 0), 1);
                // 같은 위치에 두 색이 있으면(줄무늬) 뒤의 색을 아주 조금 뒤로 민다.
                result[i] = i > 0 && value <= result[i - 1] ? result[i - 1] + Epsilon : value;
            }
            return result;
        }

        private static bool TryParseDirection(string text, out double degrees)
        {
            text = text.Trim().ToLowerInvariant();
            degrees = 0;
            switch (text)
            {
                case "to top": degrees = 0; return true;
                case "to right": degrees = 90; return true;
                case "to bottom": degrees = 180; return true;
                case "to left": degrees = 270; return true;
            }
            if (text.StartsWith("to "))
            {
                throw new FormatException($"direction '{text}' is not supported (use an angle).");
            }
            var m = Regex.Match(text, @"^(-?\d*\.?\d+)(deg|grad|rad|turn)$");
            if (!m.Success)
            {
                return false;
            }
            double value = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            degrees = m.Groups[2].Value switch
            {
                "grad" => value * 0.9,
                "rad" => value * 180 / Math.PI,
                "turn" => value * 360,
                _ => value
            };
            return true;
        }

        // 퍼센트만 지원한다. 0(0px 포함)은 허용한다.
        private static double ParsePosition(string token)
        {
            var m = Regex.Match(token.Trim(), @"^(-?\d*\.?\d+)(%|px)?$");
            if (!m.Success)
            {
                throw new FormatException($"invalid color stop position '{token}'.");
            }
            double value = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            if (m.Groups[2].Value == "%")
            {
                return value / 100;
            }
            if (value == 0)
            {
                return 0;
            }
            throw new FormatException($"position '{token}' must be a percentage.");
        }

        private static Color ParseColor(string text)
        {
            text = text.Trim();
            if (text.StartsWith('#'))
            {
                return ParseHex(text[1..]);
            }
            var m = Regex.Match(text, @"^(rgba?)\((.*)\)$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var values = m.Groups[2].Value.Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (values.Length < 3 || values.Length > 4)
                {
                    throw new FormatException($"invalid color '{text}'.");
                }
                byte alpha = values.Length == 4 ? ToByte(values[3], 1) : (byte)255;
                return Color.FromArgb(alpha, ToByte(values[0], 255), ToByte(values[1], 255), ToByte(values[2], 255));
            }
            if (Regex.IsMatch(text, @"^[A-Za-z]+$")
                && ColorConverter.ConvertFromString(text) is Color named)
            {
                return named;
            }
            throw new FormatException($"invalid color '{text}'.");
        }

        // CSS 순서: #rgb, #rgba, #rrggbb, #rrggbbaa (WPF의 #aarrggbb와 다르다)
        private static Color ParseHex(string hex)
        {
            if (!Regex.IsMatch(hex, "^[0-9a-fA-F]+$") || hex.Length is not (3 or 4 or 6 or 8))
            {
                throw new FormatException($"invalid hex color '#{hex}'.");
            }
            if (hex.Length <= 4)
            {
                hex = string.Concat(hex.Select(c => new string(c, 2)));
            }
            byte Part(int i) => byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return Color.FromArgb(hex.Length == 8 ? Part(3) : (byte)255, Part(0), Part(1), Part(2));
        }

        // 숫자 또는 퍼센트를 0~255로 바꾼다. max는 100%에 해당하는 값이다. (rgb 채널은 255, 알파는 1)
        private static byte ToByte(string value, double max)
        {
            bool percent = value.EndsWith('%');
            if (!double.TryParse(percent ? value[..^1] : value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                throw new FormatException($"invalid number '{value}'.");
            }
            double ratio = percent ? v / 100 : v / max;
            return (byte)Math.Round(Math.Min(Math.Max(ratio, 0), 1) * 255);
        }

        // 괄호 안의 구분자는 무시하고 최상위 구분자로만 나눈다. (rgb(1, 2, 3) 안의 쉼표 등)
        private static List<string> SplitTopLevel(string text, char separator)
        {
            var result = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == separator && depth == 0)
                {
                    AddPart(result, text[start..i]);
                    start = i + 1;
                }
            }
            AddPart(result, text[start..]);
            return result;
        }

        private static void AddPart(List<string> list, string part)
        {
            part = part.Trim();
            if (part.Length > 0)
            {
                list.Add(part);
            }
        }

        private static (double H, double S, double L) ToHsl(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double l = (max + min) / 2, d = max - min;
            if (d == 0)
            {
                return (0, 0, l);
            }
            double s = d / (1 - Math.Abs(2 * l - 1));
            double h = max == r ? ((g - b) / d + (g < b ? 6 : 0))
                     : max == g ? (b - r) / d + 2
                     : (r - g) / d + 4;
            return (h * 60, s, l);
        }

        private static Color FromHsl(double h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
            double m = l - c / 2;
            (double r, double g, double b) = (int)(h / 60) switch
            {
                0 => (c, x, 0.0),
                1 => (x, c, 0.0),
                2 => (0.0, c, x),
                3 => (0.0, x, c),
                4 => (x, 0.0, c),
                _ => (c, 0.0, x)
            };
            byte B(double v) => (byte)Math.Round((v + m) * 255);
            return Color.FromRgb(B(r), B(g), B(b));
        }
    }
}
