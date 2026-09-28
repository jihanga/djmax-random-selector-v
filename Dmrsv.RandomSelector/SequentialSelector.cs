namespace Dmrsv.RandomSelector
{
    // 무작위 선택 대신 위에서 아래 순서대로 패턴을 선택한다.
    // 다음 Select() 호출에서 어떤 패턴이 반환될지를 나타내는 내부 포인터를 유지한다.
    public class SequentialSelector : ISelector
    {
        private int _currentIndex;

        // 다음 Select() 호출에서 반환될 패턴. UpdateNextPattern을 통해 항상 동기화된다.
        public Pattern? NextPattern { get; private set; }

        public Pattern? Select(IList<Pattern> patternList)
        {
            if (patternList is null || !patternList.Any())
            {
                NextPattern = null;
                return null;
            }
            NormalizeIndex(patternList.Count);
            Pattern selected = patternList[_currentIndex];
            _currentIndex = (_currentIndex + 1) % patternList.Count;
            UpdateNextPattern(patternList);
            return selected;
        }

        // patternId와 일치하는 패턴으로 포인터를 직접 이동시킨다. (예: 사용자가 플레이리스트 항목을 더블클릭한 경우)
        // 현재 후보 목록에 해당 패턴이 없으면 포인터를 그대로 유지하고 false를 반환한다.
        public bool SetPointer(IList<Pattern> patternList, int patternId)
        {
            int index = IndexOf(patternList, patternId);
            if (index < 0)
            {
                return false;
            }
            _currentIndex = index;
            UpdateNextPattern(patternList);
            return true;
        }

        // 플레이리스트가 수정되어도 포인터가 유지하던 패턴이 남아있다면 해당 패턴을 유지한다.
        // 남아있지 않으면 포인터를 목록의 처음으로 되돌린다.
        public void SyncPointer(IList<Pattern> patternList, int? previousPatternId)
        {
            int index = previousPatternId.HasValue ? IndexOf(patternList, previousPatternId.Value) : -1;
            _currentIndex = index >= 0 ? index : 0;
            UpdateNextPattern(patternList);
        }

        public void Reset(IList<Pattern> patternList)
        {
            _currentIndex = 0;
            UpdateNextPattern(patternList);
        }

        private void UpdateNextPattern(IList<Pattern> patternList)
        {
            NormalizeIndex(patternList?.Count ?? 0);
            NextPattern = (patternList is not null && patternList.Any()) ? patternList[_currentIndex] : null;
        }

        private void NormalizeIndex(int count)
        {
            if (count <= 0)
            {
                _currentIndex = 0;
                return;
            }
            if (_currentIndex >= count)
            {
                _currentIndex %= count;
            }
        }

        private static int IndexOf(IList<Pattern> patternList, int patternId)
        {
            if (patternList is null)
            {
                return -1;
            }
            for (int i = 0; i < patternList.Count; i++)
            {
                if (patternList[i].PatternId == patternId)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
