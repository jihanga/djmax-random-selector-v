using DjmaxRandomSelectorV.Models;
using System.Collections.Generic;
using System.Linq;

namespace DjmaxRandomSelectorV
{
    public class CategoryContainer
    {
        // Used for DLCs that are known from v-archive's dlcs.json but not yet
        // registered in appdata.json (steamId/type/linkDisc metadata unavailable).
        public const int UnclassifiedType = 99;

        private List<Category> _categories;

        public List<Category> GetCategories()
        {
            return _categories.ConvertAll(x => x);
        }

        public void SetCategories(Dmrsv3AppData appData, IEnumerable<VArchiveDlcItem> dlcList = null)
        {
            //var categories = appData.Categories.Where(cat => cat.Type != 3);
            //var plis = appData.PliCategories
                              //.SelectMany(pli => pli.Minors, (pli, m) => new Category(m.Name, $"{pli.Major}:{m.Name}", null, 3));
            _categories = new List<Category>(appData.Categories);

            if (dlcList is null)
            {
                return;
            }

            var knownIds = new HashSet<string>(_categories.Select(c => c.Id));
            var newCategories = dlcList
                .Where(dlc => !knownIds.Contains(dlc.DlcCode))
                .Select(dlc => new Category(dlc.DlcName, dlc.DlcCode, null, UnclassifiedType));
            _categories.AddRange(newCategories);
        }
    }
}
