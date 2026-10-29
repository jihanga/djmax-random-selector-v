using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Caliburn.Micro;
using DjmaxRandomSelectorV.Messages;
using DjmaxRandomSelectorV.Models;
using Dmrsv.RandomSelector.Enums;

namespace DjmaxRandomSelectorV.ViewModels
{
    public class VArchiveWizardViewModel : Screen
    {
        private const string FloorDbUrl = "https://v-archive.net/db/floors.json";

        private readonly IEventAggregator _eventAggregator;
        private readonly TrackDB _trackDB;

        private string _currentButton;
        private string _currentBoard;
        public string Nickname { get; set; }
        public string SelectedButton { get; set; } = "4";
        public string SelectedBoard { get; set; } = "SC";
        public string CurrentButton
        {
            get => _currentButton;
            set
            {
                _currentButton = value;
                NotifyOfPropertyChange();
            }
        }
        public string CurrentBoard
        {
            get => _currentBoard;
            set
            {
                _currentBoard = value;
                NotifyOfPropertyChange();
            }
        }

        public BindableCollection<VArchivePatternItem> PatternItems { get; }

        public VArchiveWizardViewModel(IEventAggregator eventAggregator, TrackDB trackDB)
        {
            _eventAggregator = eventAggregator;
            _trackDB = trackDB;
            PatternItems = new BindableCollection<VArchivePatternItem>();
        }

        // 요청 중에 다시 누르면 무시한다. 버튼의 IsEnabled는 XAML 트리거가 관리하므로 Can 가드를 쓰지 않는다.
        private bool _isRequesting;

