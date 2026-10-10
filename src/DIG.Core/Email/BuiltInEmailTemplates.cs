namespace DIG.Core.Email;

/// <summary>Seed templates covering the common IT-support communication types.
/// These are inserted on first run and can be edited or deleted by the user.</summary>
public static class BuiltInEmailTemplates
{
    private const string Sig =
        "\n\nBest regards,\n{{engineer}}\n{{team}}";

    public static IReadOnlyList<EmailTemplate> All { get; } = new List<EmailTemplate>
    {
        New("Initial case response", "Response",
            "[{{case_number}}] We have received your request",
            "Hello {{customer}},\n\nThank you for contacting support. I have received your case regarding {{affected_component}} and will begin investigating right away.\n\nTo help me move quickly, could you confirm the affected product version ({{product_version}}) and the time the issue was first observed?" + Sig),

        New("Information / log request", "Request",
            "[{{case_number}}] Additional information required",
            "Hello {{customer}},\n\nTo continue the investigation into {{affected_component}}, could you please provide:\n\n- A support bundle / log export from the affected system\n- The exact error message and timestamp\n- Any recent changes to the environment\n\nOnce I have these I will update you with the next step." + Sig),

        New("Meeting invitation", "Scheduling",
            "[{{case_number}}] Proposed troubleshooting session",
            "Hello {{customer}},\n\nI would like to schedule a short session to investigate {{affected_component}} together. Please let me know your availability and I will send a calendar invitation.\n\nSuggested focus: {{next_step}}." + Sig),

        New("Technical progress update", "Update",
            "[{{case_number}}] Progress update",
            "Hello {{customer}},\n\nA quick update on your case. So far I have {{action_taken}}. The next step is {{next_step}}.\n\nI will follow up with further findings as soon as they are available." + Sig),

        New("Findings and recommendations", "Update",
            "[{{case_number}}] Findings and recommended actions",
            "Hello {{customer}},\n\nBased on the analysis of {{affected_component}} on {{product_version}}, the findings are:\n\n1. {{finding}}\n\nRecommended action:\n\n- {{next_step}}\n\nPlease review and let me know if you would like to proceed." + Sig),

        New("Severity update", "Update",
            "[{{case_number}}] Case severity update",
            "Hello {{customer}},\n\nThe severity of this case has been updated. This reflects the current business impact of {{affected_component}}. Our response targets have been adjusted accordingly and I will keep you informed of progress." + Sig),

        New("Workaround communication", "Update",
            "[{{case_number}}] Temporary workaround available",
            "Hello {{customer}},\n\nWhile we continue working on a permanent fix, the following workaround can reduce the impact of {{affected_component}}:\n\n{{action_taken}}\n\nPlease note this is a temporary measure. I will advise once a permanent resolution is confirmed." + Sig),

        New("Resolution confirmation", "Closure",
            "[{{case_number}}] Resolution confirmation",
            "Hello {{customer}},\n\nThe issue affecting {{affected_component}} should now be resolved following {{action_taken}}. Could you please confirm that everything is working as expected on your side?" + Sig),

        New("Case closure", "Closure",
            "[{{case_number}}] Case closure",
            "Hello {{customer}},\n\nAs the issue with {{affected_component}} has been resolved and confirmed, I will now close this case. Please do not hesitate to reach out if anything else comes up." + Sig),

        New("Follow-up / escalation", "Escalation",
            "[{{case_number}}] Follow-up",
            "Hello {{customer}},\n\nI wanted to follow up on this case. If the matter is still impacting you, please let me know and I will ensure it receives the appropriate attention, including escalation if required." + Sig),
    };

    private static EmailTemplate New(string name, string category, string subject, string body)
        => new(0, name, category, subject, body, true, DateTimeOffset.UtcNow);
}
