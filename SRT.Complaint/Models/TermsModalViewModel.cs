#nullable enable

namespace SRT.Complaint.Models;

public sealed record TermsModalViewModel(
    ComplaintTerms? Terms,
    bool Show,
    bool AcceptedTerms,
    string FormId);
