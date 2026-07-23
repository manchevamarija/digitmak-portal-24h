using System.Text;

namespace DigitMak.Portal.Api.Application;

public static class CalendarExport
{
    public static byte[] Ics(IEnumerable<Meeting> meetings)
    {
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//DigitMak//Portal V1//EN",
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
        };
        foreach (var meeting in meetings.Where(x => x.StartsAt is not null))
        {
            lines.AddRange([
                "BEGIN:VEVENT",
                $"UID:{meeting.Id}@portal.digitmak.mk",
                $"DTSTAMP:{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}",
                $"DTSTART:{meeting.StartsAt!.Value.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}",
                $"DTEND:{(meeting.EndsAt ?? meeting.StartsAt.Value.AddHours(1)).UtcDateTime:yyyyMMdd'T'HHmmss'Z'}",
                $"SUMMARY:{Escape(meeting.Subject)}",
                $"DESCRIPTION:{Escape(meeting.Description)}",
                $"LOCATION:{Escape(meeting.MeetingType == "Online" ? meeting.OnlineLink ?? "Online" : meeting.Location ?? "")}",
                $"STATUS:{(meeting.Status == "Cancelled" ? "CANCELLED" : "CONFIRMED")}",
                "END:VEVENT",
            ]);
        }
        lines.Add("END:VCALENDAR");
        return Encoding.UTF8.GetBytes(string.Join("\r\n", lines) + "\r\n");
    }

    private static string Escape(string value) =>
        value
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n");
}
