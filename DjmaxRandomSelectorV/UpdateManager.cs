using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DjmaxRandomSelectorV
{
    public class UpdateManager
    {
        private const string VersionCheckUrl = "https://raw.githubusercontent.com/pali-fly/djmax-random-selector-v/main/DjmaxRandomSelectorV/Version3.txt";
        private const string AllTrackDownloadUrl = "https://v-archive.net/db/v2/songs.json";
        private const string AppdataDownloadUrl = "https://raw.githubusercontent.com/pali-fly/djmax-random-selector-v/main/DjmaxRandomSelectorV/DMRSV3_Data/appdata.json";
        private const string DlcListDownloadUrl = "https://v-archive.net/db/dlcs.json";
        private const string AllTrackFilePath = @"DMRSV3_Data\AllTrackList.json";
        private const string AppdataFilePath = @"DMRSV3_Data\appdata.json";
        public const string DlcListFilePath = @"DMRSV3_Data\DlcList.json";

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
            // update appdata
            if (!File.Exists(AllTrackFilePath)
                || versions[1].CompareTo(_container.AppdataVersion) > 0)
            {
                Debug.WriteLine("appdata update start");
                tasks.Add(DownloadAppdataAsync());
            }
            // DLC 목록 갱신 (출처 API에 버전 정보가 없어 항상 새로 받는다)
            Debug.WriteLine("dlc list update start");
            tasks.Add(DownloadDlcListAsync());

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