        public async Task RequestBoard()
        {
            if (_isRequesting)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Nickname))
            {
                MessageBox.Show("Nickname is empty.", "Invalid Request", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedButton))
            {
                MessageBox.Show("Button is empty.", "Invalid Request", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(SelectedButton, out int selectedButtonNum)
                || !new[] { 4, 5, 6, 8 }.Contains(selectedButtonNum))
            {
                MessageBox.Show("Invalid button value.", "Invalid Request", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_trackDB?.AllTrack is null)
            {
                MessageBox.Show("Track database is not ready.", "Request Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 응답을 기다리는 동안 입력값이 바뀔 수 있으므로 요청 시점의 값을 고정한다.
            string nickname = Nickname;
            string selectedBoard = SelectedBoard;
            _isRequesting = true;
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var floorNameByFloor = await LoadFloorNameByFloor(client);

                string url = $"https://v-archive.net/api/v2/archive/{Uri.EscapeDataString(nickname)}/button/{selectedButtonNum}";
                HttpResponseMessage response = await client.GetAsync(url);

                ArchiveV2Response root = await response.Content.ReadFromJsonAsync<ArchiveV2Response>();
                if (root is null)
                {
                    MessageBox.Show("Invalid response from server.", "Request Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (root.Success != true)
                {
                    MessageBox.Show(root.Message ?? $"Request failed ({(int)response.StatusCode}).", "Request Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                PatternItems.Clear();
                CurrentButton = selectedButtonNum + "B";
                CurrentBoard = string.IsNullOrWhiteSpace(selectedBoard) ? "All" : selectedBoard;

                var records = root.Records ?? new List<ArchiveV2Record>();
                var recordDict = records
                    .GroupBy(r => (r.Title, r.Pattern))
                    .ToDictionary(g => g.Key, g => g.First());

                var selectedButtonTunes = (ButtonTunes)selectedButtonNum;
                var allPatterns = from track in _trackDB.AllTrack
                                  from pattern in track.Patterns
                                  where pattern.Button == selectedButtonTunes
                                  let patternStr = pattern.Difficulty.AsString()
                                  let recordKey = (track.Info.Id, patternStr)
                                  let record = recordDict.ContainsKey(recordKey) ? recordDict[recordKey] : null
                                  let fallbackLevel = record?.Level ?? pattern.Level
                                  let dbFloor = pattern.Floor > 0 ? (double?)pattern.Floor : null
                                  let effectiveFloor = record?.Floor ?? dbFloor
                                  let hasFloor = effectiveFloor.HasValue
                                  let floorKey = hasFloor ? (int?)effectiveFloor.Value : null
                                  let boardValue = selectedBoard ?? string.Empty
                                  where IsPatternMatchedByBoard(boardValue, patternStr, fallbackLevel, floorKey)
                                  select new VArchivePatternItem()
                                  {
                                      Id = track.Info.Id,
                                      Style = patternStr,
                                      Title = track.Info.Title,
                                      Floor = hasFloor ? effectiveFloor.Value : fallbackLevel,
                                      FloorText = !string.IsNullOrWhiteSpace(record?.FloorName)
                                          ? record.FloorName
                                          : hasFloor
                                              ? (floorKey.HasValue && floorNameByFloor.ContainsKey(floorKey.Value)
                                                  ? floorNameByFloor[floorKey.Value]
                                                  : effectiveFloor.Value.ToString())
                                              : $"{fallbackLevel}L",
                                      HasFloor = hasFloor,
                                      Score = record?.Score,
                                      IsMaxCombo = record?.MaxCombo,
                                      HasRecord = record != null
                                  };

                PatternItems.AddRange(allPatterns
                    .OrderBy(p => p.HasFloor ? 1 : 0)
                    .ThenBy(p => p.Floor)
                    .ThenBy(p => p.Title)
                    .ThenBy(p => p.Style));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Request failed: {ex.Message}", "Request Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isRequesting = false;
            }
        }

        private static async Task<Dictionary<int, string>> LoadFloorNameByFloor(HttpClient client)
        {
            try
            {
                var floors = await client.GetFromJsonAsync<List<FloorInfo>>(FloorDbUrl);
                if (floors is null)
                {
                    return new Dictionary<int, string>();
                }

                return floors
                    .Where(f => !string.IsNullOrWhiteSpace(f.FloorName))
                    .GroupBy(f => f.Floor)
                    .ToDictionary(g => g.Key, g => g.First().FloorName);
            }
            catch
            {
                return new Dictionary<int, string>();
            }
        }

        private static bool IsPatternMatchedByBoard(string board, string patternStyle, int level, int? floor)
        {
            return board switch
            {
                "1L" => patternStyle != "SC" && level == 1,
                "2L" => patternStyle != "SC" && level == 2,
                "3L" => patternStyle != "SC" && level == 3,
                "4L" => patternStyle != "SC" && level == 4,
                "5L" => patternStyle != "SC" && level == 5,
                "6L" => patternStyle != "SC" && level == 6,
                "7L" => patternStyle != "SC" && level == 7,
                "8L" => patternStyle != "SC" && level == 8,
                "9L" => patternStyle != "SC" && level == 9,
                "10L" => patternStyle != "SC" && level == 10,
                "11L" => patternStyle != "SC" && level == 11,
                "12L~15L" => patternStyle != "SC" && level >= 12,
                "SC" => patternStyle == "SC",
                "SC~5" => floor.HasValue && floor.Value <= 53,
                "SC~10" => floor.HasValue && floor.Value >= 61 && floor.Value <= 103,
                "SC~15" => floor.HasValue && floor.Value >= 111,
                _ => true,
            };
        }

        #region Query
        public bool IncludesPlayed { get; set; } = false;
        public bool IncludesScore { get; set; } = false;
        public bool IncludesMaxCombo { get; set; } = true;
        public bool IncludesFloor { get; set; } = false;
        public bool IsPlayed { get; set; } = false;
        public bool IsNotPlayed { get; set; } = true;
        public double ScoreAbove { get; set; } = 0.0;
        public double ScoreBelow { get; set; } = 99.99;
        public bool IsMaxCombo { get; set; } = false;
        public bool IsNotMaxCombo { get; set; } = true;
        public double FloorAbove { get; set; } = 0.0;
        public double FloorBelow { get; set; } = 15.0;

        public void ApplyQuery(string command)
        {
            IEnumerable<VArchivePatternItem> items = PatternItems;
            if (IncludesPlayed)
            {
                items = IsPlayed
                      ? items.Where(i => i.Score is not null)
                      : items.Where(i => i.Score is null);
            }
            if (IncludesScore)
            {
                items = items.Where(i => i.Score is not null)
                        .Where(i => ScoreAbove <= i.Score && i.Score <= ScoreBelow);
            }
            if (IncludesMaxCombo)
            {
                items = items.Where(i => i.IsMaxCombo is not null)
                        .Where(i => i.IsMaxCombo == IsMaxCombo);
            }
            if (IncludesFloor)
            {
                items = items.Where(i => i.HasFloor)
                             .Where(i =>
                             {
                                 if (string.IsNullOrWhiteSpace(i.FloorText))
                                 {
                                     return false;
                                 }

                                 var text = i.FloorText.Trim();
                                 if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double floorDisplay))
                                 {
                                     return false;
                                 }

                                 if (!text.Contains(".") && floorDisplay >= 100)
                                 {
                                     floorDisplay /= 10.0;
                                 }

                                 return FloorAbove <= floorDisplay && floorDisplay <= FloorBelow;
                             });
            }

            bool checks = command != "deselect";
            if (command == "exclusive")
            {
                foreach (var item in PatternItems)
                {
                    item.IsChecked = false;
                }
            }

            foreach (var item in items)
            {
                item.IsChecked = checks;
            }
        }
        #endregion

        public void PublishItems(string command)
        {
            var items = from item in PatternItems
                        where item.IsChecked
                        select 100 * item.Id + 10 * (int)CurrentButton.AsButtonTunes() + (int)item.Style.AsDifficulty();
            _eventAggregator.PublishOnUIThreadAsync(new VArchiveMessage(items.ToArray(), command));
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            if (close)
            {
                _eventAggregator.PublishOnUIThreadAsync(new VArchiveMessage(null, "close"));
            }
            return Task.CompletedTask;
        }

        public record ArchiveV2Response
        {
            public bool? Success { get; init; }
            public int Button { get; init; }
            public string Nickname { get; init; }
            public int Count { get; init; }
            public List<ArchiveV2Record> Records { get; init; }
            public string Message { get; init; }
        }

        public record ArchiveV2Record
        {
            public int Title { get; init; }
            public string Name { get; init; }
            public string DlcCode { get; init; }
            public string Pattern { get; init; }
            public int Level { get; init; }
            public double? Floor { get; init; }
            public string FloorName { get; init; }
            public bool NewTab { get; init; }
            public double MaxRating { get; init; }
            public double Score { get; init; }
            public bool MaxCombo { get; init; }
            public double? Rating { get; init; }
            public double Djpower { get; init; }
            public double? MaxDjpower { get; init; }
            public string UpdatedAt { get; init; }
        }

        public record FloorInfo
        {
            public int Floor { get; init; }
            public string FloorName { get; init; }
        }
    }
}
