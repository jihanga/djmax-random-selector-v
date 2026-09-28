using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;
using Dmrsv.RandomSelector;

namespace DjmaxRandomSelectorV.Models
{
    public class PlaylistItem : PropertyChangedBase, IEquatable<PlaylistItem>
    {
        private bool _isNext;

        public int PatternId { get; init; }
        public string Title { get; init; }
        public string Composer { get; init; }
        public string Category { get; init; }
        public string Style { get; init; }
        public string ButtonTunes => Style[..2];
        public string Difficulty => Style[2..];
        public string Level { get; init; }

        // 순차 선택기가 다음에 뽑을 항목임을 표시한다.
        public bool IsNext
        {
            get => _isNext;
            set
            {
                if (_isNext != value)
                {
                    _isNext = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        // 플레이리스트 내용만으로 값 비교를 하므로, IsNext가 Distinct()/Contains()에 영향을 주지 않는다.
        public bool Equals(PlaylistItem other)
        {
            return other is not null && PatternId == other.PatternId;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlaylistItem);
        }

        public override int GetHashCode()
        {
            return PatternId;
        }
    }
}
