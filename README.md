# DadsStorage

DadsStorage automatically deposits nearby ground items and player inventory items into configured containers. It is a standalone source-built Valheim 1.0.12 port with its own plugin, configuration, YAML, command, RPC, and assembly identity.

## Features

- Automatically moves nearby dropped items into valid containers.
- Stores player inventory items with the `.` shortcut.
- Stores one selected item with middle-click.
- Temporarily pauses automatic storage with `Left Shift + .`.
- Searches nearby containers by holding `Y` while selecting an item or by running `dadsstoragesearch <prefab>`.
- Supports per-container range, excluded items, included items, item groups, and predefined groups through YAML.
- Supports item and slot favorites so protected items are not stored.
- Supports vanilla containers, ItemDrawers, backpacks, WardIsLove, MultiUserChest, and moving containers.
- Detects DadsEPI quick slots directly and can exclude them from player storage.
- Uses synchronized server configuration and version checks for multiplayer.

## Configuration

DadsStorage creates these files without replacing existing files:

- `BepInEx/config/com.dadisbored.dadsstorage.cfg`
- `BepInEx/config/com.dadisbored.dadsstorage.yml`

The CFG controls global behavior, shortcuts, ranges, visual effects, and favorites. The YAML controls storage rules for each container prefab.

## Installation

Install BepInExPack for Valheim, then place `DadsStorage.dll` in a folder under `BepInEx/plugins`. Install the same DadsStorage version on the server and clients for multiplayer.

## Build

Run `.\build.ps1 -Package`. The source is compiled against the Valheim 1.0.12 publicized assemblies, and the Thunderstore ZIP is written to `dist`.

## License

This project is derived from the MIT-licensed AzuAutoStore source. The original copyright and MIT terms are retained in `LICENSE`; DadsStorage modifications are copyright Dad_Is_Bored.
