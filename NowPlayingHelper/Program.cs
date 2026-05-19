using System;
using System.Globalization;
using Windows.Media.Control;

try
{
    var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
    var session = manager.GetCurrentSession();
    if (session == null)
    {
        Console.Write("{\"Title\":null}");
        return;
    }
    var props   = await session.TryGetMediaPropertiesAsync();
    var timeline = session.GetTimelineProperties();
    var playback = session.GetPlaybackInfo();
    string title  = (props.Title  ?? "").Replace("\"", "");
    string artist = (props.Artist ?? "").Replace("\"", "");
    double elapsed = timeline.Position.TotalSeconds;
    double endTime = timeline.EndTime.TotalSeconds;
    string status  = playback.PlaybackStatus.ToString();
    Console.Write(
        "{\"Title\":\"" + title + "\"," +
        "\"Artist\":\"" + artist + "\"," +
        "\"ElapsedTime\":" + elapsed.ToString("F3", CultureInfo.InvariantCulture) + "," +
        "\"EndTime\":"    + endTime.ToString("F3", CultureInfo.InvariantCulture) + "," +
        "\"Status\":\"" + status + "\"}");
}
catch
{
    Console.Write("{\"Title\":null}");
}
