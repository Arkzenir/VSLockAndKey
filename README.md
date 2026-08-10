# LockAndKey

Adds a key requirement on top of Vintage Story's existing block reinforcement/locking
system. Locking a block to yourself or to a group still works exactly like vanilla —
this mod adds one rule on top: **to interact with a locked block, you must possess a
matching key somewhere in your inventory**, even if you're the lock's owner or a
member of the owning group.

## Features

- **Key gate on locked blocks.** Owning or being in the group of a locked block is no
  longer enough on its own — you also need a key bound to that specific player or
  group. No key, no access, even for the owner.
- **Keys work for anyone who has one.** A key is a physical credential: if a stranger
  ends up with a matching key (lent, found, stolen), it opens the lock for them too.
  There's no separate "is this really your lock" check beyond possessing the key.
- **Optional durability penalty for unauthorised use.** Keys can have limited
  durability that only drains when used by someone who *isn't* the lock's actual
  owner/group member — legitimate use never wears a key down.
- **Key Files, for binding keys to a target.** Hold a key in your main hand and a key
  file of equal-or-higher metal tier in your offhand to open a binding dialog, where
  you pick yourself or one of your groups as the key's target. A bronze file only
  needs to be bronze tier or better to file a bronze key, and can file a steel key
  too, as long as the file's own tier is high enough. Files wear down with use (real
  durability, breaks when spent) but aren't consumed outright by a single filing.
- **Keyrings.** A holdable container that accepts only keys — right-click it (with
  nothing targeted) to open its own inventory screen and manage its contents. Keys
  inside a keyring you're carrying anywhere in your inventory work exactly as if
  they were loose, whether or not the keyring's screen is currently open. Capacity
  scales with the keyring's material.
- **Admin bypass and per-lock exemptions**, configurable — see below.
- **Metal variants matching lock materials**: bronze (any of the three real alloys),
  iron, meteoric iron, steel — for keys, key files, and metal keyrings alike. Keys
  also have an optional gem-studded finish, purely cosmetic.
- **Ground-storable.** Keys, key files, and keyrings can all be placed on the ground
  (shift+right-click) rather than only ever sitting in inventory or dropped loose.

## How it works, in practice

1. **Lock a block as normal.** Vanilla reinforcement/locking is untouched — reinforce
   and lock to yourself or to a group exactly like you always have.
2. **Forge or cast a key** of any metal (see Crafting below), then **file it** to
   the player or group the lock belongs to: hold the key in your main hand, a
   sufficient-tier key file in your offhand, right-click in the air, pick a target
   from the dropdown, confirm.
3. **Carry the filed key** (loose in inventory, or inside a keyring you're carrying
   — right-click the keyring to open it and drop keys in) whenever you want to open
   that lock. Without it, even the lock's owner is turned away.
4. If `LimitUnauthorisedUse` is on, a key used by someone other than the lock's
   actual owner/group member loses one use each time; at zero it breaks (with the
   normal item-break sound and particles). Used by the rightful owner/group member,
   it never wears down.

## Crafting

- **Anvil smithing**: heat an ingot (bronze/iron/meteoric iron/steel), work it on the
  anvil into a key, a key file, or a metal keyring. A bulk recipe produces 4 keys from
  one smithing pass, using a bit under 2 ingots' worth of material rather than a full
  4x cost.
- **Casting**: shape clay into a key mold, fire it in a kiln, then pour molten metal
  from a crucible to cast a key — same reusable-mold pattern as vanilla ingot/tool
  molds (the mold survives the pour and can be reused). A bulk mold variant casts 4
  keys per pour.
- **Gem-studding**: hammer + chisel + a cut gem + an already-forged/cast key of the
  matching metal produces the gem-studded variant. Purely cosmetic.
- **Rope keyring**: rope, in a simple grid recipe (the only keyring that isn't
  smithed).

## Configuration

Written to `ModConfig/vslockandkey.json` on first run (per installation — the
default only applies the first time; edit the file directly to change it later).

