using System;
using System.IO;

public sealed class ConfigSnapshot
{
    public bool Changed;
    public bool Exists;
    public WidgetSettings Settings;
}

public sealed class ConfigWatcher
{
    private static readonly TimeSpan EmptyDebounce = TimeSpan.FromMilliseconds(500);

    private readonly string _path;
    private bool _initialized;
    private bool _lastExists;
    private DateTime _lastWriteUtc;
    private long _lastLength;
    private WidgetSettings _lastSettings = new WidgetSettings();

    private bool _hasPendingEmpty;
    private DateTime _pendingEmptySince;
    private bool _pendingEmptyExists;
    private WidgetSettings _pendingEmptySettings;

    public ConfigWatcher(string path)
    {
        _path = path;
    }

    public ConfigSnapshot Poll(DateTime nowUtc)
    {
        bool exists = SafeExists();
        DateTime writeUtc = DateTime.MinValue;
        long length = 0;

        if (exists)
        {
            try
            {
                writeUtc = File.GetLastWriteTimeUtc(_path);
                length = new FileInfo(_path).Length;
            }
            catch
            {
                return Snapshot(false, _lastExists, _lastSettings);
            }
        }

        bool metadataChanged = !_initialized || exists != _lastExists
            || (exists && (writeUtc != _lastWriteUtc || length != _lastLength));

        if (!metadataChanged)
        {
            if (_hasPendingEmpty && nowUtc - _pendingEmptySince >= EmptyDebounce)
            {
                ApplyPendingEmpty();
                return Snapshot(true, _lastExists, _lastSettings);
            }
            return Snapshot(false, _lastExists, _lastSettings);
        }

        if (!exists)
        {
            _lastExists = false;
            _lastWriteUtc = DateTime.MinValue;
            _lastLength = 0;
            _initialized = true;

            if (_lastSettings.Codes.Count == 0)
            {
                _lastSettings = new WidgetSettings();
                ClearPending();
                return Snapshot(true, false, _lastSettings);
            }

            StartPendingEmpty(false, new WidgetSettings(), nowUtc);
            return Snapshot(false, false, _lastSettings);
        }

        WidgetSettings settings;
        try
        {
            settings = StockList.ParseWidgetSettings(File.ReadAllText(_path));
        }
        catch
        {
            return Snapshot(false, _lastExists, _lastSettings);
        }
        if (settings == null) settings = new WidgetSettings();

        _lastExists = true;
        _lastWriteUtc = writeUtc;
        _lastLength = length;
        _initialized = true;

        if (settings.Codes.Count > 0)
        {
            _lastSettings = settings;
            ClearPending();
            return Snapshot(true, true, _lastSettings);
        }

        if (_lastSettings.Codes.Count == 0)
        {
            _lastSettings = settings;
            ClearPending();
            return Snapshot(true, true, _lastSettings);
        }

        StartPendingEmpty(true, settings, nowUtc);
        return Snapshot(false, true, _lastSettings);
    }

    private void StartPendingEmpty(bool exists, WidgetSettings settings, DateTime nowUtc)
    {
        if (!_hasPendingEmpty)
        {
            _hasPendingEmpty = true;
            _pendingEmptySince = nowUtc;
        }
        _pendingEmptyExists = exists;
        _pendingEmptySettings = settings;
    }

    private void ApplyPendingEmpty()
    {
        _lastSettings = _pendingEmptySettings != null ? _pendingEmptySettings : new WidgetSettings();
        _lastExists = _pendingEmptyExists;
        ClearPending();
    }

    private void ClearPending()
    {
        _hasPendingEmpty = false;
        _pendingEmptySince = DateTime.MinValue;
        _pendingEmptyExists = false;
        _pendingEmptySettings = null;
    }

    private bool SafeExists()
    {
        return !string.IsNullOrEmpty(_path) && File.Exists(_path);
    }

    private static ConfigSnapshot Snapshot(bool changed, bool exists, WidgetSettings settings)
    {
        var s = new ConfigSnapshot();
        s.Changed = changed;
        s.Exists = exists;
        s.Settings = settings != null ? settings : new WidgetSettings();
        return s;
    }
}
