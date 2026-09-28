namespace DjmaxRandomSelectorV.Models
{
    // https://v-archive.net/db/dlcs.json 응답 항목
    // appdata.json에 아직 등록되지 않은 새로 출시된 DLC를 보충하기 위해 사용한다.
    public record VArchiveDlcItem(string DlcCode, string DlcName, string Ymdt);
}
