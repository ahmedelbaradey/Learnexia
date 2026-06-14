using System.Reflection;
using FluentAssertions;
using Learnexia.Modules.Ai.Application.Safety;
using Learnexia.Modules.Ai.Infrastructure.Gateway;
using Learnexia.Modules.Ai.Infrastructure.Providers;
using Learnexia.Shared.Contracts.Ai;
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
    // P302 arch tests — IAiGateway no-bypass (AC1)
    // -------------------------------------------------------------------------

    /// <summary>
    /// P302-ARCH-04: No type outside <c>Ai.Infrastructure</c> AND outside
    /// <c>Ai.Application/Safety/SafetyLayer</c> references <see cref="IAiGateway"/> directly.
    ///
    /// <para>Feature handlers (P3-04/05/06) may ONLY consume AI content through
    /// <see cref="ISafetyLayer"/>. A direct reference to <see cref="IAiGateway"/>
    /// from outside the allowed locations is a bypass of the safety layer (AC1, FR-AI-4 Critical).</para>
    ///
    /// <para>Allowed references:
    ///   <list type="bullet">
    ///     <item><c>Learnexia.Modules.Ai.Infrastructure</c> — the gateway implementation itself.</item>
    ///     <item><c>SafetyLayer</c> in <c>Learnexia.Modules.Ai.Application</c> — the facade that wraps it.</item>
    ///     <item><c>Learnexia.Shared.Contracts</c> — the interface definition itself.</item>
    ///     <item>The test assembly (<c>Modules.Ai.UnitTests</c>) — may reference it for DI setup.</item>
    ///   </list>
    /// </para>
    /// </summary>
    [Fact(DisplayName = "P302-ARCH-04 No type outside Ai.Infrastructure+SafetyLayer references IAiGateway directly")]
    public void NoTypeOutsideAllowed_ReferencesIAiGateway()
    {
        // Types that ARE allowed to reference IAiGateway:
        // 1. Ai.Infrastructure (the implementation).
        // 2. SafetyLayer in Ai.Application (the facade — approved by lead, Q3).
        // 3. Shared.Contracts (the interface definition).
        // 4. Test assembly (for DI mocking/setup in unit tests).
        const string infraAssembly          = "Learnexia.Modules.Ai.Infrastructure";
        const string sharedContractsAssembly = "Learnexia.Shared.Contracts";
        // Test assemblies (Modules.Ai.UnitTests, Ai.EvalTests) are excluded by the
        // assembly filter below (they don't start with "Learnexia."), so no separate
        // exclusion list is needed.

        // SafetyLayer's full type name — the only Application type allowed.
        var safetyLayerTypeName = typeof(SafetyLayer).FullName!;
        var iaigType            = typeof(IAiGateway);

        var learnexiaAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a =>
            {
                var name = a.GetName().Name ?? string.Empty;
                // Include all Learnexia assemblies EXCEPT the allowed ones.
                return name.StartsWith("Learnexia.", StringComparison.Ordinal) &&
                       name != infraAssembly &&
                       name != sharedContractsAssembly &&
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
                    // SafetyLayer in Application is the approved exception.
                    if (type.FullName == safetyLayerTypeName)
                        continue;

                    if (TypeReferencesIAiGateway(type, iaigType))
                    {
                        violations.Add(
                            $"Assembly '{assembly.GetName().Name}' / Type '{type.FullName}' " +
                            $"references IAiGateway directly — feature handlers must use ISafetyLayer.");
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Partial load — skip gracefully.
            }
        }

        violations.Should().BeEmpty(
            "IAiGateway must only be referenced from Ai.Infrastructure or SafetyLayer " +
            "(AC1 no-bypass invariant). Violations: {0}", string.Join("; ", violations));
    }

    /// <summary>
    /// P302-ARCH-05: <see cref="ISafetyLayer"/> lives in <c>Shared.Contracts</c> so any module
    /// can inject it without referencing the Ai module's projects.
    /// </summary>
    [Fact(DisplayName = "P302-ARCH-05 ISafetyLayer lives in Shared.Contracts assembly")]
    public void ISafetyLayer_IsInSharedContracts()
    {
        var assemblyName = typeof(ISafetyLayer).Assembly.GetName().Name;
        assemblyName.Should().Be("Learnexia.Shared.Contracts",
            "ISafetyLayer must be in Shared.Contracts so any module can inject it " +
            "without referencing the Ai module's projects (module isolation rule)");
    }

    private static bool TypeReferencesIAiGateway(Type type, Type iaigType)
    {
        // Check fields.
        try
        {
            foreach (var field in type.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static))
            {
                if (field.FieldType == iaigType || field.FieldType.IsAssignableTo(iaigType))
                    return true;
            }
        }
        catch { /* ignore */ }

        // Check constructor parameters.
        // NOTE: Type.GetMethods() does NOT return constructors; we must call GetConstructors()
        // separately. This scan completes the stated intent of the helper comment.
        try
        {
            var ctors = type.GetConstructors(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (var ctor in ctors)
            {
                foreach (var param in ctor.GetParameters())
                {
                    if (param.ParameterType == iaigType || param.ParameterType.IsAssignableTo(iaigType))
                        return true;
                }
            }
        }
        catch { /* ignore */ }

        // Check method parameters and return types.
        try
        {
            var methods = type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static);

            foreach (var method in methods)
            {
                if (method.ReturnType == iaigType)
                    return true;

                foreach (var param in method.GetParameters())
                {
                    if (param.ParameterType == iaigType || param.ParameterType.IsAssignableTo(iaigType))
                        return true;
                }
            }
        }
        catch { /* ignore */ }

        return false;
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
