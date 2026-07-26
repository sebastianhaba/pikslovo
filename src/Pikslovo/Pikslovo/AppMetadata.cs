using System.Reflection;

namespace Pikslovo;

public static class AppMetadata
{
    public static string DisplayVersion { get; } =
        typeof(AppMetadata).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?
            .Split('+')[0]
        ?? typeof(AppMetadata).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    public static string DisplayVersionLabel => $"Pikslovo v{DisplayVersion}";
}
