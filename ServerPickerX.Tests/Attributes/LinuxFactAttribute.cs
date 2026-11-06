namespace ServerPickerX.Tests.Attributes
{
    // Runs a test only on Linux as a regular user, since such tests execute real shell scripts
    // and the privilege elevation path is skipped entirely when the test runner is root
    public sealed class LinuxFactAttribute : FactAttribute
    {
        public LinuxFactAttribute()
        {
            if (!OperatingSystem.IsLinux())
            {
                Skip = "Requires Linux";
            }
            else if (Environment.IsPrivilegedProcess)
            {
                Skip = "Requires a non-root user";
            }
        }
    }
}
