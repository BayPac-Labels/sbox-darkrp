# sbox DarkRP

Local DarkRP workspace for [s&box](https://sbox.game/). Public repo so you can clone it on any PC and keep working.

**Play this project:** open the `DarkRP2` folder (the gamemode).  
`darkrp` is only the empty editor project created first. You can ignore it.

## What you need on each computer

Install these before you clone. s&box is not in this repo.

1. [Steam](https://store.steampowered.com/)
2. **s&box** and **s&box editor** from Steam (Library → Tools, or search `s&box`)
3. [Git](https://git-scm.com/download/win)
4. [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

Docs: [Install s&box](https://sbox.game/dev/doc/getting-started/installation) · [First project](https://sbox.game/learn/facepunch/creating-first-project)

## Install this repo

**Clone (recommended)**

```powershell
git clone https://github.com/BayPac-Labels/sbox-darkrp.git C:\sbox-darkrp
```

**Or ZIP:** on GitHub click **Code → Download ZIP**, extract to `C:\sbox-darkrp`.

## Open it in the editor

The Open dialog wants a **folder**, not a file.

1. Launch **s&box editor** from Steam (Tools) or `sbox-dev.exe`.
2. File → Open, go to `C:\sbox-darkrp`, select the **`DarkRP2`** folder, click Open.
3. Or in File Explorer double-click `C:\sbox-darkrp\DarkRP2\sandbox.sbproj`.
4. Press **Play** or `F5`. Default scene is `scenes/rp_downtown.scene`.

If you are already inside `DarkRP2` in the picker, go up one level and select the folder itself.

## After a fresh clone

The editor rebuilds compiled asset caches (`*_c`, `.sbox`). Those are not on GitHub on purpose. First Play can take a bit while it compiles.

This template last shipped around March 2025. Current s&box uses .NET 10. If Play fails, fix compile errors in the editor first.

## Repo layout

| Path | What it is |
| --- | --- |
| `DarkRP2/` | Customizable DarkRP gamemode (jobs, money, printers, shops, UI). Open this. |
| `darkrp/` | Empty s&box Game project (`local.darkrp`). Not required to play. |
| `SETUP.md` | Extra notes: job files, economy folders, project ident. |

`DarkRP2` is based on the MIT [sousou63/DarkRP](https://github.com/sousou63/DarkRP) template. Identity here is `local.darkrp2` so it is not the workshop package `sousoup/darkrp2`. License: `DarkRP2/LICENSE`.

## Add jobs and content

- Jobs: copy `DarkRP2/Assets/jobs/citizen.jobdef` and edit it
- Job code: `DarkRP2/Code/Jobs`
- Money / printers / shops: `DarkRP2/Code/Economy`
- Items / weapons: `DarkRP2/Code/Items`, `DarkRP2/Code/Weapons`
- UI: `DarkRP2/Code/UI`

More detail: [SETUP.md](SETUP.md)
