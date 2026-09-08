using System.Xml.Linq;
using NetArchTest.Rules;

// NetArchTest and xUnit both define a TestResult. We only ever mean NetArchTest's.
using TestResult = NetArchTest.Rules.TestResult;

namespace Fleet.Architecture.Tests;

/// <summary>
/// The module contract from the README, enforced.
/// </summary>
/// <remarks>
/// If one of these fails, the fix is almost never to change the test. It is to move the type,
/// or to add the missing interface to the <c>.Contracts</c> project and depend on that instead.
/// </remarks>
public sealed class ModuleBoundaryTests
{
    /// <summary>
    /// Rule 1a: no module may reference ASP.NET Core at all.
    /// </summary>
    /// <remarks>
    /// Checked against the project files rather than the compiled assemblies, because an unused
    /// package reference still breaks the boundary - and it is exactly how the boundary erodes:
    /// someone adds the package "just to use StatusCodes", and six weeks later the domain layer
    /// returns <c>IActionResult</c>.
    /// </remarks>
    [Fact]
    public void Modules_do_not_reference_aspnet_core()
    {
        var offenders = new List<string>();

        foreach (var projectFile in SolutionLayout.ModuleProjectFiles)
        {
            var project = XDocument.Load(projectFile);

            var webSdk = project.Root?.Attribute("Sdk")?.Value;
            if (webSdk is not null && webSdk.Contains("Web", StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add($"{SolutionLayout.ProjectName(projectFile)} uses the Web SDK");
            }

            var frameworkReferences = project
                .Descendants("FrameworkReference")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(value => value is not null && value.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase));

            offenders.AddRange(frameworkReferences.Select(reference =>
                $"{SolutionLayout.ProjectName(projectFile)} has FrameworkReference '{reference}'"));

            var packageReferences = project
                .Descendants("PackageReference")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(value => value is not null
                                && value.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase));

            offenders.AddRange(packageReferences.Select(reference =>
                $"{SolutionLayout.ProjectName(projectFile)} has PackageReference '{reference}'"));
        }

        Assert.True(
            offenders.Count == 0,
            "Modules must not know that HTTP exists. Offenders:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// Rule 1b: and no module may use an ASP.NET Core type, however it got hold of one.
    /// </summary>
    [Fact]
    public void Module_types_do_not_depend_on_aspnet_core()
    {
        foreach (var assembly in SolutionLayout.ModuleAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .Should()
                .NotHaveDependencyOn("Microsoft.AspNetCore")
                .GetResult();

            Assert.True(
                result.IsSuccessful,
                $"{assembly.GetName().Name} uses ASP.NET Core types: {FormatFailures(result)}");
        }
    }

    /// <summary>
    /// Rule 2: a module may reference other modules' <c>.Contracts</c> projects, never their
    /// implementations.
    /// </summary>
    [Fact]
    public void Modules_reference_only_other_modules_contracts()
    {
        var offenders = new List<string>();

        foreach (var projectFile in SolutionLayout.ModuleProjectFiles)
        {
            var projectName = SolutionLayout.ProjectName(projectFile);
            var owningModule = SolutionLayout.OwningModule(projectName);

            var referencedProjects = XDocument.Load(projectFile)
                .Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(value => value is not null)
                .Select(value => Path.GetFileNameWithoutExtension(value!.Replace('\\', Path.DirectorySeparatorChar)));

            foreach (var reference in referencedProjects)
            {
                if (!reference.StartsWith("Fleet.Modules.", StringComparison.Ordinal))
                {
                    continue;
                }

                var isContracts = reference.EndsWith(".Contracts", StringComparison.Ordinal);
                var isOwnModule = SolutionLayout.OwningModule(reference) == owningModule;

                if (!isContracts && !isOwnModule)
                {
                    offenders.Add($"{projectName} -> {reference}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A module may only reference another module's .Contracts project. Offenders:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// Rule 2b: the dependency arrow never points from the shared kernel to a module.
    /// </summary>
    [Fact]
    public void Shared_kernel_references_no_module()
    {
        var commonProject = Path.Combine(
            SolutionLayout.RepositoryRoot, "src", "Fleet.Common", "Fleet.Common.csproj");

        var references = XDocument.Load(commonProject)
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .ToList();

        Assert.True(
            references.Count == 0,
            "Fleet.Common is the shared kernel and must depend on nothing. It references:\n  "
            + string.Join("\n  ", references));
    }

    /// <summary>
    /// Rule 3: nothing internal may leak out through a public contract.
    /// </summary>
    /// <remarks>
    /// A public interface whose method returns an internal type compiles happily inside its own
    /// assembly and is unusable from anywhere else. This catches it at the point where it is
    /// still one line to fix.
    /// </remarks>
    [Fact]
    public void Contracts_expose_only_public_types()
    {
        var offenders = new List<string>();

        foreach (var assembly in SolutionLayout.ContractsAssemblies)
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                foreach (var method in type.GetMethods())
                {
                    if (method.DeclaringType != type)
                    {
                        continue;
                    }

                    AssertVisible(method.ReturnType, $"{type.Name}.{method.Name} returns", offenders);

                    foreach (var parameter in method.GetParameters())
                    {
                        AssertVisible(
                            parameter.ParameterType,
                            $"{type.Name}.{method.Name} takes '{parameter.Name}' of",
                            offenders);
                    }
                }

                foreach (var property in type.GetProperties())
                {
                    if (property.DeclaringType == type)
                    {
                        AssertVisible(property.PropertyType, $"{type.Name}.{property.Name} is", offenders);
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Contracts must be usable from outside their assembly. Offenders:\n  "
            + string.Join("\n  ", offenders));
    }

    private static void AssertVisible(Type type, string description, List<string> offenders)
    {
        foreach (var part in Unwrap(type))
        {
            if (!part.IsPublic && !part.IsNestedPublic && !part.IsGenericParameter)
            {
                offenders.Add($"{description} non-public type '{part.FullName ?? part.Name}'");
            }
        }
    }

    /// <summary>Yields a type and, for generics and arrays, the types hiding inside it.</summary>
    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (type.IsByRef || type.IsArray || type.IsPointer)
        {
            var element = type.GetElementType();
            if (element is not null)
            {
                foreach (var inner in Unwrap(element))
                {
                    yield return inner;
                }
            }

            yield break;
        }

        yield return type;

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var inner in Unwrap(argument))
            {
                yield return inner;
            }
        }
    }

    private static string FormatFailures(TestResult result) =>
        result.FailingTypeNames is null
            ? "(none reported)"
            : string.Join(", ", result.FailingTypeNames);
}
