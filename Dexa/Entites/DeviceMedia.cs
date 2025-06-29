namespace Else.PhoneMirror.ViewModels;

public class DeviceMedia
{
    public Boolean WasMediaPlaying { get; set; }
    public DateTime? MediaPaused { get; set; }

    public TimeSpan? GetTimeSinceMediaPaused()
    {
        if (MediaPaused == null)
            return null;
        return DateTime.UtcNow - MediaPaused.Value;
    }
}