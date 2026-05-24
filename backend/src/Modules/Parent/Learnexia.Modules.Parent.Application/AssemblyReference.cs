namespace Learnexia.Modules.Parent.Application;

/// <summary>
/// Marker type for assembly scanning. Used by the Host's cross-module MediatR registration
/// (RegisterServicesFromAssemblyContaining&lt;AssemblyReference&gt;) and by AddParentApplication
/// for validator/AutoMapper discovery. Mirrors Learning's AssemblyReference.
/// </summary>
public class AssemblyReference
{
}
