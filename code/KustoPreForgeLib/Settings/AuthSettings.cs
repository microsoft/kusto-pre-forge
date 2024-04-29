
using Azure.Core;
using Azure.Identity;

namespace KustoPreForgeLib.Settings
{
    public class AuthSettings
    {
        public AuthMode AuthMode { get; }

        public string? ManagedIdentityResourceId { get; }
        
        public string? TenantId { get; }
        
        public string? ApplicationId { get; }
        
        public string? Secret { get; }

        public AuthSettings(
            AuthMode? authMode,
            string? managedIdentityResourceId,
            string? tenantId,
            string? applicationId,
            string? secret)
        {
            if (AuthMode == AuthMode.ManagedIdentity
                && string.IsNullOrWhiteSpace(managedIdentityResourceId))
            {
                throw new ArgumentNullException(nameof(managedIdentityResourceId));
            }
            if (AuthMode == AuthMode.Secret)
            {
                if (string.IsNullOrWhiteSpace(tenantId))
                {
                    throw new ArgumentNullException(nameof(managedIdentityResourceId));
                }
                if (string.IsNullOrWhiteSpace(applicationId))
                {
                    throw new ArgumentNullException(nameof(applicationId));
                }
                if (string.IsNullOrWhiteSpace(secret))
                {
                    throw new ArgumentNullException(nameof(secret));
                }
            }

            AuthMode = authMode ?? AuthMode.Default;
            ManagedIdentityResourceId = managedIdentityResourceId;
            TenantId = tenantId;
            ApplicationId = applicationId;
            Secret = secret;
        }

        public void WriteOutSettings()
        {
            Console.WriteLine($"AuthMode:  {AuthMode}");
            Console.WriteLine($"ManagedIdentityResourceId:  {ManagedIdentityResourceId}");
            Console.WriteLine($"TenantId:  {TenantId}");
            Console.WriteLine($"ApplicationId:  {ApplicationId}");
            Console.WriteLine($"Secret:  {Secret?.Length} characters");
        }

        public TokenCredential GetCredentials()
        {
            switch (AuthMode)
            {
                case AuthMode.Default:
                    return new DefaultAzureCredential();
                
                case AuthMode.ManagedIdentity:
                    return new ManagedIdentityCredential(
                        new ResourceIdentifier(ManagedIdentityResourceId!));
                
                case AuthMode.Secret:
                    return new ClientSecretCredential(TenantId, ApplicationId, Secret);

                default:
                    throw new NotSupportedException(
                        $"Auth mode:  '{AuthMode}'");
            }
        }
    }
}