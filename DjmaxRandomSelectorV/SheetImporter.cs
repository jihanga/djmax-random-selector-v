using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using DjmaxRandomSelectorV.Models;
using Dmrsv.RandomSelector;

namespace DjmaxRandomSelectorV
{
    // 스프레드시트의 CSV 탭들을 appdata.json과 같은 구조(Dmrsv3AppData)로 변환한다.
    public class SheetImporter
    {
        private const int MaxCategoryType = 3;

        public List<string> Warnings { get; } = new();

        public Dmrsv3AppData Build(string categoriesCsv, string linkDiscCsv, string settingsCsv)
        {
            var categories = ParseCategories(categoriesCsv);
            if (categories.Length == 0)
            {
                throw new InvalidDataException("categories sheet has no valid rows.");
            }

            var settings = ReadRows(settingsCsv, "key", "value")
                .Where(r => !string.IsNullOrWhiteSpace(r["key"]))
                .GroupBy(r => r["key"].Trim())
                .ToDictionary(g => g.Key, g => g.Last()["value"]);

            string[] basicCategories = SplitList(settings.GetValueOrDefault("basicCategories"));
            if (basicCategories.Length == 0)
            {
                throw new InvalidDataException("settings sheet has no basicCategories.");
            }

            return new Dmrsv3AppData
            {
                CategoryType = SplitList(settings.GetValueOrDefault("categoryType")),
                BasicCategories = basicCategories,
                Categories = categories,
                LinkDisc = linkDiscCsv is null ? Array.Empty<LinkDiscItem>() : ParseLinkDisc(linkDiscCsv)
            };
        }

        private Category[] ParseCategories(string csv)
        {
            var result = new List<Category>();
            var knownIds = new HashSet<string>();
            int line = 1;
            foreach (var row in ReadRows(csv, "name", "id", "steamId", "type"))
            {
                line++;
                string name = row["name"].Trim();
                string id = row["id"].Trim();
                if (name.Length == 0 || id.Length == 0)
                {
                    Warn($"categories line {line}: name or id is empty.");
                    continue;
                }
                if (!int.TryParse(row["type"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int type)
                    || type < 0 || type > MaxCategoryType)
                {
                    Warn($"categories line {line} ({id}): invalid type '{row["type"]}'.");
                    continue;
                }
                if (!knownIds.Add(id))
                {
                    Warn($"categories line {line}: duplicated id '{id}'.");
                    continue;
                }
                string steamId = row["steamId"].Trim();
                result.Add(new Category(name, id, steamId.Length == 0 ? null : steamId, type));
            }
            return result.ToArray();
        }

        private LinkDiscItem[] ParseLinkDisc(string csv)
        {
            var result = new List<LinkDiscItem>();
            int line = 1;
            foreach (var row in ReadRows(csv, "id", "requiredDlc"))
            {
                line++;
                if (!int.TryParse(row["id"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                {
                    Warn($"linkDisc line {line}: invalid id '{row["id"]}'.");
                    continue;
                }
                // "BS+CE|BS+T1" => [["BS","CE"],["BS","T1"]] (OR of AND)
                var required = row["requiredDlc"].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(group => group.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Where(group => group.Length > 0)
                    .ToArray();
                if (required.Length == 0)
                {
                    Warn($"linkDisc line {line} ({id}): requiredDlc is empty.");
                    continue;
                }
                result.Add(new LinkDiscItem { Id = id, RequiredDlc = required });
            }
            return result.ToArray();
        }

        public SortRules BuildSortRules(string csv)
        {
            var rules = new SortRules();
            int line = 1;
            foreach (var row in ReadRows(csv, "kind", "from", "to", "trackId", "priority"))
            {
                line++;
                string kind = row["kind"].Trim().ToLowerInvariant();
                // from/to는 공백도 의미가 있을 수 있어 자르지 않는다. 대문자로 바꾼 뒤 비교하므로 대문자로 맞춘다.
                string from = row["from"].ToUpper();
                string to = row["to"].ToUpper();
                switch (kind)
                {
                    case "strip":
                        if (from.Length == 0) { Warn($"sortRules line {line}: strip needs 'from'."); break; }
                        rules.Strip.Add(from);
                        break;
                    case "replace":
                        if (from.Length == 0) { Warn($"sortRules line {line}: replace needs 'from'."); break; }
                        rules.Replacements.Add(new SortReplacement(from, to));
                        break;
                    case "tiebreak":
                        if (!int.TryParse(row["trackId"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int trackId)
                            || !int.TryParse(row["priority"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int priority))
                        {
                            Warn($"sortRules line {line}: tiebreak needs numeric trackId and priority.");
                            break;
                        }
                        rules.Tiebreaks[trackId] = priority;
                        break;
                    default:
                        Warn($"sortRules line {line}: unknown kind '{row["kind"]}'.");
                        break;
                }
            }
            return rules;
        }

        public List<CategoryStyle> BuildStyles(string csv)
        {
            var result = new Dictionary<string, CategoryStyle>();
            int line = 1;
            foreach (var row in ReadRows(csv, "id", "bg", "fg", "border"))
            {
                line++;
                string id = row["id"].Trim();
                if (id.Length == 0)
                {
                    Warn($"styles line {line}: id is empty.");
                    continue;
                }
                if (result.ContainsKey(id))
                {
                    Warn($"styles line {line}: duplicated id '{id}' (the last one is used).");
                }
                result[id] = new CategoryStyle(id, row["bg"].Trim(), row["fg"].Trim(), row["border"].Trim());
            }
            return result.Values.ToList();
        }

        // 헤더 이름으로 열을 찾는다(대소문자 무시). 필요한 열이 없으면 예외.
        private static List<Dictionary<string, string>> ReadRows(string csv, params string[] columns)
        {
            using var reader = new StringReader(csv ?? string.Empty);
            using var parser = new CsvReader(reader, CultureInfo.InvariantCulture);
            if (!parser.Read() || !parser.ReadHeader())
            {
                throw new InvalidDataException("CSV has no header.");
            }
            var header = parser.HeaderRecord.Select(h => h.Trim()).ToArray();
            var missing = columns.Where(c => !header.Contains(c, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidDataException($"CSV is missing column(s): {string.Join(", ", missing)}.");
            }

            var rows = new List<Dictionary<string, string>>();
            while (parser.Read())
            {
                var row = new Dictionary<string, string>();
                foreach (string column in columns)
                {
                    int index = Array.FindIndex(header, h => string.Equals(h, column, StringComparison.OrdinalIgnoreCase));
                    row[column] = parser.GetField(index) ?? string.Empty;
                }
                if (row.Values.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }
                rows.Add(row);
            }
            return rows;
        }

        private static string[] SplitList(string value)
        {
            return (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private void Warn(string message)
        {
            Warnings.Add(message);
            System.Diagnostics.Debug.WriteLine("[sheet] " + message);
        }
    }
}
