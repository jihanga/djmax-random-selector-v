using DjmaxRandomSelectorV.Models;
using System.Collections.Generic;
using System.Linq;

namespace DjmaxRandomSelectorV
{
    public class CategoryContainer
    {
        // v-archive의 dlcs.json에는 알고 있지만 appdata.json에는 아직 등록되지 않은 DLC에 사용한다.
        // (steamId/type/linkDisc 등의 메타데이터를 제공받지 못한다.)
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
