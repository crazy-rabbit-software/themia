namespace Themia.App.Services.Accounts;

/// <summary>Where and as whom an IMAP account signs in. Holds a plaintext password: keep it short-lived.</summary>
internal sealed record ImapCredentials(string Host, int Port, string Username, string Password)
{
    /// <summary>Never prints the password.</summary>
    public override string ToString() => $"{Username}@{Host}:{Port}";
}
