using Amplify.Core.Auth;
using Windows.Security.Credentials;

namespace Amplify.App.Auth;

/// <summary>
/// Stores the Spotify refresh token in the Windows Credential Locker, scoped to the app's package
/// identity. The locker is OS-managed and not readable as plaintext, satisfying the secure-storage
/// requirement; <see cref="Clear"/> backs the disconnect / data-deletion path.
/// </summary>
internal sealed class PasswordVaultRefreshTokenStore : IRefreshTokenStore
{
    private const string _resourceName = "Amplify";
    private const string _userName = "refresh_token";

    // HRESULT the locker raises when the requested credential does not exist (ERROR_NOT_FOUND).
    private const int _elementNotFound = unchecked((int)0x80070490);

    private readonly PasswordVault _vault = new();

    public string? Load()
    {
        try
        {
            PasswordCredential credential = _vault.Retrieve(_resourceName, _userName);
            credential.RetrievePassword(); // Password is lazily populated; this fills it in.
            return credential.Password;
        }
        catch (Exception ex) when (ex.HResult == _elementNotFound)
        {
            return null;
        }
    }

    public void Save(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(refreshToken);
        Clear(); // Replace any existing token for this resource/user.
        _vault.Add(new PasswordCredential(_resourceName, _userName, refreshToken));
    }

    public void Clear()
    {
        try
        {
            PasswordCredential credential = _vault.Retrieve(_resourceName, _userName);
            _vault.Remove(credential);
        }
        catch (Exception ex) when (ex.HResult == _elementNotFound)
        {
            // Nothing stored — clearing is a no-op.
        }
    }
}
