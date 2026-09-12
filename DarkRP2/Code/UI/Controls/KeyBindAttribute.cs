namespace Sandbox;

/// <summary>
/// Marks a string property as a raw keyboard bind edited via click-to-bind UI.
/// </summary>
[AttributeUsage( AttributeTargets.Property )]
public sealed class KeyBindAttribute : Attribute
{
}
