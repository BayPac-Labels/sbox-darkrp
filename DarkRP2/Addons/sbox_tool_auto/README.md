# [Sbox Tool Auto](https://asset.party/wiremod/sbox_tool_auto), an addon for [SandboxPlus](https://github.com/Nebual/sandbox-plus)

Similar to `gmod_tool_auto`, this adds a concommand `sbox_tool_auto` (also bound to "attack3") which cycles the toolgun between appropriate-to-target-entity tools.

For example, middle clicking a Wire Button entity will select the Wire Button tool, then Wiring tool, then Debugger. Middle clicking props opens the Constraint Tool, and then Physgun.

It should generically work for any entity named "ent_something" if its corresponding tool is named "tool_something".  
Alternatively, entities can add the following Property to declare their desired tool(s):

```csharp
public string[] SboxToolAutoTools => new string[] { "tool_constraint" };
```
