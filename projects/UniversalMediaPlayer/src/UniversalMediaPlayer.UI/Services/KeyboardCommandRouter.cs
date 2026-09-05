namespace UniversalMediaPlayer.UI.Services;

public enum KeyInput
{
    None,
    Space,
    Left,
    Right,
    Up,
    Down,
    M,
    F,
    Escape,
    Enter,
    A,
    S,
    PageUp,
    PageDown
}

public enum PlayerAction
{
    None,
    PlayPause,
    SeekForwardSmall,
    SeekBackwardSmall,
    SeekForwardLarge,
    SeekBackwardLarge,
    VolumeUp,
    VolumeDown,
    ToggleMute,
    ToggleFullscreen,
    ExitFullscreen,
    CycleAudioTrack,
    CycleSubtitleTrack,
    NextEpisode,
    PreviousEpisode
}

public static class KeyboardCommandRouter
{
    public static PlayerAction Route(KeyInput key, bool isCtrlPressed = false, bool isAltPressed = false)
    {
        return key switch
        {
            KeyInput.Space => PlayerAction.PlayPause,
            KeyInput.Left when isCtrlPressed => PlayerAction.SeekBackwardLarge,
            KeyInput.Left => PlayerAction.SeekBackwardSmall,
            KeyInput.Right when isCtrlPressed => PlayerAction.SeekForwardLarge,
            KeyInput.Right => PlayerAction.SeekForwardSmall,
            KeyInput.Up => PlayerAction.VolumeUp,
            KeyInput.Down => PlayerAction.VolumeDown,
            KeyInput.M => PlayerAction.ToggleMute,
            KeyInput.F => PlayerAction.ToggleFullscreen,
            KeyInput.Enter when isAltPressed => PlayerAction.ToggleFullscreen,
            KeyInput.Escape => PlayerAction.ExitFullscreen,
            KeyInput.A when !isCtrlPressed && !isAltPressed => PlayerAction.CycleAudioTrack,
            KeyInput.S when !isCtrlPressed && !isAltPressed => PlayerAction.CycleSubtitleTrack,
            KeyInput.PageDown => PlayerAction.NextEpisode,
            KeyInput.PageUp => PlayerAction.PreviousEpisode,
            _ => PlayerAction.None
        };
    }
}
