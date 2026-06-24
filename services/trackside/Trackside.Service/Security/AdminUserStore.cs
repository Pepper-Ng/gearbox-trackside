using System.Text.Json;
using Microsoft.Extensions.Options;
using Trackside.Application.Serialization;
using Trackside.Service.Configuration;
using Trackside.Service.Hosting;

namespace Trackside.Service.Security;

/// <summary>
/// File-backed admin user store for the local venue service.
/// </summary>
public sealed class AdminUserStore
{
    private const int SchemaVersion = 1;
    private readonly TracksideRuntimeContext _runtimeContext;
    private readonly IOptionsMonitor<TracksideOptions> _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Creates the admin user store.
    /// </summary>
    /// <param name="runtimeContext">Runtime paths used for local fallback storage.</param>
    /// <param name="options">Deployment options used to locate durable data.</param>
    public AdminUserStore(TracksideRuntimeContext runtimeContext, IOptionsMonitor<TracksideOptions> options)
    {
        _runtimeContext = runtimeContext;
        _options = options;
    }

    /// <summary>
    /// Path of the persisted admin user store.
    /// </summary>
    public string StorePath => Path.Combine(GetSecurityDirectory(), "admin-users.json");

    /// <summary>
    /// Returns true when at least one admin exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the store contains users.</returns>
    public async Task<bool> HasUsersAsync(CancellationToken cancellationToken) => (await GetUsersAsync(cancellationToken)).Count > 0;

