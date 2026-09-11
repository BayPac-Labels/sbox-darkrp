# Addons

Wirebox is installed with the official layout from https://github.com/wiremod/wirebox

- Source checkout: `C:\sbox-darkrp\External\wirebox`
- WireLib junction: `DarkRP2/Libraries/WireLib/Code`
- Asset junctions: `DarkRP2/Assets/{materials,models,particles,entity,spawnlists}/wirebox`

Do not add `wiremod.wireboxaddon` as a cloud package. It loads SandboxPlus and breaks DarkRP2.

Official `Code/wirebox` tools are not compiled here. They still require SandboxPlus (`BaseTool`, `PropHelper`). DarkRP2 lists `ToolMode` in Q → Tools, so the Wire group lives in `Code/Weapons/ToolGun/Modes/Wire`.
