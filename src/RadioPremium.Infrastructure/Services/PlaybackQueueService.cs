using RadioPremium.Core.Models;
using RadioPremium.Core.Services;

namespace RadioPremium.Infrastructure.Services;

/// <summary>
/// Implementation of playback queue service
/// </summary>
public sealed class PlaybackQueueService : IPlaybackQueueService
{
    private List<Station> _queue = new();
    private List<Station> _originalQueue = new(); // For shuffle
    private List<Station> _history = new();
    private int _currentIndex = -1;
    private bool _isShuffleEnabled;
    private bool _isRepeatEnabled;

    public IReadOnlyList<Station> Queue => _queue.AsReadOnly();
    public int CurrentIndex => _currentIndex;
    public Station? CurrentStation => _currentIndex >= 0 && _currentIndex < _queue.Count
        ? _queue[_currentIndex]
        : null;

    public bool IsShuffleEnabled
    {
        get => _isShuffleEnabled;
        set
        {
            if (_isShuffleEnabled != value)
            {
                _isShuffleEnabled = value;
                ApplyShuffle();
                QueueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsRepeatEnabled
    {
        get => _isRepeatEnabled;
        set
        {
            if (_isRepeatEnabled != value)
            {
                _isRepeatEnabled = value;
                QueueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool HasNext => _queue.Count > 0 && (_currentIndex < _queue.Count - 1 || _isRepeatEnabled);
    public bool HasPrevious => _history.Count > 0 || (_currentIndex > 0);

    public event EventHandler? QueueChanged;
    public event EventHandler<Station?>? CurrentStationChanged;

    public void SetQueue(IEnumerable<Station> stations, Station? startStation = null)
    {
        _queue = stations.ToList();
        _originalQueue = _queue.ToList();

        if (_isShuffleEnabled)
        {
            ApplyShuffle();
        }

        if (startStation != null)
        {
            _currentIndex = _queue.FindIndex(s => s.StationUuid == startStation.StationUuid);
        }
        else
        {
            _currentIndex = _queue.Count > 0 ? 0 : -1;
        }

        QueueChanged?.Invoke(this, EventArgs.Empty);
        CurrentStationChanged?.Invoke(this, CurrentStation);
    }

    public void AddToQueue(Station station)
    {
        _queue.Add(station);
        _originalQueue.Add(station);
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public void PlayNext(Station station)
    {
        if (_currentIndex >= 0 && _currentIndex < _queue.Count)
        {
            _queue.Insert(_currentIndex + 1, station);
            _originalQueue.Insert(_currentIndex + 1, station);
        }
        else
        {
            AddToQueue(station);
        }

        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveFromQueue(Station station)
    {
        var index = _queue.FindIndex(s => s.StationUuid == station.StationUuid);
        if (index >= 0)
        {
            _queue.RemoveAt(index);
            _originalQueue.Remove(station);

            if (index < _currentIndex)
            {
                _currentIndex--;
            }
            else if (index == _currentIndex)
            {
                // Current station removed, stay at same index (plays next automatically)
                CurrentStationChanged?.Invoke(this, CurrentStation);
            }

            QueueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClearQueue()
    {
        _queue.Clear();
        _originalQueue.Clear();
        _history.Clear();
        _currentIndex = -1;
        QueueChanged?.Invoke(this, EventArgs.Empty);
        CurrentStationChanged?.Invoke(this, null);
    }

    public Station? MoveNext()
    {
        if (_queue.Count == 0) return null;

        // Add current to history
        if (CurrentStation != null)
        {
            _history.Add(CurrentStation);
            if (_history.Count > 50) // Keep last 50
            {
                _history.RemoveAt(0);
            }
        }

        if (_currentIndex < _queue.Count - 1)
        {
            _currentIndex++;
        }
        else if (_isRepeatEnabled)
        {
            _currentIndex = 0; // Loop back to start
        }
        else
        {
            return null; // End of queue
        }

        var station = CurrentStation;
        CurrentStationChanged?.Invoke(this, station);
        return station;
    }

    public Station? MovePrevious()
    {
        // First try to go back in history
        if (_history.Count > 0)
        {
            var previousStation = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);

            // Find it in queue
            var index = _queue.FindIndex(s => s.StationUuid == previousStation.StationUuid);
            if (index >= 0)
            {
                _currentIndex = index;
                CurrentStationChanged?.Invoke(this, CurrentStation);
                return CurrentStation;
            }
        }

        // Otherwise move to previous in queue
        if (_currentIndex > 0)
        {
            _currentIndex--;
            CurrentStationChanged?.Invoke(this, CurrentStation);
            return CurrentStation;
        }
        else if (_isRepeatEnabled && _queue.Count > 0)
        {
            _currentIndex = _queue.Count - 1; // Loop to end
            CurrentStationChanged?.Invoke(this, CurrentStation);
            return CurrentStation;
        }

        return null;
    }

    public IReadOnlyList<Station> GetHistory(int count = 10)
    {
        return _history.TakeLast(count).ToList().AsReadOnly();
    }

    private void ApplyShuffle()
    {
        if (_isShuffleEnabled)
        {
            var currentStation = CurrentStation;

            // Shuffle using Fisher-Yates algorithm
            var random = new Random();
            var shuffled = _originalQueue.ToList();

            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            // Ensure current station stays at current position if it exists
            if (currentStation != null)
            {
                var currentStationIndex = shuffled.FindIndex(s => s.StationUuid == currentStation.StationUuid);
                if (currentStationIndex >= 0 && currentStationIndex != _currentIndex)
                {
                    (shuffled[_currentIndex], shuffled[currentStationIndex]) =
                        (shuffled[currentStationIndex], shuffled[_currentIndex]);
                }
            }

            _queue = shuffled;
        }
        else
        {
            // Restore original order
            var currentStation = CurrentStation;
            _queue = _originalQueue.ToList();

            // Update current index to match station in original order
            if (currentStation != null)
            {
                _currentIndex = _queue.FindIndex(s => s.StationUuid == currentStation.StationUuid);
            }
        }
    }
}
