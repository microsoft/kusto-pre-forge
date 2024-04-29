
namespace KustoPreForgeLib.Settings
{
    public class AuthSettings
    {
        public AuthMode AuthMode { get; }

        public string? ManagedIdentityResourceId { get; }

        public AuthSettings(AuthMode? authMode, string? managedIdentityResourceId)
        {
            if (AuthMode == AuthMode.ManagedIdentity
                && string.IsNullOrWhiteSpace(managedIdentityResourceId))
            {
                throw new ArgumentNullException(nameof(managedIdentityResourceId));
            }

            AuthMode = authMode ?? AuthMode.Default;
            ManagedIdentityResourceId = managedIdentityResourceId;
        }

        public void WriteOutSettings()
        {
            Console.WriteLine($"AuthMode:  {AuthMode}");
            Console.WriteLine($"ManagedIdentityResourceId:  {ManagedIdentityResourceId}");
        }
    }
}