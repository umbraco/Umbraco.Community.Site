using FluentAssertions;
using UmbracoCommunity.MeetBooking.Models;
using UmbracoCommunity.MeetBooking.Workflows;
using Xunit;

namespace UmbracoCommunity.MeetBooking.Tests;

/// <summary>
/// The message wording is the whole point of this workflow existing, so it is tested directly. Posting is a single
/// HttpClient call with no logic worth a fake.
/// </summary>
public class PostMeetToSlackWorkflowTests
{
    private static MeetProvisioningState Booked() => new()
    {
        EventId = "evt1",
        HtmlLink = "https://www.google.com/calendar/event?eid=abc",
        MeetingCode = "tfb-hhth-qyz",
        MeetUri = "https://meet.google.com/tfb-hhth-qyz",
        SpaceName = "spaces/XYZ",
        Configured = true,
    };

    [Fact]
    public void An_automatic_cohost_says_what_happened_and_does_not_link_the_meet()
    {
        var state = Booked();
        state.CohostAdded = true;

        var message = PostMeetToSlackWorkflow.BuildMessage(state, "Umbraco Copenhagen meetup", "Owain Jones", "someone@gmail.com");

        message.Should().Be(
            "✅ Community meeting <https://www.google.com/calendar/event?eid=abc|created> for Owain Jones (Umbraco Copenhagen meetup).\n" +
            "someone@gmail.com has been invited and made a co-host, so they can start the Meet on their own.");
    }

    [Fact]
    public void The_meet_link_is_never_posted()
    {
        // The meeting is usually weeks away and a Meet link in a channel is something people click. The calendar
        // event is the useful destination instead.
        var state = Booked();
        state.CohostAdded = true;

        PostMeetToSlackWorkflow.BuildMessage(state, "x", "Owain Jones", "someone@gmail.com")
            .Should().NotContain("meet.google.com");
    }

    [Fact]
    public void A_manual_cohost_says_they_are_invited_but_not_yet_a_cohost()
    {
        var state = Booked();
        state.CohostManual = true;
        state.CohostInstructions = "Open the event as community@umbraco.com → gear icon next to the Meet link → Co-hosts → add someone@gmail.com → Save. Uninvited guests must knock, and only a host or co-host can admit them.";

        var message = PostMeetToSlackWorkflow.BuildMessage(state, "Seb 19", "Owain Jones", "someone@gmail.com");

        message.Should().Contain("has been invited, but is *not* a co-host yet.");
        message.Should().Contain("<https://www.google.com/calendar/event?eid=abc|Open the event> as community@umbraco.com");
        message.Should().EndWith("can admit them.");
    }

    [Fact]
    public void A_skipped_cohost_just_confirms_the_invite()
    {
        var state = Booked();
        state.CohostSkipped = true;

        PostMeetToSlackWorkflow.BuildMessage(state, "x", "Owain Jones", "someone@gmail.com")
            .Should().EndWith("someone@gmail.com has been invited.");
    }

    [Fact]
    public void A_missing_email_falls_back_to_naming_the_requester_generically()
    {
        var state = Booked();
        state.CohostAdded = true;

        PostMeetToSlackWorkflow.BuildMessage(state, "x", "Owain Jones", null)
            .Should().Contain("The requester has been invited and made a co-host");
    }

    [Fact]
    public void A_failed_booking_says_so_rather_than_posting_nothing()
    {
        // The built-in workflow at least showed provisioningError. Replacing it must not make failure silent.
        PostMeetToSlackWorkflow.BuildMessage(new MeetProvisioningState(), "Seb 19", "Owain Jones", "a@b.co")
            .Should().Be("⚠️ Community meeting *not* created for Owain Jones (Seb 19) — check the entry in the backoffice.");

        PostMeetToSlackWorkflow.BuildMessage(null, "Seb 19", "Owain Jones", "a@b.co")
            .Should().Contain("not* created");
    }

    [Fact]
    public void A_requester_cannot_forge_a_link_through_the_title_or_the_email()
    {
        // Both are attacker-controlled free text. Slack mrkdwn needs & < > escaped, and an unescaped | would end
        // the link label and let the rest become a different link target.
        var state = Booked();
        state.CohostAdded = true;

        var message = PostMeetToSlackWorkflow.BuildMessage(
            state,
            "evil|https://phish.example.com> <https://phish.example.com|click me",
            "name|<https://phish.example.com|evil",
            "also|<https://phish.example.com|evil");

        message.Should().NotContain("phish.example.com>");
        message.Should().NotContain("<https://phish.example.com|");
        message.Should().Contain("&#124;");
        message.Should().Contain("&lt;");
    }

    [Fact]
    public void An_instruction_that_does_not_start_with_the_known_prefix_is_left_alone()
    {
        var state = Booked();
        state.CohostManual = true;
        state.CohostInstructions = "Ask community@umbraco.com to add someone@gmail.com as co-host.";

        var message = PostMeetToSlackWorkflow.BuildMessage(state, "x", "Owain Jones", "someone@gmail.com");

        message.Should().EndWith("Ask community@umbraco.com to add someone@gmail.com as co-host.");
    }

    [Fact]
    public void A_missing_event_link_leaves_the_headline_unlinked()
    {
        var state = Booked();
        state.HtmlLink = null;
        state.CohostAdded = true;

        var message = PostMeetToSlackWorkflow.BuildMessage(state, "Seb 19", "Owain Jones", "someone@gmail.com");

        message.Should().StartWith("✅ Community meeting created for Owain Jones (Seb 19).\n");
        message.Should().NotContain("<|");
    }

    [Fact]
    public void The_message_is_exactly_two_lines_with_the_outcome_on_the_second()
    {
        var state = Booked();
        state.CohostAdded = true;

        var lines = PostMeetToSlackWorkflow.BuildMessage(state, "Community hour", "Owain Jones", "someone@gmail.com")
            .Split('\n');

        lines.Should().HaveCount(2);
        lines[0].Should().Be("✅ Community meeting <https://www.google.com/calendar/event?eid=abc|created> for Owain Jones (Community hour).");
        lines[1].Should().StartWith("someone@gmail.com has been invited");
    }

    [Fact]
    public void Only_the_word_created_is_linked()
    {
        var state = Booked();
        state.CohostAdded = true;

        var message = PostMeetToSlackWorkflow.BuildMessage(state, "Community hour", "Owain Jones", "someone@gmail.com");

        message.Should().Contain("|created>");
        message.Should().NotContain("|Community meeting");
    }

    [Fact]
    public void A_missing_requester_name_falls_back_to_the_title_alone()
    {
        var state = Booked();
        state.CohostAdded = true;

        PostMeetToSlackWorkflow.BuildMessage(state, "Community hour", null, "someone@gmail.com")
            .Should().StartWith("✅ Community meeting <https://www.google.com/calendar/event?eid=abc|created> for Community hour.");
    }

    [Fact]
    public void A_runaway_title_is_truncated_rather_than_posting_a_wall()
    {
        var state = Booked();
        state.CohostAdded = true;

        var message = PostMeetToSlackWorkflow.BuildMessage(state, new string('x', 8000), "Owain Jones", "a@b.co");

        message.Length.Should().Be(PostMeetToSlackWorkflow.MaxMessageLength);
        message.Should().EndWith("…");
    }
}
