# WayExperience Texture Mapper

Unity Editor tool that remaps texture channels into packed maps driven by
filename suffixes. Select materials, click **Remap** — the tool finds source
textures automatically, bakes them into `_BaseMap`, `_MAHS` and `_Normal`
outputs, assigns the configured shader, and wires up the texture slots.

---

## Requirements

- Unity 6000.0 or later
- Universal Render Pipeline (URP) — or any custom shader via the shader assign config

---

## Installation

### Option A — Git URL (recommended for teams)

1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL…**
3. Paste:
   ```
   https://github.com/josefgrunig/texturemapper.git#main
   ```
4. Click **Add**

This always tracks the latest stable release. To update, click **Update** in Package Manager whenever a new version is available.

To pin to a specific release instead, use a version tag:
   ```
   https://github.com/josefgrunig/texturemapper.git#v1.2.0
   ```

### Option B — Edit `manifest.json` directly

Open `Packages/manifest.json` in your project and add the entry inside `"dependencies"`:

**Latest stable (recommended):**
```json
{
  "dependencies": {
    "com.wayexperience.texturemapper": "https://github.com/josefgrunig/texturemapper.git#main"
  }
}
```

**Pinned to a specific version:**
```json
{
  "dependencies": {
    "com.wayexperience.texturemapper": "https://github.com/josefgrunig/texturemapper.git#v1.2.0"
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

| Map | Color space | Channels |
|---|---|---|
| `{prefix}{MaterialName}_BaseMap.png` | sRGB | RGB = color, A = opacity |
| `{prefix}{MaterialName}_MAHS.png` | Linear | R = Metallic, G = AO, A = Smoothness |
| `{prefix}{MaterialName}_Normal.png` | Normal Map | RGB |

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

### General

| Option | Default | Description |
|---|---|---|
| **Output Texture Prefix** | `REMAPPED_` | Prepended to every generated texture filename |
| **Delete Source Textures** | On | Removes source textures (e.g. `_Diffuse`, `_Metallic`) after baking |

### Suffix → Channel Mappings

Defines how each source texture suffix is baked into the output maps.

- Expand any entry to edit its **Suffix**, **Output Texture** (`BaseMap` / `MAHS` / `Normal`), normal map flag, and per-channel rules (`src → dst`)
- Available source options: `R`, `G`, `B`, `A`, `OneMinusR/G/B/A` (useful for Roughness inversion), `Zero`, `One`
- Use **+ Add Suffix** at the bottom of the section to add new rules, **✕** to remove

### Auto-Assign to Materials

Controls whether and how generated textures are wired to the material after remapping.

| Option | Default | Description |
|---|---|---|
| **Enabled** | On | Assigns textures and sets the material shader after remapping |
| **Shader** | URP/Lit | Target shader — set via ObjectField or **Preset ▾** dropdown |
| **Texture → Shader Property** | (preset) | Maps each output texture to a shader property name |

#### Built-in shader presets

| Preset | Mappings |
|---|---|
| `Universal Render Pipeline/Lit` | BaseMap→`_BaseMap`, MAHS→`_MetallicGlossMap`, MAHS→`_OcclusionMap`, Normal→`_BumpMap` |
| `Shader Graphs/DefaultMasterShader` | BaseMap→`_BaseMap`, MAHS→`_MAHS`, Normal→`_BumpMap` |

Selecting a preset via the **Preset ▾** dropdown clears existing mappings and populates the defaults for that shader. The preset also works when the shader is not yet imported in the project.

Use **+ Add Shader Mapping** to add extra slot assignments, **−** to remove.

### Bottom bar

| Button | Action |
|---|---|
| **Reset Defaults** | Restores all suffix configs and shader assign settings to built-in defaults |
| **Save Config** | Persists current settings to `Assets/Editor/WayExperience/TextureMapperConfig.json` |

> Config is also saved automatically whenever any field is changed.

---

## Texture folder detection

The tool locates textures automatically by:

1. Checking for a sibling `textures/` folder next to the `materials/` folder  
   (e.g. `Assets/Models/Character/materials/` → `Assets/Models/Character/textures/`)
2. Falling back to any texture already assigned to the material

Textures are matched by prefix — only files whose name starts with the material name are processed.

---

## Notes

- Remap is **manual only** — it runs when you press the button. There is no automatic trigger on FBX or texture import.
- All output textures are always saved as **PNG** (supports alpha channel required by `_BaseMap` and `_MAHS`).
- When source textures have different resolutions, the largest is used as the output resolution and others are bilinearly upscaled.

---

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
