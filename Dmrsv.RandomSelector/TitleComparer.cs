using System.Globalization;

namespace Dmrsv.RandomSelector
{
    public class TitleComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (string.Equals(x, y))
            {
                return 0;
            }
            if (y == null)
            {
                return -1;
            }
            if (x == null)
            {
                return 1;
            }
            // Djmax sorts titles with case-insensitive and ignoring the characters below
            x = CleanString(x);
            y = CleanString(y);
            int index = x.Zip(y, (a, b) => a == b).TakeWhile(equals => equals).Count();
            if (index == Math.Min(x.Length, y.Length))
            {
                return x.Length - y.Length;
            }
            // priority order: white-space -> non-alphabetic letter -> special character -> number -> alphabet(Normalize diacritics)
            char a = x[index], b = y[index];
            int priorityA = GetPriority(a);
            int priorityB = GetPriority(b);
            if (priorityA == priorityB)
            {
                if (priorityA == 1)
                {
                    var compareInfo = new CultureInfo("ko-KR").CompareInfo;
                    return compareInfo.Compare(a.ToString(), b.ToString());
                }

                return a.CompareTo(b);
            }
            return priorityA - priorityB;
        }

        // Djmax sorts titles case-insensitively and with some characters ignored or replaced (see SortRules)
        private string CleanString(string text)
        {
            return SortRules.Current.Clean(text);
        }
/*
        private int GetPriority(char ch, int idx)
        {
            if (char.IsWhiteSpace(ch))
            {
                return 0;
            }
            if (char.IsUpper(ch)) // alphabet
            {
                return 5;
            }
            if (char.IsLetter(ch) && !char.IsUpper(ch) && !char.IsLower(ch)) // non-alphabetic letter
            {
               if (idx == 0)
                {
                    bool isKorean = (ch >= 0xAC00 && ch <= 0xD7A3) || (ch >= 0x3131 && ch <= 0x318E);
                    return isKorean ? 2 : 1; 
                }
                else
                {
                 return 6;
                }
            }
            if (char.IsDigit(ch))
            {
                return 4;
            }
            // symbol, punctuation, etc.
            return 3;
        }*/
        private int GetPriority(char ch)
        {
            if (char.IsWhiteSpace(ch)) return 0;

            if (char.IsLetter(ch)) // alphabet
            {
                bool isKorean = (ch >= 0xAC00 && ch <= 0xD7A3) || (ch >= 0x3131 && ch <= 0x318E);
                if (isKorean) return 2; // ko

                bool isEnglish = (ch >= 'A' && ch <= 'Z');
                if (isEnglish) return 5; // en

                // non-alphabetic letter
                return 1;
            }

            if (char.IsDigit(ch)) return 4;

            // symbol, punctuation, etc.
            return 3;
        }
    }
}