| Option | Default | Meaning |
|---|---|---|
| `AdminBypassKeyRequirement` | `true` | Players with the `commandplayer` privilege skip the *key* requirement — they still need normal vanilla owner/group access to the lock itself. |
| `AdminBypassPrivilege` | `commandplayer` | Which privilege counts as "admin" for the bypass above. |
| `ExemptPlayerUids` | *(empty)* | Player UIDs whose own locks never require a key. |
| `ExemptGroupNames` | *(empty)* | Group names whose locks never require a key. |
| `LimitUnauthorisedUse` | `true` | If true, keys have finite durability that only drains on unauthorised use (see above). If false, keys never take damage. |
| `KeyDurability` | `3` | Uses before an unauthorised-use key breaks, when `LimitUnauthorisedUse` is on. |
| `GroupFilingRequiresOwnerOrOp` | `true` | If true, filing a key to a Group target requires the filer to hold Owner or Op rank in that group. |
| `ShowKeyBindingInfo` | `true` | If true, key tooltips show their "Bound to: X" / "Not yet filed" line. |

## Status

Actively in development, under live in-game testing. Gameplay logic, recipes, config,
and models/textures are all implemented and build cleanly (0 warnings) for both
supported game versions. All six shapes are real modeled geometry, wired to real
vanilla metal/clay/gem textures, with hand/GUI-icon/ground orientation confirmed
correct in-game. Not expected to be compatible with other mods that alter vanilla
locking if both are installed together.

The keyring's own inventory dialog is a right-click-to-open custom dialog backed by
vanilla's own `InventoryGeneric`/container-slot networking, not the equip-slot-based
`IHeldBag` system (which doesn't work for a plain carried item — see `PLANNING.md`).
Its interactive plumbing (open/close, drag in/out, concurrent sessions) hasn't had a
full in-game pass yet. Ground-storage placement for keys/files/keyrings is likewise
implemented but not yet visually confirmed in-game.

See `PLANNING.md` for the full design brief, implementation notes, and known open
items.

## Building

Requires the .NET SDK and a Vintage Story install. Two game versions are supported
from one shared `source/` tree — no conditional compilation, both `.csproj` files
build the same code.

1. Copy `Properties/localSettings.props.template` to `Properties/localSettings.props`
   and set your game install path(s) — `GameDirectory_1_22_5` and/or
   `GameDirectory_1_21_5`, whichever you're building for.
2. Build:

```
dotnet build VSLockAndKey_1.22.5.csproj                # VS 1.22.5, debug
dotnet build VSLockAndKey_1.22.5.csproj -c Release     # VS 1.22.5, release + zip

dotnet build VSLockAndKey_1.21.5.csproj                # VS 1.21.5, debug
dotnet build VSLockAndKey_1.21.5.csproj -c Release     # VS 1.21.5, release + zip
```

Debug output lands in `bin/Debug/{version}/Mods/vslockandkey/`. Add the `Mods/` folder
above it to the game's ModPaths for live asset reloading (JSON/texture/sound edits
apply without rebuilding; C# changes and item/block registration changes like
`storageFlags` need a restart or rejoin to take effect).

Release output lands in `Releases/vslockandkey_<version>_vs{1.22.5,1.21.5}.zip`,
ready for the mod portal.

## Layout

| Path | Contents |
|---|---|
| `source/` | C# source — config, Harmony patch, items, keyring inventory GUI/networking, filing GUI/networking |
| `resources/assets/vslockandkey/` | Block/item types, recipes, lang, shapes |
| `Directory.Build.props` | Mod identity — name, id, version, description, side, authors |
| `Common.Build.targets` | Shared build logic |
| `VSLockAndKey_1.22.5.csproj` / `VSLockAndKey_1.21.5.csproj` | One per supported game version |
| `deps/` | Vendored dependency DLLs, per game version |
| `external/` | Ad-hoc DLLs, auto-referenced |

`modinfo.json` is generated by the build from `Directory.Build.props`. Do not create
one by hand.

## Licence

Add your licence here.
