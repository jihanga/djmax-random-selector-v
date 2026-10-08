using Caliburn.Micro;
using System.Collections.Generic;
using System.Windows.Media;

namespace DjmaxRandomSelectorV.Models
{
    public class ListUpdater : PropertyChangedBase
    {
        private readonly string _name;
        private readonly string _value;
        private readonly ICollection<string> _target;
        private readonly ResolvedCategoryStyle _style;

        public string Name { get => _name; }
        public bool IsValueContained
        {
            get => _target.Contains(_value);
            set
            {
                if (value)
                {
                    _target.Add(_value);
                }
                else
                {
                    _target.Remove(_value);
                }
                NotifyOfPropertyChange();
            }
        }

        // 스프레드시트의 styles 탭에서 받은 색. 값이 있으면 View에 정의된 색보다 우선한다.
        public bool HasStyle => _style?.Background is not null;
        public bool HasStyleForeground => _style?.Foreground is not null;
        public bool HasStyleBorder => _style?.BorderBrush is not null;
        public Brush StyleBackground => _style?.Background;
        public Brush StyleForeground => _style?.Foreground;
        public Brush StyleBorderBrush => _style?.BorderBrush;

        public ListUpdater(string name, string value, ICollection<string> target, ResolvedCategoryStyle style = null)
        {
            _name = name;
            _value = value;
            _target = target;
            _style = style;
        }
    }
}
