using SamyrTechWay.SpinWin.Tests.Support;

namespace SamyrTechWay.SpinWin.Tests.Engine;

/// <summary>
/// Same suites on a real SQL Server, using the real EF migrations. Opt in with
/// SPINWIN_TEST_SQLSERVER="Server=...;User Id=...;Password=...;TrustServerCertificate=True" (a server login
/// allowed to CREATE DATABASE). Each test class creates and drops its own throwaway database.
/// </summary>
internal static class SqlServerGate
{
    public static string? ConnectionString => Environment.GetEnvironmentVariable("SPINWIN_TEST_SQLSERVER");

    public static Task<TestHost> CreateAsync()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), "SPINWIN_TEST_SQLSERVER not set — SQL Server suite skipped.");
        return TestHost.SqlServerAsync(ConnectionString!);
    }
}

[Trait("Category", "SqlServer")]
public class SqlServerSpinEngineTests : SpinEngineTests
{
    protected override Task<TestHost> CreateHostAsync() => SqlServerGate.CreateAsync();
}

[Trait("Category", "SqlServer")]
public class SqlServerConcurrencyTests : ConcurrencyTests
{
    protected override Task<TestHost> CreateHostAsync() => SqlServerGate.CreateAsync();
}

[Trait("Category", "SqlServer")]
public class SqlServerCustomerLoginTests : CustomerLoginTests
{
    protected override Task<TestHost> CreateHostAsync() => SqlServerGate.CreateAsync();
}

[Trait("Category", "SqlServer")]
public class SqlServerAdminRulesTests : AdminRulesTests
{
    protected override Task<TestHost> CreateHostAsync() => SqlServerGate.CreateAsync();
}
