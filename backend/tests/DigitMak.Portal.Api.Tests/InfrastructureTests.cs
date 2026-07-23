using DigitMak.Portal.Api.Application;
using DigitMak.Portal.Api.Application.Realtime;

namespace DigitMak.Portal.Api.Tests;

public class InfrastructureTests
{
    [Fact]
    public void Presence_tracker_follows_join_leave_and_disconnect()
    {
        var tracker = new TicketPresenceTracker();
        var ticketId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        tracker.Join("connection-1", ticketId, userId);
        Assert.True(tracker.IsPresent(ticketId, userId));

        tracker.Leave("connection-1", ticketId);
        Assert.False(tracker.IsPresent(ticketId, userId));

        tracker.Join("connection-2", ticketId, userId);
        tracker.Disconnect("connection-2");
        Assert.False(tracker.IsPresent(ticketId, userId));
    }

    [Theory]
    [InlineData("mk", "DigitMak - Тикетот е креиран")]
    [InlineData("sq", "DigitMak - Tiketa u krijua")]
    public void Email_templates_localize_ticket_notifications(
        string language,
        string expectedSubject
    )
    {
        var rendered = EmailTemplates.Render(
            "TicketCreated",
            language,
            "Ticket created",
            "<p>Original body</p>"
        );
        Assert.Equal(expectedSubject, rendered.Subject);
        Assert.DoesNotContain("Original body", rendered.Body);
    }

    [Fact]
    public void Localized_email_preserves_secure_action_link()
    {
        var rendered = EmailTemplates.Render(
            "EmailVerification",
            "mk",
            "Verify email",
            "<p><a href=\"https://portal.example/verify?token=abc\">Verify</a></p>"
        );

        Assert.Contains("https://portal.example/verify?token=abc", rendered.Body);
    }
}
