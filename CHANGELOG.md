# Changelog

All notable changes to this package will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
- Save/Reset config persisted as ScriptableObject in the project
- Flag to delete source textures after baking (default: on)
- Flag to auto-assign generated textures to URP/Lit material slots (default: on)
- Bilinear resize when source textures have mismatched resolutions
- Operates on selected materials in the Project window
