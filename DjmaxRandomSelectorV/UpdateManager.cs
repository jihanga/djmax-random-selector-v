using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Dmrsv.RandomSelector;

namespace DjmaxRandomSelectorV
{
    public class UpdateManager
    {
        private const string VersionCheckUrl = "https://raw.githubusercontent.com/jihanga/djmax-random-selector-v/main/DjmaxRandomSelectorV/Version3.txt";
        private const string AllTrackDownloadUrl = "https://v-archive.net/db/v2/songs.json";
        private const string AppdataDownloadUrl = "https://raw.githubusercontent.com/jihanga/djmax-random-selector-v/main/DjmaxRandomSelectorV/DMRSV3_Data/appdata.json";
        private const string DlcListDownloadUrl = "https://v-archive.net/db/dlcs.json";
        private const string AllTrackFilePath = @"DMRSV3_Data\AllTrackList.json";
        private const string AppdataFilePath = @"DMRSV3_Data\appdata.json";
        public const string DlcListFilePath = @"DMRSV3_Data\DlcList.json";
        public const string SortRulesFilePath = @"DMRSV3_Data\SortRules.json";
        public const string SheetVersionLabel = "sheet";

        private readonly VersionContainer _container;
        private readonly IFileManager _fileManager;

        public UpdateManager(Dmrsv3Configuration config, VersionContainer container, IFileManager fileManager)
        {
            _container = container;
            _fileManager = fileManager;
            Version assemblyVersion = Assembly.GetEntryAssembly().GetName().Version;
            _container.CurrentAppVersion = _container.LatestAppVersion = assemblyVersion;
            _container.AllTrackVersion = config.AllTrackVersion;
            _container.AppdataVersion = config.AppdataVersion;
        }

        public async Task UpdateAsync()
        {
            string[] versions; // [ app version, appdata version, notice header, notice body ]
            try
            {
                string result = await _fileManager.RequestAsync(VersionCheckUrl);
                versions = result.Split('\n');
            }
            catch
            {
                throw new Exception("Failed to check update.");
            }
            _container.LatestAppVersion = new Version(versions[0]);

            var tasks = new List<Task<int>>();
            // update all track
            long now = long.Parse(DateTime.Now.ToString("yyMMddHHmm"));
            long past = _container.AllTrackVersion;
            if (now > past || !File.Exists(AllTrackFilePath))
            {
                Debug.WriteLine("all track update start");
                tasks.Add(DownloadAllTrackAsync());
            }
            // DLC 목록 갱신 (출처 API에 버전 정보가 없어 항상 새로 받는다)
            Debug.WriteLine("dlc list update start");
            tasks.Add(DownloadDlcListAsync());
            // update appdata
            SheetSource sheet = LoadSheetSource();
            if (sheet is not null)
            {
                // 시트에는 버전 정보가 없어 항상 새로 받는다.
                Debug.WriteLine("appdata update start (sheet)");
                tasks.Add(DownloadAppdataFromSheetAsync(sheet));
                if (sheet.Tabs.ContainsKey(SheetSource.SortRulesTab))
                {
                    tasks.Add(DownloadSortRulesAsync(sheet));
                }
            }
            else if (!File.Exists(AllTrackFilePath)
                || versions[1].CompareTo(_container.AppdataVersion) > 0)
            {
                Debug.WriteLine("appdata update start");
                tasks.Add(DownloadAppdataAsync());
            }

            while (tasks.Count > 0)
            {
                var finishedTask = await Task.WhenAny(tasks);
                int result = await finishedTask;
                switch (result)
                {
                    case 0:
                        _container.AllTrackVersion = now;
                        break;
                    case 1:
                        _container.AppdataVersion = versions[1];
                        break;
                    case 3:
                        _container.AppdataVersion = SheetVersionLabel;
                        break;
                }
                tasks.Remove(finishedTask);
            }
        }

        private async Task<int> DownloadAllTrackAsync()
        {
            try
            {
                string result = await _fileManager.RequestAsync(AllTrackDownloadUrl);
                _fileManager.Write(result, AllTrackFilePath);
            }
            catch
            {
                return -1;
            }
            return 0;
        }

        private async Task<int> DownloadAppdataAsync()
        {
            try
            {
                string result = await _fileManager.RequestAsync(AppdataDownloadUrl);
                _fileManager.Write(result, AppdataFilePath);
            }
            catch
            {
                return -1;
            }
            return 1;
        }

        private SheetSource LoadSheetSource()
        {
            try
            {
                var sheet = _fileManager.Import<SourcesConfig>(SourcesConfig.FilePath).Sheet;
                return sheet is { IsConfigured: true } ? sheet : null;
            }
            catch
            {
                // sources.json이 없거나 읽을 수 없으면 기존 방식(GitHub appdata.json)을 쓴다.
                return null;
            }
        }

        private async Task<int> DownloadAppdataFromSheetAsync(SheetSource sheet)
        {
            try
            {
                string categories = await _fileManager.RequestAsync(sheet.GetCsvUrl(SheetSource.CategoriesTab));
                string settings = await _fileManager.RequestAsync(sheet.GetCsvUrl(SheetSource.SettingsTab));
                string linkDisc = sheet.Tabs.ContainsKey(SheetSource.LinkDiscTab)
                    ? await _fileManager.RequestAsync(sheet.GetCsvUrl(SheetSource.LinkDiscTab))
                    : null;

                var importer = new SheetImporter();
                Dmrsv3AppData appdata = importer.Build(categories, linkDisc, settings);
                _fileManager.Export(appdata, AppdataFilePath);
                return 3;
            }
            catch (Exception e)
            {
                Debug.WriteLine("[sheet] failed: " + e.Message);
                // 마지막으로 받은 appdata.json이 남아 있으면 그대로 쓴다. 없으면 기존 방식으로 내려받는다.
                return File.Exists(AppdataFilePath) ? -1 : await DownloadAppdataAsync();
            }
        }

        private async Task<int> DownloadSortRulesAsync(SheetSource sheet)
        {
            try
            {
                string csv = await _fileManager.RequestAsync(sheet.GetCsvUrl(SheetSource.SortRulesTab));
                SortRules rules = new SheetImporter().BuildSortRules(csv);
                _fileManager.Export(rules, SortRulesFilePath);
                return 4;
            }
            catch (Exception e)
            {
                // 오류가 발생해도 마지막으로 받은 규칙이나 기본 규칙을 쓰므로 처리를 중단하지 않는다.
                Debug.WriteLine("[sheet] sort rules failed: " + e.Message);
                return -1;
            }
        }

        private async Task<int> DownloadDlcListAsync()
        {
            try
            {
                string result = await _fileManager.RequestAsync(DlcListDownloadUrl);
                _fileManager.Write(result, DlcListFilePath);
            }
            catch
            {
                // 오류가 발생해도 Categories는 appdata.json만 사용하므로 처리를 중단하지 않는다.
                return -1;
            }
            return 2;
        }
    }
}
