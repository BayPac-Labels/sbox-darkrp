namespace Sandbox.UI;

/// <summary>
/// Marks a string property as a Wire model path selectable from Wirebox spawnlists.
/// </summary>
[AttributeUsage( AttributeTargets.Property )]
public sealed class WireModelAttribute : Attribute
{
	public string[] SpawnLists { get; }

	public WireModelAttribute( params string[] spawnLists )
	{
		SpawnLists = spawnLists ?? [];
	}
}
