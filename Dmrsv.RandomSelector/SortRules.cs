namespace Dmrsv.RandomSelector
{
    // 게임이 곡 제목을 정렬하는 방식 중 코드로 일반화할 수 없는 예외 규칙.
    // 기본값은 기존에 하드코딩되어 있던 규칙과 같고, 스프레드시트의 sortRules 탭으로 대체할 수 있다.
    public class SortRules
    {
        // 프로그램 전체에서 쓰는 규칙. 시작할 때 한 번 교체한다.
        public static SortRules Current { get; set; } = CreateDefault();

        // 정렬 전에 제목에서 제거하는 문자열
        public List<string> Strip { get; set; } = new();
        // 대문자로 바꾼 뒤 적용하는 치환 (From은 대문자여야 한다)
        public List<SortReplacement> Replacements { get; set; } = new();
        // 제목이 같은 곡의 순서. 값이 큰 곡이 앞에 온다. (key: track id)
        public Dictionary<int, int> Tiebreaks { get; set; } = new();

        public string Clean(string text)
        {
            foreach (string s in Strip)
            {
                text = text.Replace(s, string.Empty);
            }
            text = text.ToUpper();
            foreach (var r in Replacements)
            {
                text = text.Replace(r.From, r.To);
            }
            return text;
        }

        public int GetTiebreak(int trackId)
        {
            return Tiebreaks.TryGetValue(trackId, out int priority) ? priority : 0;
        }

        public static SortRules CreateDefault()
        {
            return new SortRules
            {
                Strip = new List<string> { "'", "-" },
                Replacements = new List<SortReplacement>
                {
                    // Djmax treats umlauts as their base alphabets for sorting
                    new("Ö", "O"), new("Ä", "A"), new("Ü", "U"), new("È", "E"), new("É", "E"),
                    // Djmax sorts Chinese characters based on standard Korean Hanja
                    new("脳", "腦"), new("撃", "擊"),
                },
                Tiebreaks = new Dictionary<int, int> { [267] = 2, [170] = 1 }
            };
        }
    }

    public record SortReplacement(string From, string To);
}
