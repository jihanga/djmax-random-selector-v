using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Caliburn.Micro;
using DjmaxRandomSelectorV.Messages;
using Dmrsv.RandomSelector;

namespace DjmaxRandomSelectorV
{
    public class RandomSelector : IHandle<FilterMessage>, IHandle<FilterOptionMessage>, IHandle<SettingMessage>
    {
        private readonly IEventAggregator _eventAggregator;

        private readonly TrackDB _db;
        private List<Pattern> _candidates;

        private bool _isRunning;
        private Pattern _lastPlayed;

        private IFilter _filter;
        private PatternPicker _picker;
        private IHistory<int> _history;
        private ISelector _selector;
        private SelectorWithHistory _randomSelector;
        private SequentialSelector _sequentialSelector;
        private Locator _locator;

        private SelectMode _selectMode;
        private FilterType _filterType;
        // UI가 먼저 생성되고 Initialize()는 이후에 호출되므로, 초기화 완료 전에는 어떤 동작도 수행하지 않는다.
        private bool _isInitialized;
        // 재시작 시 복원할 포인터 위치. 후보 목록에서 해당 패턴을 찾을 때까지 유지한다.
        private int? _pendingRestoreId;

        private readonly WindowTitleHelper _windowTitleHelper;

        public IHistory<int> History => _history;

        public RandomSelector(IEventAggregator eventAggregator, TrackDB db)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.SubscribeOnUIThread(this);
            _db = db;
            _isRunning = false;
            _windowTitleHelper = new WindowTitleHelper();
        }

        public void Initialize(Dmrsv3Configuration config)
        {
            _candidates = new List<Pattern>();
            _picker = new PatternPicker();
            _history = new History<int>(config.RecentPlayed, config.RecentsCount);
            _randomSelector = new SelectorWithHistory(_history);
            _sequentialSelector = new SequentialSelector();
            _filterType = config.FilterType;
            _selectMode = config.SelectMode;
            _selector = ResolveSelector();
            _pendingRestoreId = config.NextPatternId;
            _locator = new Locator();
            _locator.MakeLocations(_db.AllTrack);
            SetLocatorProperties(new FilterOptionMessage(
                config.RecentsCount,
                config.Mode,
                config.Aider,
                config.Level));
            _picker.SetPickMethod(config.Mode, config.Level);
            _isInitialized = true;
            UpdateCandidates();
        }

        private ISelector ResolveSelector()
        {
            // 순차 선택은 플레이리스트 필터에서만 동작한다. 검색 필터는 선곡 순서를 정의하지 않는다.
            bool useSequential = _selectMode == SelectMode.Sequential && _filterType == FilterType.Playlist;
            return useSequential ? _sequentialSelector : _randomSelector;
        }

        public bool CanStart()
        {
            if (!_isInitialized)
            {
                return false;
            }
            if (!_windowTitleHelper.EqualsDjmax())
            {
                ShowErrorMessageBox("The foreground window is not \"DJMAX RESPECT V\".\nPress start key in the game.");
                return false;
            }
            if (!_isRunning)
            {
                return true;
            }
            return false;
        }
        public void Start()
        {
            if (!_isInitialized)
            {
                return;
            }
            _isRunning = true;
            if (_filter.IsUpdated)
            {
                UpdateCandidates();
            }
            Pattern selected = _selector.Select(_candidates);
            if (selected is not null)
            {
                _locator.Locate(selected);
                _lastPlayed = selected;
                _eventAggregator.PublishOnUIThreadAsync(new PatternMessage(selected));
            }
            else
            {
                ShowErrorMessageBox("There is no music that meets the filter conditions.");
            }
            PublishNextPattern();
            _isRunning = false;
        }
        public void StartAgain()
        {
            if (!_isInitialized)
            {
                return;
            }
            _isRunning = true;
            if (_lastPlayed is not null)
            {
                _locator.Locate(_lastPlayed);
                _eventAggregator.PublishOnUIThreadAsync(new PatternMessage(_lastPlayed));
            }
            _isRunning = false;
        }

        // 순차 포인터를 지정한 패턴으로 이동한다. (예: 사용자가 플레이리스트 항목을 더블클릭한 경우)
        public void SetSequentialPointer(int patternId)
        {
            if (!_isInitialized || !ReferenceEquals(_selector, _sequentialSelector))
            {
                return;
            }
            _sequentialSelector.SetPointer(_candidates, patternId);
            PublishNextPattern();
        }

