namespace UmbracoCommunity.Web.Features.Members.Migration;

/// <summary>
/// Thrown by <see cref="ExternalMemberMigrationService.MigrateAsync"/> when another run holds the
/// per-instance semaphore or the cross-instance <see cref="ExternalMemberMigrationLease"/>.
/// </summary>
public sealed class ExternalMemberMigrationAlreadyRunningException(string message) : InvalidOperationException(message);
