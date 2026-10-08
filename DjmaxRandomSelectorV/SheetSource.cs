using System.Collections.Generic;

namespace DjmaxRandomSelectorV
{
    // DMRSV3_Data\sources.json
    // 파일이 없거나 Sheet가 비어 있으면 기존처럼 GitHub의 appdata.json을 내려받는다.
    public class SourcesConfig
    {
        public const string FilePath = @"DMRSV3_Data\sources.json";

        public SheetSource Sheet { get; set; }
    }

    // "웹에 게시"된 구글 스프레드시트. 탭마다 gid가 필요하다.
    public class SheetSource
    {
        public const string CategoriesTab = "categories";
        public const string LinkDiscTab = "linkDisc";
        public const string SettingsTab = "settings";
        public const string SortRulesTab = "sortRules";
        public const string StylesTab = "styles";

        // https://docs.google.com/spreadsheets/d/e/2PACX-.../pub
        public string BaseUrl { get; set; }
        public Dictionary<string, long> Tabs { get; set; } = new();

        public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl)
                                    && Tabs.ContainsKey(CategoriesTab)
                                    && Tabs.ContainsKey(SettingsTab);

        public string GetCsvUrl(string tab) => $"{BaseUrl.TrimEnd('/', '?')}?gid={Tabs[tab]}&single=true&output=csv";
    }
}
