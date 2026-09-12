# S&Box DarkRP setup

This folder is separate from BayPac. Two s&box projects live here:

| Folder | What it is | Open this |
| --- | --- | --- |
| [darkrp](darkrp) | Empty **Game** project you created in the s&box editor (`local.darkrp`). Clean shell only. | [darkrp/darkrp.sbproj](darkrp/darkrp.sbproj) |
| [DarkRP2](DarkRP2) | MIT DarkRP 2 template (jobs, money, printers, shops, sandbox UI). This is the customizable gamemode. | [DarkRP2/sandbox.sbproj](DarkRP2/sandbox.sbproj) |

Use **DarkRP2** when you want to add jobs, items, or maps. The empty `darkrp` project is yours to keep or ignore.

The template is yours to change. The original repo is attached only as `upstream` so you can pull reference updates. It is not their workshop package.

## What is already installed

On this machine:

- Steam at `C:\Program Files (x86)\Steam\steam.exe`
- s&box editor at `C:\Program Files (x86)\Steam\steamapps\common\sbox\sbox-dev.exe`
- Git 2.55
- .NET 10 SDK 10.0.401 (s&box requires this)

If Steam ever loses the game or editor: Library → search **s&box** → install both **s&box** and **s&box editor** (Tools). Docs: [Install](https://sbox.game/dev/doc/getting-started/installation), [First project](https://sbox.game/learn/facepunch/creating-first-project), [IDE setup](https://sbox.game/learn/brax/ide-setup).

## Open and play the gamemode

1. Launch **s&box editor** from Steam (Tools) or run `sbox-dev.exe`.
2. **File → Open** and choose `C:\sbox-darkrp\DarkRP2\sandbox.sbproj`.
3. Press **Play** or `F5`.
4. Startup scene is `scenes/rp_downtown.scene`.

The template last shipped around March 2025. Current s&box is .NET 10. If Play fails, the first job is compile errors in the 2026 editor — not new features.

## Project identity

`DarkRP2/sandbox.sbproj` is retargeted so it will not publish as `sousoup/darkrp2`:

- Title: `DarkRP`
- Org: `local`
- Ident: `darkrp2`

Change Org/Ident to your Facepunch org when you are ready to upload. The empty editor project already uses `local.darkrp`, so the template uses `darkrp2` to avoid a clash.

## Where to add things

All of this is under `DarkRP2`. New features are C# components and game resources, not GMod Lua addons.

**Jobs** (citizen, police, mayor, cook, thief, …)

- Definitions: `DarkRP2/Assets/jobs/*.jobdef` — duplicate `citizen.jobdef` and edit Title, Salary, Category, StartingItems, Clothing.
- Code: `DarkRP2/Code/Jobs/JobDefinition.cs`, `JobManager.cs`, `JobVoteManager.cs`

**Money, printers, shops**

- `DarkRP2/Code/Economy/Printers`
- `DarkRP2/Code/Economy/Weapons`
- `DarkRP2/Code/Economy/Ammo`
- `DarkRP2/Code/Economy/MoneyFormatter.cs`

**Items and weapons**

- `DarkRP2/Code/Items`
- `DarkRP2/Code/Weapons`

**Player, NPCs, save, map**

- `DarkRP2/Code/Player`
- `DarkRP2/Code/Npcs`
- `DarkRP2/Code/Save`
- `DarkRP2/Code/Map`

**UI**

- `DarkRP2/Code/UI`

**Scenes**

- `DarkRP2/Assets/scenes/rp_downtown.scene` — default play scene
- `DarkRP2/Assets/scenes/system.scene` — system scene

## Git / pick up on another PC

This whole folder (`darkrp`, `DarkRP2`, `SETUP.md`) is one git repo. The original DarkRP 2 project is MIT: [sousou63/DarkRP](https://github.com/sousou63/DarkRP). Keep `DarkRP2/LICENSE` when you copy or publish.

On the other computer you still need Steam **s&box** + **s&box editor**, Git, and the .NET 10 SDK. Then:

```powershell
git clone https://github.com/BayPac-Labels/sbox-darkrp.git C:\sbox-darkrp
```

Repo (public): https://github.com/BayPac-Labels/sbox-darkrp

Open `C:\sbox-darkrp\DarkRP2` (the folder) or `DarkRP2\sandbox.sbproj` in the s&box editor.

## Installed addons (Wiremod)

DarkRP2 installs **Wirebox + WireLib + Autotool** using the official Wirebox layout (vendored source + junctions), not Workshop packages.

Do **not** add `wiremod.wireboxaddon`, `wiremod.sandboxplus`, or `wiremod.sbox_tool_auto` as `PackageReferences` — those load SandboxPlus / Entity-era APIs and break Facepunch DarkRP2.

| Piece | How it is installed |
|---|---|
| Wirebox | Vendored in `External/wirebox`; Facepunch `ToolMode` port in `DarkRP2/Code/Weapons/ToolGun/Modes/Wire` + `Code/Wirebox` |
| WireLib | `DarkRP2/Libraries/WireLib` (Code junction → External wirelib) |
| Autotool | Scene port in `Code/Weapons/ToolGun/ToolAuto.cs` (`sbox_tool_auto` / attack3) |

After clone, recreate the junctions (once per machine):

```powershell
$root = "D:\Sandbox DarkRP"
cmd /c mklink /J "$root\DarkRP2\Libraries\WireLib\Code" "$root\External\wirebox\wirelib\Code"
cmd /c mklink /J "$root\DarkRP2\Assets\materials\wirebox" "$root\External\wirebox\Assets\materials\wirebox"
cmd /c mklink /J "$root\DarkRP2\Assets\models\wirebox" "$root\External\wirebox\Assets\models\wirebox"
cmd /c mklink /J "$root\DarkRP2\Assets\particles\wirebox" "$root\External\wirebox\Assets\particles\wirebox"
cmd /c mklink /J "$root\DarkRP2\Assets\entity\wirebox" "$root\External\wirebox\Assets\entity\wirebox"
cmd /c mklink /J "$root\DarkRP2\Assets\spawnlists\wirebox" "$root\External\wirebox\Assets\spawnlists"
```

Official SandboxPlus `BaseTool` sources stay in `External/wirebox/Code/wirebox` and are not compiled into DarkRP2.

## Optional IDE

Cursor can edit the C#. For full IntelliSense, open the `.slnx` from the s&box editor (**Project → Open Solution**) in Visual Studio 2026 or Rider. See [IDE setup](https://sbox.game/learn/brax/ide-setup).
