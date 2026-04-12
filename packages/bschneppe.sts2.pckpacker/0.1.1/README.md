# StS2 PCK Packer

Standalone PCK packer for Slay the Spire 2 mods. Produces Godot 4.5 format-v3 `.pck` files **without requiring the Godot editor**.

## Why?

STS2 mods need a `.pck` file containing localization JSON and card art. Normally this requires the Godot editor (or MegaDot fork) — a heavy dependency when your actual mod code is just a `.dll`. This package runs as part of `dotnet build` and handles everything automatically.

## Quick Start

Add the NuGet package to your mod project:

```xml
<PackageReference Include="BSchneppe.StS2.PckPacker" Version="*" PrivateAssets="All" />
```

That's it. On build, any assets in `YourModName/` will be packed into `YourModName.pck` in the output directory.

### Expected directory structure

```
YourMod/
├── YourMod.csproj
├── YourMod.json
├── YourMod/                    ← asset directory (same name as project)
│   ├── localization/
│   │   └── eng/
│   │       └── cards.json      ← passed through as-is
│   └── images/
│       └── card_portraits/
│           ├── card.png         ← converted to .ctex
│           └── big/
│               └── card.png     ← converted to .ctex (unique hash)
```

### Copying the PCK to the mods directory

The `PackPck` target runs after `Build`. To copy the generated PCK alongside your DLL and manifest, use a target that runs after `PackPck` and conditionally copies the PCK:

```xml
<Target Name="CopyModFiles" AfterTargets="PackPck">
  <Copy SourceFiles="$(OutputPath)$(AssemblyName).dll"
        DestinationFolder="$(STS2ModsDir)/$(AssemblyName)" />
  <Copy SourceFiles="$(AssemblyName).json"
        DestinationFolder="$(STS2ModsDir)/$(AssemblyName)" />
  <Copy Condition="Exists('$(OutputPath)$(AssemblyName).pck')"
        SourceFiles="$(OutputPath)$(AssemblyName).pck"
        DestinationFolder="$(STS2ModsDir)/$(AssemblyName)" />
</Target>
```

> **Note:** `PackPck` runs after `Build`, and `CopyModFiles` runs after `PackPck`, so the ordering is: Build → PackPck → CopyModFiles. The PCK copy is conditional so the build still succeeds if packing was skipped (e.g. unsupported files, or no asset directory).

## What it does

| Source file | In the PCK | Conversion |
|---|---|---|
| `.png` | `.ctex` + `.import` remap | PNG → lossless WebP with GST2 header |
| `.json` | as-is | None |
| `.tres`, `.cfg`, etc. | as-is | None |
| `.tscn`, `.gdshader`, `.gdscript` | **not supported** | Build skips — use Godot export |

### PNG conversion details

PNGs are converted to Godot's CompressedTexture2D format (`.ctex`): a 56-byte GST2 header + lossless WebP payload. Import remap files (`.import`) are generated so Godot's resource loader finds the textures at their original `res://` paths.

Filenames use the same MD5-hashed naming as Godot's real importer (`filename.png-{md5}.ctex`), so duplicate filenames in different directories are handled correctly.

## MSBuild Properties

All properties are optional — defaults work for standard mod layouts.

| Property | Default | Description |
|---|---|---|
| `PckPackerEnabled` | `true` | Set to `false` to disable PCK packing |
| `PckPackerSourceDir` | `$(MSBuildProjectName)/` | Directory containing assets to pack |
| `PckPackerResPrefix` | `$(MSBuildProjectName)` | `res://` prefix for paths inside the PCK |
| `PckPackerOutputPath` | `$(OutputPath)$(MSBuildProjectName).pck` | Output `.pck` file path |

### Example: custom asset directory

```xml
<PropertyGroup>
  <PckPackerSourceDir>assets/</PckPackerSourceDir>
  <PckPackerResPrefix>MyMod</PckPackerResPrefix>
</PropertyGroup>
```

### Disabling for specific builds

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <PckPackerEnabled>false</PckPackerEnabled>
</PropertyGroup>
```

## Unsupported file fallback

If the asset directory contains files that require Godot compilation (`.tscn`, `.gdshader`, `.gdscript`, `.gdextension`), the packer **skips gracefully** instead of producing a broken PCK. The build succeeds with a warning message, and `PckPackerSkipped` is set to `true`.

This enables a fallback pattern in mod templates: try the PCK packer first, fall back to Godot export if it can't handle the assets.

## Library API

The packer can also be used programmatically:

```csharp
using StS2PckPacker;

var result = AssetPacker.Pack(
    sourceDir: "MyMod/",
    resPrefix: "MyMod",
    outputPath: "MyMod.pck"
);

if (result.Success)
    Console.WriteLine("Packed!");
else
    Console.WriteLine($"Skipped: {string.Join(", ", result.UnsupportedFiles)}");
```

Lower-level APIs are also available: `CtexConverter.Convert()`, `ImportRemapGenerator.Generate()`, `PckWriter.Write()`.

## PCK format compatibility

Output matches Godot 4.5's real export format:
- 112-byte header (GDPC magic, format v3, engine 4.5.1)
- `PACK_REL_FILEBASE` flag (`0x02`)
- 32-byte aligned file data
- No `res://` prefix in directory paths
- MD5 checksums per file

Verified against the game's own PCK and BaseLib.pck.
