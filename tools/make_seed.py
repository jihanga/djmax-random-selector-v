"""Generate Google Sheets seed CSVs from DMRSV3_Data/appdata.json and the app's hardcoded sort rules."""
import csv, json, sys
from pathlib import Path

root = Path(__file__).resolve().parent.parent
src = root / "DjmaxRandomSelectorV" / "DMRSV3_Data" / "appdata.json"
# usage: make_seed.py [dlcs.json path] [output dir]
dlcs = Path(sys.argv[1]) if len(sys.argv) > 1 else None
out = Path(sys.argv[2]) if len(sys.argv) > 2 else root / "tools" / "sheet-seed"
out.mkdir(parents=True, exist_ok=True)
data = json.loads(src.read_text(encoding="utf-8-sig"))

def write(name, header, rows):
    with open(out / name, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f)
        w.writerow(header)
        w.writerows(rows)

cats = data["categories"]
if dlcs:
    # Order by release (dlcs.json order); regular (type 0) block first, then collab/PLI (type 1-3) block.
    # Names stay as in appdata.json because the UI color triggers match on them.
    rank = {x["dlcCode"]: i for i, x in enumerate(json.loads(dlcs.read_text(encoding="utf-8-sig")))}
    cats = sorted(cats, key=lambda c: (c["type"] != 0, rank.get(c["id"], len(rank))))
write("categories.csv", ["name", "id", "steamId", "type"],
      [[c["name"], c["id"], c["steamId"] or "", c["type"]] for c in cats])

write("linkDisc.csv", ["id", "requiredDlc"],
      [[d["id"], "|".join("+".join(g) for g in d["requiredDlc"])] for d in data["linkDisc"]])

write("settings.csv", ["key", "value"],
      [["basicCategories", ",".join(data["basicCategories"])],
       ["categoryType", ",".join(data["categoryType"])]])

# TitleComparer.CleanString + Locator tie-break (ids 267, 170 in descending order)
write("sortRules.csv", ["kind", "from", "to", "trackId", "priority"],
      [["strip", "'", "", "", ""], ["strip", "-", "", "", ""],
       ["replace", "Ö", "O", "", ""], ["replace", "Ä", "A", "", ""], ["replace", "Ü", "U", "", ""],
       ["replace", "È", "E", "", ""], ["replace", "É", "E", "", ""],
       ["replace", "脳", "腦", "", ""], ["replace", "撃", "擊", "", ""],
       ["tiebreak", "", "", 267, 2], ["tiebreak", "", "", 170, 1]])

# Button colors. Only DLCs that have no color defined in BasicFilterView.xaml are listed
# (values taken from the v-archive.net stylesheet: .dlc_logo--<code>). A row here wins over the app's own color.
# bg/fg/border accept: #hex, rgb(), css linear-gradient(...), or @ResourceKey (ColorDictionary.xaml). fg is derived from bg when empty.
write("styles.csv", ["id", "bg", "fg", "border"],
      [["ARC", "#ffffff", "", ""],
       ["PLI4", "#2268f7", "", ""],
       ["DNF", "linear-gradient(#ff763e 10%, #ffcf55 90%)", "", ""]])
print("written to", out)
