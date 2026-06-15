namespace Authly.Modules.Users;

/// <summary>Admin-side create-user request (Management API). Password optional (invite-style accounts may have none yet).</summary>
public sealed record CreateUserRequest(
    string Email,
    string? Password,
    string? FirstName,
    string? LastName,
    bool EmailVerified = false);

/// <summary>Admin-side partial update. Null fields are left unchanged.</summary>
public sealed record UpdateUserRequest(
    string? FirstName,
    string? LastName,
    string? Phone,
    string? Timezone,
    string? Locale);

public sealed class UserNotFoundException(Guid id) : Exception($"User {id} was not found.");

public sealed class UserEmailAlreadyExistsException(string email) : Exception($"A user with email '{email}' already exists.");
