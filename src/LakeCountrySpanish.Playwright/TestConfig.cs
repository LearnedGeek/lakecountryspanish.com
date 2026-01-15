namespace LakeCountrySpanish.Playwright;

/// <summary>
/// Configuration for Playwright tests.
/// Set environment variables to override defaults.
/// </summary>
public static class TestConfig
{
    /// <summary>
    /// Base URL for the application under test.
    /// Override with TEST_BASE_URL environment variable.
    /// </summary>
    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("TEST_BASE_URL") ?? "https://localhost:5001";

    /// <summary>
    /// Test student credentials for login tests.
    /// Override with TEST_STUDENT_EMAIL and TEST_STUDENT_PASSWORD environment variables.
    /// </summary>
    public static string TestStudentEmail =>
        Environment.GetEnvironmentVariable("TEST_STUDENT_EMAIL") ?? "teststudent@test.com";

    public static string TestStudentPassword =>
        Environment.GetEnvironmentVariable("TEST_STUDENT_PASSWORD") ?? "Test123!";

    /// <summary>
    /// Admin credentials for admin flow tests.
    /// Override with TEST_ADMIN_EMAIL and TEST_ADMIN_PASSWORD environment variables.
    /// </summary>
    public static string TestAdminEmail =>
        Environment.GetEnvironmentVariable("TEST_ADMIN_EMAIL") ?? "admin@lakecountryspanish.com";

    public static string TestAdminPassword =>
        Environment.GetEnvironmentVariable("TEST_ADMIN_PASSWORD") ?? "Admin123!";

    /// <summary>
    /// Whether to run against production (read-only tests only).
    /// Set TEST_ENVIRONMENT=production to enable.
    /// </summary>
    public static bool IsProduction =>
        Environment.GetEnvironmentVariable("TEST_ENVIRONMENT")?.ToLower() == "production";

    /// <summary>
    /// Timeout for page navigation in milliseconds.
    /// </summary>
    public static int NavigationTimeout => 30000;

    /// <summary>
    /// Timeout for element actions in milliseconds.
    /// </summary>
    public static int ActionTimeout => 10000;
}
