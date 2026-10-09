#nullable enable
namespace SRT.Complaint.Models;

/// <summary>Structured data behind the "ขั้นตอนการร้องเรียน" home popup — backs both the admin form and the generated .step-list HTML.</summary>
public record StepItemData(string Title, string Description, bool Done);
public record StepsBlockData(List<StepItemData> Items, string SlaNote, string? ImageUrl);

/// <summary>Structured data behind the "ช่องทางติดต่อ" home popup — backs both the admin form and the generated .contact-dl HTML.</summary>
public record ContactItemData(string Label, string Value);
public record ContactBlockData(List<ContactItemData> Items, string? ImageUrl);

/// <summary>Structured data behind the "ความปลอดภัย & ความเป็นส่วนตัว" home popup — backs both the admin form and the generated .trust-ul HTML.</summary>
public record TrustItemData(string Text);
public record TrustBlockData(List<TrustItemData> Items, string Note, string? ImageUrl);
