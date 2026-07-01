# WayExperience Texture Mapper

Unity Editor tool that remaps texture channels into URP/Lit-ready packed maps,
driven by filename suffixes. Select materials, click **Remap** — the tool finds
the source textures automatically, bakes them into `_BaseMap`, `_MAHS` and
`_Normal` outputs, and optionally assigns them to the material slots.

---

## Requirements

- Unity 6000.0 or later
- Universal Render Pipeline (URP)

---

## Installation

### Option A — Git URL (recommended for teams)

1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL…**
3. Paste:
   ```
   https://github.com/josefgrunig/texturemapper.git#v1.0.0
   ```
4. Click **Add**

To update to a newer release, replace `v1.0.0` with the desired tag.

### Option B — Edit `manifest.json` directly

Open `Packages/manifest.json` in your project and add the entry inside `"dependencies"`:

```json
{
  "dependencies": {
    "com.wayexperience.texturemapper": "https://github.com/josefgrunig/texturemapper.git#v1.0.0"
  }
}
```

---

## Texture naming conventions

The tool identifies texture types by their filename suffix.
Your textures must follow the pattern `{MaterialName}_{Suffix}.ext`.

| Suffix | Role | Output map | Output channel |
|---|---|---|---|
| `_Diffuse` or `_Albedo` | Base color | `_BaseMap` | RGB |
| `_Opacity` | Transparency | `_BaseMap` | A |
| `_Metallic` | Metallic value | `_MAHS` | R |
| `_Roughness` | Roughness (auto-inverted to Smoothness) | `_MAHS` | A |
| `_AmbientOcclusion` or `_Occlusion` | Ambient occlusion | `_MAHS` | G |
| `_Normal` | Normal map | `_Normal` | RGB |

### Output maps

| Map | Color space | Channels | URP/Lit slot |
|---|---|---|---|
| `{prefix}{MaterialName}_BaseMap.png` | sRGB | RGB = color, A = opacity | Base Map |
| `{prefix}{MaterialName}_MAHS.png` | Linear | R = Metallic, G = AO, A = Smoothness | Metallic Map + Occlusion Map |
| `{prefix}{MaterialName}_Normal.png` | Normal Map | RGB | Normal Map |

Example — material named `Slim_Jeans` with source textures:
```
Slim_Jeans_Diffuse.jpg
Slim_Jeans_Metallic.jpg
Slim_Jeans_Normal.png
```
Produces:
```
REMAPPED_Slim_Jeans_BaseMap.png
REMAPPED_Slim_Jeans_MAHS.png
REMAPPED_Slim_Jeans_Normal.png
```

---

## Usage

1. Open **Window → WayExperience → TextureMapper**
2. Select one or more **materials** in the Project window
3. Configure options (see below)
4. Click **Remap Selected Materials**

Progress and any errors are logged to the Console.

---

## Panel options

| Option | Default | Description |
|---|---|---|
| **Output Texture Prefix** | `REMAPPED_` | Prepended to every generated texture filename |
| **Delete Source Textures** | On | Removes source textures (e.g. `_Diffuse`, `_Metallic`) after baking |
| **Auto-Assign to Materials** | On | Assigns generated textures to the correct URP/Lit material slots automatically |

---

## Customising suffix mappings

Every suffix rule is editable in the panel:

1. Expand a suffix entry to see its settings
2. Change the **Suffix** string, **Output Texture** (`BaseMap` / `MAHS` / `Normal`), and per-channel mappings (`src → dst`)
3. Use **+ Add Suffix** to add new rules or **✕** to remove existing ones
4. Click **Save Config** — settings persist as a ScriptableObject at  
   `Assets/Editor/WayExperience/TextureMapperConfig.asset`
5. Click **Reset Defaults** to restore the built-in rules

Available source channel options include `R`, `G`, `B`, `A` and their
`OneMinusX` inverses (useful for Roughness → Smoothness conversion), plus
the constants `Zero` and `One`.

---

## Texture folder detection

The tool looks for textures automatically by:

1. Checking for a sibling `textures/` folder next to the `materials/` folder  
   (e.g. `Assets/Models/Character/materials/` → `Assets/Models/Character/textures/`)
2. Falling back to any texture already assigned to the material

Textures are matched by prefix — only files whose name starts with the material
name are processed.

---

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