    /// <summary>
    /// Lists admin users without password hashes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Admin user summaries.</returns>
    public async Task<IReadOnlyList<AdminUserSummary>> GetUsersAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            return store.Users
                .OrderBy(user => user.Username, StringComparer.OrdinalIgnoreCase)
                .Select(ToSummary)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Creates the first admin user when the store is empty.
    /// </summary>
    /// <param name="request">Admin creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created admin summary.</returns>
    public async Task<AdminUserSummary> CreateFirstAdminAsync(AdminCreateUserRequest request, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            if (store.Users.Count > 0)
            {
                throw new InvalidOperationException("Admin bootstrap is already complete.");
            }

            var user = CreateRecord(request);
            store.Users.Add(user);
            await SaveAsync(store, cancellationToken);
            return ToSummary(user);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Creates an additional admin user.
    /// </summary>
    /// <param name="request">Admin creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created admin summary.</returns>
    public async Task<AdminUserSummary> CreateUserAsync(AdminCreateUserRequest request, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            var normalizedUsername = NormalizeUsername(request.Username);
            if (store.Users.Any(user => string.Equals(user.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("An admin user with that username already exists.");
            }

            var user = CreateRecord(request);
            store.Users.Add(user);
            await SaveAsync(store, cancellationToken);
            return ToSummary(user);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Verifies admin credentials.
    /// </summary>
    /// <param name="username">Admin username.</param>
    /// <param name="password">Admin password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>User summary when credentials match, otherwise null.</returns>
    public async Task<AdminUserSummary?> VerifyAsync(string username, string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        string normalizedUsername;
        try
        {
            normalizedUsername = NormalizeUsername(username);
        }
        catch (ArgumentException)
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            var user = store.Users.FirstOrDefault(candidate => string.Equals(candidate.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                return null;
            }

            var passwordHash = new AdminPasswordHash(user.PasswordHashAlgorithm, user.PasswordHashIterations, user.PasswordSalt, user.PasswordHash);
            return AdminPasswordHasher.Verify(password, passwordHash) ? ToSummary(user) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Changes an admin password.
    /// </summary>
    /// <param name="username">Admin username.</param>
    /// <param name="newPassword">New password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ChangePasswordAsync(string username, string newPassword, CancellationToken cancellationToken)
    {
        ValidatePassword(newPassword);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            var normalizedUsername = NormalizeUsername(username);
            var user = store.Users.FirstOrDefault(candidate => string.Equals(candidate.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Admin user not found.");

            var hash = AdminPasswordHasher.Hash(newPassword);
            user.PasswordHashAlgorithm = hash.Algorithm;
            user.PasswordHashIterations = hash.Iterations;
            user.PasswordSalt = hash.Salt;
            user.PasswordHash = hash.Hash;
            user.UpdatedUtc = DateTimeOffset.UtcNow;
            await SaveAsync(store, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string GetSecurityDirectory()
    {
        var dataPath = _options.CurrentValue.Deployment.DataPath;
        var root = !string.IsNullOrWhiteSpace(dataPath)
            ? dataPath
            : Path.Combine(_runtimeContext.ContentRootPath, "App_Data");

        return Path.Combine(root, "security");
    }

    private async Task<AdminUserStoreDocument> LoadAsync(CancellationToken cancellationToken)
    {
        var path = StorePath;
        if (!File.Exists(path))
        {
            return new AdminUserStoreDocument();
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AdminUserStoreDocument>(stream, TracksideJson.SerializerOptions, cancellationToken)
            ?? new AdminUserStoreDocument();
    }

    private async Task SaveAsync(AdminUserStoreDocument store, CancellationToken cancellationToken)
    {
        store.SchemaVersion = SchemaVersion;
        var path = StorePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _runtimeContext.ContentRootPath);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, store, TracksideJson.SerializerOptions, cancellationToken);
        }

        File.Move(temporaryPath, path, overwrite: true);
    }

    private static AdminUserRecord CreateRecord(AdminCreateUserRequest request)
    {
        ValidatePassword(request.Password);
        var now = DateTimeOffset.UtcNow;
        var hash = AdminPasswordHasher.Hash(request.Password);
        var username = NormalizeUsername(request.Username);
        return new AdminUserRecord
        {
            Username = username,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? username : request.DisplayName.Trim(),
            PasswordHashAlgorithm = hash.Algorithm,
            PasswordHashIterations = hash.Iterations,
            PasswordSalt = hash.Salt,
            PasswordHash = hash.Hash,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
    }

    private static string NormalizeUsername(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        var normalized = username.Trim();
        if (normalized.Length is < 3 or > 64 || normalized.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Admin usernames must be 3-64 characters and cannot contain whitespace.", nameof(username));
        }

        return normalized;
    }

    private static void ValidatePassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (password.Length < 12)
        {
            throw new ArgumentException("Admin passwords must be at least 12 characters.", nameof(password));
        }
    }

    private static AdminUserSummary ToSummary(AdminUserRecord user) => new(user.Username, user.DisplayName, user.CreatedUtc, user.UpdatedUtc);

    private sealed record AdminUserStoreDocument
    {
        public int SchemaVersion { get; set; } = 1;

        public List<AdminUserRecord> Users { get; init; } = [];
    }

    private sealed record AdminUserRecord
    {
        public string Username { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string PasswordHashAlgorithm { get; set; } = AdminPasswordHasher.Algorithm;

        public int PasswordHashIterations { get; set; } = AdminPasswordHasher.Iterations;

        public string PasswordSalt { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public DateTimeOffset CreatedUtc { get; set; }

        public DateTimeOffset UpdatedUtc { get; set; }
    }
}

/// <summary>
/// Public admin user summary.
/// </summary>
public sealed record AdminUserSummary(string Username, string DisplayName, DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc);

/// <summary>
/// Request used to create an admin user.
/// </summary>
public sealed record AdminCreateUserRequest
{
    /// <summary>
    /// Admin username.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Display name shown in the admin panel.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Initial password.
    /// </summary>
    public string Password { get; init; } = string.Empty;
}

/// <summary>
/// Request used to authenticate an admin.
/// </summary>
public sealed record AdminLoginRequest(string Username, string Password);

/// <summary>
/// Request used to change an admin password.
/// </summary>
public sealed record AdminChangePasswordRequest(string NewPassword);