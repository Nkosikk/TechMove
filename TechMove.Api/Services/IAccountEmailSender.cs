namespace TechMove.Api.Services;

public interface IAccountEmailSender
{
    Task SendRegistrationConfirmationAsync(
        string email,
        string displayName,
        string confirmationUrl,
        CancellationToken cancellationToken = default);

    Task SendAccountApprovedAsync(
        string email,
        string displayName,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(
        string email,
        string displayName,
        string resetUrl,
        CancellationToken cancellationToken = default);
}
