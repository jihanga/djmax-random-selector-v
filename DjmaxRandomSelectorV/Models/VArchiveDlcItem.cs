namespace DjmaxRandomSelectorV.Models
{
    // Response item of https://v-archive.net/db/dlcs.json
    // Used to supplement newly released DLCs that are not yet registered in appdata.json.
    public record VArchiveDlcItem(string DlcCode, string DlcName, string Ymdt);
}
