# Addons

Wiremod install for DarkRP2 follows the official Wirebox layout from https://github.com/wiremod/wirebox, adapted for Facepunch Sandbox (`ToolMode`).

## Installed

| Piece | Location |
|---|---|
| **Wirebox** (source + assets) | `External/wirebox` |
| **WireLib** | `Libraries/WireLib` (Code junction → `External/wirebox/wirelib/Code`) |
| **Autotool** | `Code/Weapons/ToolGun/ToolAuto.cs` (`sbox_tool_auto` / attack3) |
| Asset junctions | `Assets/{materials,models,particles,entity,spawnlists}/wirebox` |

Gameplay Wire tools/components are the Facepunch port under `Code/Weapons/ToolGun/Modes/Wire` and `Code/Wirebox`. Official SandboxPlus `BaseTool` sources stay in `External/wirebox/Code/wirebox` and are **not** compiled here.

## Do not add as cloud packages

- `wiremod.wireboxaddon` — pulls SandboxPlus and breaks DarkRP2 (`Player` clash)
- `wiremod.sbox_tool_auto` — Entity-era; replaced by the Scene port above
- `wiremod.sandboxplus`

## Junctions (once per machine)

```powershell
$root = "D:\Sandbox DarkRP"
cmd /c mklink /J "$root\DarkRP2\Libraries\WireLib\Code" "$root\External\wirebox\wirelib\Code"
cmd /c mklink /J "$root\DarkRP2\Assets\materials\wirebox" "$root\External\wirebox\Assets\materials\wirebox"
cmd /c mklink /J "$root\DarkRP2\Assets\models\wirebox" "$root\External\wirebox\Assets\models\wirebox"
cmd /c mklink /J "$root\DarkRP2\Assets\particles\wirebox" "$root\External\wirebox\Assets\particles\wirebox"
cmd /c mklink /J "$root\DarkRP2\Assets\entity\wirebox" "$root\External\wirebox\Assets\entity\wirebox"
cmd /c mklink /J "$root\DarkRP2\Assets\spawnlists\wirebox" "$root\External\wirebox\Assets\spawnlists"
```
