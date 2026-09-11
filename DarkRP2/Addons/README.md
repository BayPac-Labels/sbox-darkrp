# Addons

Wirebox is installed with the official layout from https://github.com/wiremod/wirebox

- Source checkout: `C:\sbox-darkrp\External\wirebox`
- WireLib junction: `DarkRP2/Libraries/WireLib/Code`
- Asset junctions: `DarkRP2/Assets/{materials,models,particles,entity,spawnlists}/wirebox`

Do not add `wiremod.wireboxaddon` as a cloud package. It loads SandboxPlus and breaks DarkRP2.

Official SandboxPlus `BaseTool` sources under `Addons/wirebox` are not compiled here.
DarkRP2 ports Wire tools as `ToolMode` under `Code/Weapons/ToolGun/Modes/Wire`, with components in `Code/Wirebox`.
