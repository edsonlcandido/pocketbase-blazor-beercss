using Microsoft.AspNetCore.Components.Forms;

namespace Web.Services;

/// <summary>
/// Operações de perfil do usuário logado.
/// Substitui a store Pinia <c>profile</c> do template Vue.
/// </summary>
public sealed class ProfileService
{
    private readonly PocketBaseClient _pb;
    private readonly AuthService _auth;

    public ProfileService(PocketBaseClient pb, AuthService auth)
    {
        _pb = pb;
        _auth = auth;
    }

    public PbUser? User => _auth.User;

    public string UserName => string.IsNullOrWhiteSpace(User?.Name) ? "Usuário" : User!.Name;

    public bool IsVerified => User?.Verified ?? false;

    public bool EmailVisibility => User?.EmailVisibility ?? false;

    public string AvatarUrl => User is null ? string.Empty : _pb.GetFileUrl(User);

    public string Initials
    {
        get
        {
            var source = (User?.Name ?? string.Empty).Trim();

            if (source.Length == 0)
            {
                source = (User?.Email ?? string.Empty).Trim();
            }

            if (source.Length == 0)
            {
                return "?";
            }

            var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
        }
    }

    public async Task<PbUser> LoadAsync(CancellationToken ct = default)
    {
        var user = await _pb.GetUserAsync(RequireId(), ct);
        _auth.SetUser(user);
        return user;
    }

    public async Task<PbUser> UpdateNameAsync(string name, CancellationToken ct = default)
    {
        var user = await _pb.UpdateUserAsync(RequireId(), new { name }, ct);
        _auth.SetUser(user);
        return user;
    }

    public async Task<PbUser> UpdateEmailVisibilityAsync(bool visible, CancellationToken ct = default)
    {
        var user = await _pb.UpdateUserAsync(RequireId(), new { emailVisibility = visible }, ct);
        _auth.SetUser(user);
        return user;
    }

    public async Task<PbUser> ChangePasswordAsync(
        string oldPassword, string password, string passwordConfirm, CancellationToken ct = default)
    {
        var user = await _pb.UpdateUserAsync(
            RequireId(), new { oldPassword, password, passwordConfirm }, ct);

        _auth.SetUser(user);
        return user;
    }

    public async Task<PbUser> UploadAvatarAsync(
        byte[] content, string fileName, string contentType, CancellationToken ct = default)
    {
        var user = await _pb.UploadFileAsync(RequireId(), "avatar", content, fileName, contentType, ct);
        _auth.SetUser(user);
        return user;
    }

    private string RequireId()
        => _auth.User?.Id ?? throw new InvalidOperationException("Nenhum usuário autenticado.");
}
