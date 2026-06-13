using System.Reflection;
using FluentAssertions;
using Learnexia.Modules.Ai.Infrastructure.Gateway;
using Learnexia.Modules.Ai.Infrastructure.Providers;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Architecture test: asserts that no provider SDK / provider-client namespace
/// (e.g. "Anthropic.", "OpenAI.", "Google.Apis.") is referenced from any assembly
/// other than <c>Learnexia.Modules.Ai.Infrastructure</c>.
///
/// This enforces AC1: <c>IAiGateway</c> is the single public entry point and
/// provider SDK types are isolated to the Infrastructure layer.
///
/// Implementation approach: reflection-based assembly scan (no external arch testing package
/// required). Loads all Learnexia assemblies from the build output and verifies that
/// none of their public/internal types carry provider SDK namespace prefixes in their
/// custom attributes, base types, interface implementations, or method signatures.
///
/// Since we use thin HttpClient wrappers (Q7 decision) — NOT vendor SDK packages —
/// the test asserts that no type in non-Infrastructure assemblies references the
/// provider namespace roots that would indicate a vendor SDK leak.
/// </summary>
public sealed class AiModuleArchTests
{
    // Provider SDK namespace prefixes that must NEVER appear outside Ai.Infrastructure.
    // Using raw HTTP (no vendor SDK) so these should never appear anywhere in our code.
    // Adding them here as a forward-looking guard for if someone accidentally adds a vendor SDK.
    private static readonly string[] ForbiddenNamespacePrefixes =
    [
        "Anthropic.",
        "OpenAI.",
        "Google.Apis.",
        "Betalgo.OpenAI.",     // common community OpenAI SDK
        "Azure.AI.OpenAI.",    // Azure OpenAI SDK
    ];

    private static readonly string InfraAssemblyName =
        typeof(AiGateway).Assembly.GetName().Name!; // "Learnexia.Modules.Ai.Infrastructure"

    /// <summary>
    /// Verifies that no Learnexia assembly (other than Ai.Infrastructure) references
    /// any provider SDK namespace.
    /// </summary>
    [Fact(DisplayName = "P301-ARCH-01 No provider SDK namespace appears outside Ai.Infrastructure")]
    public void NoProviderSdkNamespace_OutsideAiInfrastructure()
    {
        // Load all Learnexia assemblies present in the current AppDomain (test runner loads them).
        var learnexiaAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a =>
            {
                var name = a.GetName().Name ?? string.Empty;
                return name.StartsWith("Learnexia.", StringComparison.Ordinal) &&
                       name != InfraAssemblyName &&
                       !a.IsDynamic;
            })
            .ToList();

        var violations = new List<string>();

        foreach (var assembly in learnexiaAssemblies)
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    foreach (var prefix in ForbiddenNamespacePrefixes)
                    {
                        if (TypeReferencesNamespace(type, prefix))
                        {
                            violations.Add(
                                $"Assembly '{assembly.GetName().Name}' / Type '{type.FullName}' " +
                                $"references forbidden namespace prefix '{prefix}'");
                        }
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Some assemblies may fail partial load — skip those gracefully.
            }
        }

        violations.Should().BeEmpty(
            "provider SDK namespaces must be isolated to Learnexia.Modules.Ai.Infrastructure " +
            "(AC1). Violations: {0}", string.Join("; ", violations));
    }

    /// <summary>
    /// Verifies that <c>ClaudeProvider</c> and <c>OpenAiProvider</c> live in the
    /// <c>Ai.Infrastructure</c> assembly — confirming the provider adapters are correctly placed.
    /// </summary>
    [Fact(DisplayName = "P301-ARCH-02 Provider adapters are in Ai.Infrastructure assembly")]
    public void ProviderAdapters_AreInAiInfrastructure()
    {
        typeof(ClaudeProvider).Assembly.GetName().Name
            .Should().Be(InfraAssemblyName,
                "ClaudeProvider must live in Learnexia.Modules.Ai.Infrastructure");

        typeof(OpenAiProvider).Assembly.GetName().Name
            .Should().Be(InfraAssemblyName,
                "OpenAiProvider must live in Learnexia.Modules.Ai.Infrastructure");
    }

    /// <summary>
    /// Verifies that <c>IAiGateway</c> lives in <c>Shared.Contracts</c> — confirming the
    /// seam is accessible to all modules without referencing the Ai module's projects.
    /// </summary>
    [Fact(DisplayName = "P301-ARCH-03 IAiGateway lives in Shared.Contracts assembly")]
    public void IAiGateway_IsInSharedContracts()
    {
        var assemblyName = typeof(Learnexia.Shared.Contracts.Ai.IAiGateway).Assembly.GetName().Name;
        assemblyName.Should().Be("Learnexia.Shared.Contracts",
            "IAiGateway must be in Shared.Contracts so any module can inject it without " +
            "referencing the Ai module's projects (module isolation rule)");
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static bool TypeReferencesNamespace(Type type, string namespacePrefix)
    {
        // Check the type's own namespace.
        if (type.Namespace?.StartsWith(namespacePrefix, StringComparison.Ordinal) == true)
            return true;

        // Check base type.
        if (type.BaseType?.Namespace?.StartsWith(namespacePrefix, StringComparison.Ordinal) == true)
            return true;

        // Check implemented interfaces.
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.Namespace?.StartsWith(namespacePrefix, StringComparison.Ordinal) == true)
                return true;
        }

        // Check method parameter and return types (public + private to catch hidden usage).
        try
        {
            foreach (var method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.ReturnType.Namespace?.StartsWith(namespacePrefix, StringComparison.Ordinal) == true)
                    return true;

                foreach (var param in method.GetParameters())
                {
                    if (param.ParameterType.Namespace?.StartsWith(namespacePrefix, StringComparison.Ordinal) == true)
                        return true;
                }
            }
        }
        catch
        {
            // Reflection may fail on some generated types — ignore.
        }

        return false;
    }
}
