using Trackside.Application.LiveSession;
using Trackside.Application.Persistence;
using Trackside.Domain.LiveSession;

namespace Trackside.Service.LiveData;

/// <summary>
/// Adds deterministic, session-stable Tracker colours to browser-facing live snapshots.
/// </summary>
public sealed class LiveSessionDriverColorAssigner
{
    private readonly ITracksideStore _store;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, ActiveParticipant> _sessionParticipants = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string>? _history;
    private string _sessionKey = string.Empty;
    private double? _lastSessionSeconds;
    private long _nextJoinSequence;
    private string? _lastPersistedFingerprint;

    /// <summary>
    /// Creates a colour assigner backed by the durable Trackside store.
    /// </summary>
    /// <param name="store">Store that retains name-to-colour history between host restarts.</param>
    public LiveSessionDriverColorAssigner(ITracksideStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Applies session-stable colours using first-seen join order and persists changed active assignments.
    /// </summary>
    /// <param name="snapshot">Normalized source snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A snapshot whose driver rows contain Tracker colours.</returns>
    public async Task<LiveSessionSnapshot> ApplyAsync(LiveSessionSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Drivers.Count == 0)
        {
            return snapshot;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_history is null)
            {
                _history = new Dictionary<string, string>(
                    await _store.GetDriverColorHistoryAsync(cancellationToken),
                    StringComparer.OrdinalIgnoreCase);
            }

            if (IsNewSession(snapshot))
            {
                _sessionKey = GetSessionKey(snapshot);
                _sessionParticipants.Clear();
                _nextJoinSequence = 0;
                _lastSessionSeconds = null;
            }

            foreach (var driver in snapshot.Drivers)
            {
                if (!_sessionParticipants.TryGetValue(driver.DriverId, out var participant))
                {
                    participant = new ActiveParticipant(++_nextJoinSequence, GetHistoryKey(driver), null);
                    _sessionParticipants[driver.DriverId] = participant;
                }
                else
                {
                    // A staff alias can be amended while cars are already running. Keep the assigned colour, but
                    // save future history under the corrected name once the next assignment is persisted.
                    participant = participant with { HistoryKey = GetHistoryKey(driver) };
                    _sessionParticipants[driver.DriverId] = participant;
                }
            }

            // Keep every participant seen in this session in the allocation, even when a source frame briefly omits
            // a car. That reservation is what makes a mid-session reconnect unable to reshuffle another driver's colour.
            var allocationParticipants = _sessionParticipants
                .Select(participant => new DriverColorParticipant
                {
                    ParticipantId = participant.Key,
                    HistoryKey = participant.Value.HistoryKey,
                    JoinSequence = participant.Value.JoinSequence,
                })
                .ToList();
            var activeParticipants = snapshot.Drivers
                .Select(driver => new DriverColorParticipant
                {
                    ParticipantId = driver.DriverId,
                    HistoryKey = _sessionParticipants[driver.DriverId].HistoryKey,
                    JoinSequence = _sessionParticipants[driver.DriverId].JoinSequence,
                })
                .ToList();
            var allocationHistory = new Dictionary<string, string>(_history, StringComparer.OrdinalIgnoreCase);
            foreach (var participant in allocationParticipants)
            {
                if (_sessionParticipants.TryGetValue(participant.ParticipantId, out var sessionParticipant)
                    && !string.IsNullOrWhiteSpace(sessionParticipant.Color))
                {
                    allocationHistory[participant.HistoryKey] = sessionParticipant.Color;
                }
            }

            var assignments = DriverColorAllocator.Allocate(allocationParticipants, allocationHistory);
            var activeHistory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var participant in allocationParticipants)
            {
                var color = assignments[participant.ParticipantId];
                _sessionParticipants[participant.ParticipantId] = _sessionParticipants[participant.ParticipantId] with { Color = color };
                if (activeParticipants.Any(active => string.Equals(active.ParticipantId, participant.ParticipantId, StringComparison.OrdinalIgnoreCase)))
                {
                    _history[participant.HistoryKey] = color;
                    activeHistory[participant.HistoryKey] = color;
                }
            }

            await PersistChangedHistoryAsync(activeHistory, cancellationToken);
            _lastSessionSeconds = Math.Max(_lastSessionSeconds ?? 0, snapshot.Session.CurrentSessionSeconds ?? 0);

            return snapshot with
            {
                Drivers = snapshot.Drivers
                    .Select(driver => driver with { TrackerColor = assignments[driver.DriverId] })
                    .ToList(),
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsNewSession(LiveSessionSnapshot snapshot)
    {
        var sessionKey = GetSessionKey(snapshot);
        if (!string.Equals(_sessionKey, sessionKey, StringComparison.Ordinal))
        {
            return true;
        }

        // A large elapsed-clock rewind signals a new run of the same track and session type.
        return snapshot.Session.CurrentSessionSeconds is { } currentSessionSeconds
            && _lastSessionSeconds is { } lastSessionSeconds
            && currentSessionSeconds < lastSessionSeconds - 15.0;
    }

    private async Task PersistChangedHistoryAsync(IReadOnlyDictionary<string, string> activeHistory, CancellationToken cancellationToken)
    {
        var fingerprint = string.Join('|', activeHistory
            .OrderBy(assignment => assignment.Key, StringComparer.OrdinalIgnoreCase)
            .Select(assignment => $"{assignment.Key}:{assignment.Value}"));
        if (string.Equals(fingerprint, _lastPersistedFingerprint, StringComparison.Ordinal))
        {
            return;
        }

        await _store.SaveDriverColorHistoryAsync(activeHistory, cancellationToken);
        _lastPersistedFingerprint = fingerprint;
    }

    private static string GetSessionKey(LiveSessionSnapshot snapshot) => string.Join('|',
        snapshot.Source.Trim(),
        snapshot.Session.TrackName.Trim(),
        snapshot.Session.Kind);

    private static string GetHistoryKey(DriverSnapshot driver)
    {
        var name = string.IsNullOrWhiteSpace(driver.DisplayName) ? driver.RigName : driver.DisplayName;
        return name.Trim().ToUpperInvariant();
    }

    private sealed record ActiveParticipant(long JoinSequence, string HistoryKey, string? Color);
}