        // 후보를 다시 계산하고 순차 포인터를 동기화한다. (예: 플레이리스트를 편집한 직후)
        public void RefreshSequentialPointer()
        {
            if (!_isInitialized || !ReferenceEquals(_selector, _sequentialSelector))
            {
                return;
            }
            UpdateCandidates();
        }

        // 순차 선택기에서 다음에 뽑을 패턴. 순차 모드가 아니면 null을 반환한다.
        public Pattern PeekNextSequentialPattern()
        {
            if (!_isInitialized || !ReferenceEquals(_selector, _sequentialSelector))
            {
                return null;
            }
            return _sequentialSelector.NextPattern;
        }

        // 순차 모드가 아닐 때는 null을 발행하여 화면의 포인터 표시를 해제한다.
        private void PublishNextPattern()
        {
            if (!_isInitialized)
            {
                return;
            }
            Pattern next = ReferenceEquals(_selector, _sequentialSelector) ? _sequentialSelector.NextPattern : null;
            _eventAggregator.PublishOnUIThreadAsync(new NextPatternMessage(next));
        }

        private void UpdateCandidates()
        {
            if (_picker is null || _filter is null || _candidates is null)
            {
                return;
            }
            // 포인터가 현재 유지 중인 패턴을 저장해 두어, 플레이리스트가 수정되어도 같은 곡을 유지한다.
            int? previousPatternId = _sequentialSelector?.NextPattern?.PatternId;
            _candidates = _picker.Pick(_filter.Filter(_db.Playable)).ToList();
            if (!ReferenceEquals(_selector, _sequentialSelector))
            {
                PublishNextPattern();
                return;
            }
            _sequentialSelector.SyncPointer(_candidates, previousPatternId);
            // 재시작 시 저장해 둔 위치가 아직 유효하면 그 곡부터 이어서 진행한다.
            if (_pendingRestoreId.HasValue && _sequentialSelector.SetPointer(_candidates, _pendingRestoreId.Value))
            {
                _pendingRestoreId = null;
            }
            PublishNextPattern();
        }

        // 순차 모드에서 다음에 선곡될 패턴의 ID. 종료 시 저장하여 재시작 시 복원한다.
        public int? NextPatternId
        {
            get
            {
                if (!_isInitialized || !ReferenceEquals(_selector, _sequentialSelector))
                {
                    return null;
                }
                return _sequentialSelector.NextPattern?.PatternId;
            }
        }

        private void SetLocatorProperties(FilterOptionMessage message)
        {
            _locator.CanLocate = message.InputMethod != InputMethod.NotInput;
            _locator.LocatesStyle = message.MusicForm == MusicForm.Default;
            _locator.PressesStart = message.MusicForm == MusicForm.Default && message.InputMethod == InputMethod.WithAutoStart;
        }


        public Task HandleAsync(FilterMessage message, CancellationToken cancellationToken)
        {
            _filter = message.Item;
            // FilterMessage는 UI 생성 시점에 비동기로 발행되므로, Initialize() 시점에는 아직 도착하지 않는다.
            // 도착한 시점에 후보를 계산해야 시작 직후의 포인터가 정상적으로 표시된다.
            if (_isInitialized)
            {
                UpdateCandidates();
            }
            return Task.CompletedTask;
        }
        public Task HandleAsync(FilterOptionMessage message, CancellationToken cancellationToken)
        {
            if (!_isInitialized)
            {
                return Task.CompletedTask;
            }
            if (_history.Capacity != message.RecentsCount)
            {
                _history.Capacity = message.RecentsCount;
            }
            SetLocatorProperties(message);
            _picker.SetPickMethod(message.MusicForm, message.LevelPreference);
            _selector = ResolveSelector();
            UpdateCandidates();
            return Task.CompletedTask;
        }

        public Task HandleAsync(SettingMessage message, CancellationToken cancellationToken)
        {
            if (!_isInitialized)
            {
                return Task.CompletedTask;
            }
            _locator.InputInterval = message.InputInterval;
            _filterType = message.FilterType;
            _selectMode = message.SelectMode;
            _selector = ResolveSelector();
            _db.SetPlayable(message.OwnedDlcs);
            _locator.MakeLocations(_db.AllTrack);
            UpdateCandidates();
            return Task.CompletedTask;
        }

        private void ShowErrorMessageBox(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
