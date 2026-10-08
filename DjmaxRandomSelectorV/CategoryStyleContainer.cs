using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace DjmaxRandomSelectorV
{
    // 스프레드시트의 styles 탭 한 행
    public record CategoryStyle(string Id, string Bg, string Fg, string Border);

    // 화면에 적용할 수 있게 해석된 스타일. 값이 없으면 null.
    public record ResolvedCategoryStyle(Brush Background, Brush Foreground, Brush BorderBrush);

    // 카테고리 id별로 시트에서 받은 버튼 색을 보관한다. 시트에 값이 있으면 앱에 정의된 색보다 우선한다.
    public class CategoryStyleContainer
    {
        private readonly Dictionary<string, CategoryStyle> _styles = new();
        private readonly Dictionary<string, ResolvedCategoryStyle> _resolved = new();

        public void SetStyles(IEnumerable<CategoryStyle> styles)
        {
            _styles.Clear();
            _resolved.Clear();
            foreach (var style in styles)
            {
                if (!string.IsNullOrWhiteSpace(style?.Id))
                {
                    _styles[style.Id.Trim()] = style;
                }
            }
        }

        // 스타일이 없거나 배경을 해석할 수 없으면 null. (앱에 정의된 색을 그대로 쓴다.)
        public ResolvedCategoryStyle Resolve(string id)
        {
            if (id is null || !_styles.TryGetValue(id, out var style))
            {
                return null;
            }
            if (!_resolved.TryGetValue(id, out var resolved))
            {
                resolved = ResolveCore(style);
                _resolved[id] = resolved;
            }
            return resolved;
        }

        private static ResolvedCategoryStyle ResolveCore(CategoryStyle style)
        {
            Brush background = TryParse(style.Id, "bg", style.Bg);
            if (background is null)
            {
                return null;
            }
            Brush foreground = TryParse(style.Id, "fg", style.Fg);
            if (foreground is null && string.IsNullOrWhiteSpace(style.Fg))
            {
                // @BgXxx 이면 짝이 되는 @FgXxx가 있는지 보고, 없으면 배경에서 계산한다.
                string bg = style.Bg.Trim();
                if (bg.StartsWith("@Bg", StringComparison.Ordinal))
                {
                    foreground = TryParse(style.Id, "fg", "@Fg" + bg[3..], quiet: true);
                }
                foreground ??= BrushParser.DeriveForeground(background);
            }
            Brush border = TryParse(style.Id, "border", style.Border);
            return new ResolvedCategoryStyle(background, foreground, border);
        }

        private static Brush TryParse(string id, string column, string value, bool quiet = false)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            try
            {
                return BrushParser.Parse(value, key => Application.Current?.TryFindResource(key));
            }
            catch (FormatException e)
            {
                if (!quiet)
                {
                    Debug.WriteLine($"[sheet] styles '{id}' {column}: {e.Message}");
                }
                return null;
            }
        }
    }
}
