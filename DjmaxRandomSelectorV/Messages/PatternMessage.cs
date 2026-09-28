using Dmrsv.RandomSelector;

namespace DjmaxRandomSelectorV.Messages
{
    public record PatternMessage(Pattern Item);

    // 순차 선택기가 다음에 뽑을 패턴을 통지한다. (순차 모드가 아니면 null)
    public record NextPatternMessage(Pattern Item);
}

