# Changelog

All notable changes to this package will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.2] - 2026-09-07

### Added
- Remap button now works on scene MeshRenderers: when GameObjects with a MeshRenderer are selected, the button reads "Remap Selected MeshRenderers' Materials" and processes all materials used by those renderers. Hint shows "x material(s) on y MeshRenderer(s)". Mixed selections (project materials + scene objects) are supported; shared materials are deduplicated.

## [1.2.1] - 2026-09-07

### Fixed
- Output textures are now saved to the same folder as their source textures (e.g. `_BaseMap.png` lands next to `_Diffuse.jpg`). When multiple sources for one output come from different folders, the first contributing source wins. Auto-assign now uses the exact output paths from the remap step instead of searching by folder, which also fixes assignment after source textures are deleted.

## [1.2.0] - 2026-09-01

### Added
- **Auto-Assign to Materials** is now a full configurable section replacing the simple toggle:
  - Enable/disable toggle
  - Shader picker (ObjectField) — selecting a known preset shader auto-populates the slot mappings
  - **Preset ▾** dropdown to apply a built-in mapping template without needing the shader imported in the project. Supports `Universal Render Pipeline/Lit` and `Shader Graphs/DefaultMasterShader`
  - Editable list of Output Texture → Shader Property slot mappings with add/remove controls
  - Remap now sets `mat.shader` to the configured shader before assigning texture slots
- **Save Config** and **Reset Defaults** buttons pinned to the bottom of the window
- **+ Add Suffix** moved to the bottom of the Suffix section; **+ Add Shader Mapping** at the bottom of the Shader Assign section

### Fixed
- ShaderGraph shaders now correctly resolve via their full registered name (e.g. `Shader Graphs/DefaultMasterShader`) instead of the asset filename

## [1.1.1] - 2026-09-01

### Added
- Preset ▾ dropdown for shader slot mappings (intermediate build — superseded by 1.2.0)

## [1.1.0] - 2026-07-24

### Fixed
- Config no longer resets to defaults on project reopen. Root cause: the ScriptableObject asset stored a Unity script GUID that became invalid once the code moved into a package, causing silent deserialization failure. Replaced with a plain JSON file (`TextureMapperConfig.json`) that has no script reference dependency.

## [1.0.1] - 2026-07-02

### Fixed
- Added Unity-generated `.meta` files to the package so it installs without "no meta file" warnings in immutable folders

## [1.0.0] - 2026-07-01

### Added
- Editor window at `Window → WayExperience → TextureMapper`
- Suffix-based texture detection (`_Diffuse`, `_Albedo`, `_Opacity`, `_Metallic`, `_Roughness`, `_AmbientOcclusion`, `_Occlusion`, `_Normal`)
- Channel remapping into three output textures: `_BaseMap` (sRGB, RGBA), `_MAHS` (linear, R=Metallic G=AO A=Smoothness), `_Normal` (NormalMap type)
- Roughness → Smoothness inversion (`1 - value`) built in
- Configurable output prefix (default `REMAPPED_`) to distinguish generated textures
- Per-suffix channel mapping editor with add/remove controls
- Config persisted as JSON file in the project (`Assets/Editor/WayExperience/TextureMapperConfig.json`)
- Flag to delete source textures after baking (default: on)
- Auto-assign generated textures to material shader slots (default: on)
- Bilinear resize when source textures have mismatched resolutions
- Operates on selected materials in the Project window
