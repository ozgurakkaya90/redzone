namespace RiskManagement.Services.Email;

public record EmailMessage(
    string To,
    string ToName,
    string Subject,
    string HtmlBody,
    int    Attempt = 0
);
