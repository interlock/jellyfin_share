# Jellyfin Media Share — Plugin Package

## Version

Built from `src/JellyfinMediaShare.csproj` `<Version>` field.

## Installation

Copy this directory into your Jellyfin server's plugin folder:

```
cp -r dist ~/.config/jellyfin/plugins/MediaShare/
```

Then restart Jellyfin and enable the plugin from the admin dashboard.

## Rebuilding

```bash
make publish
make package   # produces dist/ with manifest + DLL + deps
```

## Requirements

- Jellyfin 10.11+
- .NET 9.0 runtime on the Jellyfin host
