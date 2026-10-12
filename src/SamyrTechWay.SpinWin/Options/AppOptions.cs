namespace SamyrTechWay.SpinWin.Options;

public sealed class SecurityOptions
{
    public const string Section = "Security";

    /// <summary>
    /// Force HTTPS redirect, HSTS and Secure-only cookies. Keep true in production once HTTPS is active.
    /// Set to false only for the MonsterASP Free pre-HTTPS demo period (a warning banner is shown).
    /// </summary>
    public bool RequireHttps { get; set; } = true;

    /// <summary>Honour X-Forwarded-For/Proto. Only enable when the app is behind a proxy you trust.</summary>
    public bool TrustForwardedHeaders { get; set; }

    /// <summary>
    /// Optional comma-separated proxy IPs allowed to send forwarded headers. Empty = any sender, which is safe only when
    /// clients cannot reach the app except through that proxy (only the right-most, proxy-added value is used).
    /// </summary>
    public string? KnownProxies { get; set; }
}

public sealed class AdminBootstrapOptions
{
    public const string Section = "AdminBootstrap";

    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? DisplayName { get; set; }

    /// <summary>When true and the admin already exists, reset its password and clear lockout (recovery).</summary>
    public bool ResetPasswordIfExists { get; set; }
}

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>Run pending EF Core migrations at startup. Off by default; back up first.</summary>
    public bool ApplyMigrationsOnStartup { get; set; }

    /// <summary>Seed the editable sample prize catalogue. Honoured only in the Development environment.</summary>
    public bool SeedDemoPrizes { get; set; }
}
