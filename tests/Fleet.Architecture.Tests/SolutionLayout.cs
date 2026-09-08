using System.Reflection;

namespace Fleet.Architecture.Tests;

/// <summary>
/// Locates the repository on disk and the compiled module assemblies in the test output.
/// </summary>
/// <remarks>
/// Two of the three architecture rules are about project files rather than about types, so these
/// tests read <c>.csproj</c> files directly. A <c>PackageReference</c> to ASP.NET Core that the
/// module does not happen to use yet is still a broken boundary, and no amount of reflection over
/// the compiled assembly would notice it.
/// </remarks>
internal static class SolutionLayout
{
    /// <summary>The repository root, found by walking up from the test binaries to Fleet.sln.</summary>
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string ModulesDirectory { get; } = Path.Combine(RepositoryRoot, "src", "Modules");

    public static readonly string[] ModuleNames =
    [
        "Vehicles",
        "Drivers",
        "Bookings",
        "Maintenance",
        "Reporting",
        "Notifications",
    ];

    /// <summary>Every module implementation and contracts assembly, loaded from the test output.</summary>
    public static IReadOnlyList<Assembly> ModuleAssemblies { get; } = LoadModuleAssemblies(
        static name => name.StartsWith("Fleet.Modules.", StringComparison.Ordinal));

    /// <summary>Only the <c>*.Contracts</c> assemblies.</summary>
    public static IReadOnlyList<Assembly> ContractsAssemblies { get; } = LoadModuleAssemblies(
        static name => name.StartsWith("Fleet.Modules.", StringComparison.Ordinal)
                       && name.EndsWith(".Contracts", StringComparison.Ordinal));

    /// <summary>Every <c>.csproj</c> under <c>src/Modules</c>, implementations and contracts alike.</summary>
    public static IReadOnlyList<string> ModuleProjectFiles { get; } =
        [.. Directory.EnumerateFiles(ModulesDirectory, "*.csproj", SearchOption.AllDirectories).Order()];

    public static string ProjectName(string projectFilePath) =>
        Path.GetFileNameWithoutExtension(projectFilePath);

    /// <summary>
    /// The module a project belongs to: both <c>Fleet.Modules.Vehicles</c> and
    /// <c>Fleet.Modules.Vehicles.Contracts</c> return <c>Vehicles</c>.
    /// </summary>
    public static string OwningModule(string projectName)
    {
        var withoutPrefix = projectName["Fleet.Modules.".Length..];

        return withoutPrefix.EndsWith(".Contracts", StringComparison.Ordinal)
            ? withoutPrefix[..^".Contracts".Length]
            : withoutPrefix;
    }

    private static IReadOnlyList<Assembly> LoadModuleAssemblies(Func<string, bool> nameFilter)
    {
        var outputDirectory = Path.GetDirectoryName(typeof(SolutionLayout).Assembly.Location)!;

        return
        [
            .. Directory.EnumerateFiles(outputDirectory, "Fleet.Modules.*.dll")
                .Where(path => nameFilter(Path.GetFileNameWithoutExtension(path)))
                .Select(Assembly.LoadFrom)
                .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
        ];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Fleet.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find Fleet.sln by walking up from '{AppContext.BaseDirectory}'.");
    }
}
