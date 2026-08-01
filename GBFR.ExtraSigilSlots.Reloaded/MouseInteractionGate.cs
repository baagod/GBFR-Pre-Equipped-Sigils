namespace GBFR.ExtraSigilSlots.Reloaded;

internal sealed class MouseInteractionGate
{
    private const int RequiredReleasedFrames = 2;

    private bool _open;
    private int _releasedFrames;
    private long _lastButtonEventSequence;

    internal bool IsArmed { get; private set; }

    internal void Open(long buttonEventSequence)
    {
        _open = true;
        _releasedFrames = 0;
        _lastButtonEventSequence = buttonEventSequence;
        IsArmed = false;
    }

    internal void Close()
    {
        _open = false;
        _releasedFrames = 0;
        IsArmed = false;
    }

    internal void Observe(uint pressedButtons, long buttonEventSequence)
    {
        if (!_open || IsArmed)
            return;
        if (buttonEventSequence != _lastButtonEventSequence)
        {
            _lastButtonEventSequence = buttonEventSequence;
            _releasedFrames = 0;
            return;
        }
        if (pressedButtons != 0)
        {
            _releasedFrames = 0;
            return;
        }

        ++_releasedFrames;
        if (_releasedFrames >= RequiredReleasedFrames)
            IsArmed = true;
    }
}
