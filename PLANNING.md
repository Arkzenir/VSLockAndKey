# LockAndKey — Planning Brief

Mod: `VSLockAndKey` (modid `vslockandkey`). Target: VS 1.22.5, net10.0.

## 1. What this mod does not touch

Vanilla reinforcing/locking (`ModSystemBlockReinforcement`, the `Reinforcable` and
`Lockable` block behaviors, padlock items) is untouched. Players keep locking blocks
to themselves or to a chat-group exactly as today. This mod adds one thing on top:
**possessing a matching key becomes a second, independent gate on interacting with a
locked block**, alongside the existing owner/group check.

## 2. Vanilla mechanics this builds on (read from `gamesrc/survival-1.22.5`)

- `Systems/BlockReinforcement.cs` — `ModSystemBlockReinforcement` stores one
  `BlockReinforcement` record per reinforced block position: `PlayerUID` (owner) XOR
  `GroupUid` (owning chat-group), plus `Locked`. `GetReinforcment(pos)` is a public
  method — no reflection or Harmony needed to read it.
- `IsLockedForInteract(pos, player)` is vanilla's own gate: true (blocked) unless the
  player is the owner, is in the owning group, or has been granted access via `bre`
  grant commands.
- `BlockBehavior/BehaviorLockable.cs` — the `Lockable` block behavior calls
  `IsLockedForInteract` from `OnBlockInteractStart` and blocks the interaction if true.
- "Group" in this spec = VS's existing chat-group system (`IPlayer.GetGroup`,
  `PlayerGroup`, `EnumPlayerGroupMemberShip.Owner/Op`) — the same groups vanilla
  locking already uses. No new group concept is introduced.

**Integration approach: Harmony, patching `ModSystemBlockReinforcement.IsLockedForInteract`.**
`0Harmony.dll` ships in `$(GameDirectory)/Lib/` and is already referenced by every
workspace's `Common.Build.targets` — no vendoring needed. The
[LocksAffectReinforcement](../LocksAffectReinforcement/mod/source/TryLockPatch.cs)
workspace already patches a neighboring method (`TryLock`) on this exact class with
this exact pattern, so this is a proven, low-risk technique in this harness.

`IsLockedForInteract(pos, forPlayer)` is the single choke point every lockable block
type routes through (`BlockBehaviorLockable.OnBlockInteractStart` calls it directly;
so does anything else — vanilla or third-party — that checks whether a position is
locked for a player). Patching it directly, rather than inserting a competing block
behavior via JSON, sidesteps the whole question of block-behavior chaining order and
whether `PreventSubsequent`/door-vs-chest opening logic actually cooperates — the
patch runs *inside* the one method everything already calls, so there's no chain to
get right per block type. This is the reliability-first choice for a mod meant to run
correctly across many block types on busy servers.

`Prefix` on `IsLockedForInteract`, short-circuiting with `__result`:

- Not reinforced/not locked (`GetReinforcment(pos)` is null or `!Locked`) → return
  `true` (run vanilla logic unchanged; `__result` stays whatever vanilla computes).
- Locked, and the *owning* player/group is on the exempt allowlist → return `true`
  (vanilla logic decides, unmodified — no key needed for this lock).
- Locked, caller has `AdminBypass` privilege and the config allows it → return `true`
  (vanilla logic decides — normal owner/group rules still apply, just no *extra* key
  requirement on top).
- Locked otherwise → look for a key in the caller's inventory (main inventory +
  equipped keyring — never hotbar-only) bound to `bre.PlayerUID` or `bre.GroupUid`.
  - No matching key → set `__result = true` (locked for interact), return `false`
    (skip vanilla entirely) — blocks even the legitimate owner/group member who
    lacks a key.
  - Matching key found → set `__result = false` (not locked for interact), return
    `false` — grants access *even to a stranger who is neither the owner nor a group
    member*, since the key alone is the credential. If `LimitUnauthorisedUse` is on
    and the caller is not the owner/group member, damage the key by one durability
    point on this use.

Same `Postfix`-vs-`Prefix` shape needed on `BlockBehaviorLockable.OnBlockInteractStart`
is *not* required — that method already calls `IsLockedForInteract` and reacts to its
result, so patching the one upstream method is sufficient for every block that goes
through it. Only if some block type is found to check lock state through a different
path would a second, narrower patch be needed — to be confirmed empirically while
building against real doors/chests, same as any Harmony patch in this ecosystem.

Branching note: this patch's target (`ModSystemBlockReinforcement.IsLockedForInteract`)
has stayed stable across recent versions per the vanilla source checked in
`gamesrc/survival-1.22.5`, but Harmony patches are inherently pinned to a method's
signature. Per your call, separate major-version branches will carry their own
verified patch if the signature ever moves — no cross-version abstraction needed now.

## 3. Config (`ModConfig`, JSON in `resources/assets/vslockandkey/config/`)

| Option | Type | Meaning |
|---|---|---|
| `AdminBypassKeyRequirement` | bool | Server OPs (`commandplayer` privilege) skip the key check entirely (they still need normal vanilla owner/group access — this only waives the *extra* key requirement). |
| `ExemptPlayerUids` | string[] | Lock owners in this list don't need a key for their own locks. |
| `ExemptGroupNames` | string[] | Locks owned by these groups don't need a key. |
| `LimitUnauthorisedUse` | bool | If true, keys have finite durability that only drains when the user is *not* the lock's owner/group member. If false, keys never take durability damage. |
| `KeyDurability` | int | Max uses before an unauthorised-use key breaks, when `LimitUnauthorisedUse` is true. |
| `GroupFilingRequiresOwnerOrOp` | bool | If true, filing a key to a Group target requires the filer to hold Owner or Op membership level in that group (mirrors vanilla's own re-lock gate in `TryLock`). |

## 4. Items

### 4.1 Key (`item/key-*`)

- Variant item, metal materials matching lock/padlock materials: bronze, iron,
  meteoric iron, steel. "Bronze" is one *gating* tier but three real vanilla item
  variant states — `tinbronze`, `bismuthbronze`, `blackbronze` — each keeping its own
  correct texture via the `{metal}` wildcard, matching how `examples/Thievery`'s own
  keys work; `MetalTier.RankOf` (C#) ranks all three equally so file/key tier checks
  treat them as one tier while the visuals stay accurate to whatever alloy was
  actually forged/cast. (Mirrors `TankardsandGoblets`' `variantgroups` pattern —
  `{ code: "metal", states: [...] }`.) Plus an optional `jewel` variant group for a
  gem-studded finish (purely cosmetic, no mechanical effect), same pattern as
  `goblet-jeweled`'s `texturesByType` reusing `game:block/metal/plate/{metal}*` /
  `game:block/stone/jewels/{jewel}*` — no new per-metal textures to draw.
- Key material and lock material are independent (a bronze key can bind to a
  steel-locked block) — the item's own metal variant is cosmetic/flavor only; the
  match is by bound UID, not material.
- Attributes stored on the itemstack: `boundPlayerUid` (string, nullable) XOR
  `boundGroupId` (int, nullable), `boundName` (string, cached display name for
  tooltip), and `durability` (int, only meaningful when `LimitUnauthorisedUse`).
  Unbound (freshly forged/cast) keys have neither set and do nothing until filed.
- Forged on the anvil: one shared voxel-smithing shape for all metal variants (no
  per-metal recipe geometry needed), plus a bulk recipe that turns one heated
  ingot-equivalent into 4 keys at once.
- Cast from liquid metal via a reusable clay mold (same pattern as vanilla ingot/
  toolhead molds: shape wet clay into a key mold, fire it, then pour molten metal
  from a crucible repeatedly — the mold itself never breaks).

### 4.2 Key File (`item/keyfile-*`)

- Same four metals as keys, plain only (no gem variant — it's a tool, not a keepsake).
- Filing rule: file material tier must be ≥ key material tier. Tier order:
  bronze < iron < meteoriciron < steel (matches vanilla's own tool-tier ordering).
- Usage: file in offhand, unbound (or rebindable) key in main hand → opens a custom
  GUI dialog: a dropdown listing the player's own name plus every chat-group they
  belong to, and a Confirm button. Confirming writes `boundPlayerUid` or
  `boundGroupId` (+ `boundName`) onto the key itemstack. Rebinding an already-bound
  key is allowed (re-filing overwrites the old binding) unless we're told otherwise.
- If `GroupFilingRequiresOwnerOrOp` is on, group entries the filer only holds plain
  Member rank in are hidden from (or disabled in) the dropdown.
- Files are tools, not consumed by filing; no durability loss from filing itself
  (only from whatever normal tool-durability vanilla tools already have, if any).

### 4.3 Keyring (`item/keyring-*`, or a `BlockEntityContainer`-backed item)

- Right-click opens a dedicated inventory GUI that accepts only Key items.
- Keys inside a keyring the player is carrying (anywhere in inventory, not
  necessarily hotbar) count exactly as if they were loose in the inventory for every
  key check above.
- Capacity scales with keyring material, worst → best:
  `rope < copper|tin-bronze/bismuth-bronze/black-bronze (bronze tier) < iron <
  meteoriciron < steel` — exact slot counts TBD at build time (proposal:
  rope=4, bronze=8, iron=12, meteoriciron=16, steel=20 — open to adjustment).

## 5. Visuals

Keys, key files, and keyrings all get correctly-named placeholder shape (`shapes/
item/...`) and texture (`textures/item/...`) files with no actual model/pixel data —
matching `TankardsandGoblets`' approach of reusing existing vanilla metal textures via
`texturesByType`/`{metal}` wildcards rather than shipping a texture per metal. The
user will replace shapes/textures later.

## 6. Decisions already made (confirmed by user)

- Crafting: anvil voxel-smithing, one shared shape across all metal variants, plus a
  bulk recipe producing 4 keys per smithing pass.
- Casting: reusable clay mold (same as vanilla ingot/toolhead molds), not a one-shot
  mold.
- `GroupFilingRequiresOwnerOrOp` gates on the *group's* Owner/Op rank, not a
  server-wide OP privilege.
- A matching key is sufficient on its own to open a lock, even for a player who is
  neither the owner nor a group member — that's the entire point of
  `LimitUnauthorisedUse`'s durability drain (it only fires for exactly this
  stranger-with-a-key case).
- Re-filing is unrestricted: anyone holding a sufficient-tier Key File can rebind any
  key (bound or not) to a new player/group target. Keys carry no ownership of their
  own, only whatever they're currently bound to — mirrors vanilla padlocks/
  reinforcement having no "who owns this item" concept, only "who owns this lock".

- Integration is via Harmony (`0Harmony.dll`, already available from
  `$(GameDirectory)/Lib/`), patching `ModSystemBlockReinforcement.IsLockedForInteract`
  directly rather than JSON-patching block behavior order — prioritises reliability
  across every lockable block type on a busy multi-mod server over avoiding a
  dependency that turned out to already be free. Not expected to be compatible with
  other mods that alter locking (e.g. Thievery) if both are installed together —
  acceptable, per your call.

## 7. Still open / to confirm before or during build

- Exact keyring capacity numbers per material (proposal above, adjustable).
- Confirm empirically, once a locked door/chest exists in a live test world, that no
  vanilla block type checks lock state through a path other than
  `IsLockedForInteract` (expected: none do, per `gamesrc/survival-1.22.5`).

## 8. Implementation status (first pass)

Built and compiling clean (0 errors) against the harness's Debug target:

- Harmony patch on `IsLockedForInteract`, config, and the key-matching/durability
  helper (`Util/KeyAccessUtil.cs`) are in place per section 2/3.
- `ItemKey`, `ItemKeyFile`, `ItemKeyring` classes exist; filing is a real client GUI
  (`Gui/GuiDialogKeyFile.cs`) backed by a server-validated network packet
  (`Network/Packets.cs`, handled in `VSLockAndKeyModSystem.OnBindKeyPacket`).
- Keyring uses vanilla's own held-bag system (`Behaviors/CollectibleBehaviorKeyring.cs`
  implementing `IHeldBag`) rather than a custom dialog — carrying one anywhere in your
  inventory surfaces its slots directly in the character inventory screen, matching
  "if you have it on you, it works" for free, restricted to `ItemKey` stacks only.
- Casting reuses vanilla's `BlockToolMold`/`ToolMold` classes directly via JSON
  (`blocktypes/keymold.json`) — zero new C# for the mold block itself, following the
  reusable-clay-mold decision from section 6.
- Bug found and fixed (twice — see the correction below): the `{metal}`/`{material}`
  texture wildcard used by `key`, `key-jeweled`, `keyfile`, and `keyring` resolves
  the literal variant state name into a vanilla texture path
  (`game:block/metal/ingot/{metal}`). That's correct for real vanilla ingot texture
  names but breaks for anything that isn't one. My first pass curated a single
  generic `bronze` state (matching how you described lock materials) and only
  caught at review time that vanilla has no generic "bronze" ingot texture, only
  per-alloy ones (`tinbronze`/`bismuthbronze`/`blackbronze`) — every bronze variant
  would have rendered with a missing-texture checkerboard. My first attempted fix
  (a hardcoded texture override for the generic `bronze` state) was the wrong
  correction: you pointed out bronze should keep the tier gating in code but show
  each alloy's *real* texture, exactly like `examples/Thievery`'s own keys do. Final
  fix: `bronze`, `iron`, `meteoriciron`, `steel` as a concept became six real item
  variant states — `tinbronze`, `bismuthbronze`, `blackbronze`, `iron`,
  `meteoriciron`, `steel` — across `key`, `key-jeweled`, `keyfile`, and `keyring`,
  each resolving its own correct texture via the plain `{metal}`/`{material}`
  wildcard with no override needed. `MetalTier.cs` ranks the three bronze alloys
  equally (rank 0) so file-tier and key-tier gating still treats them as one tier,
  per the original spec. This also simplified every recipe: since our own items no
  longer have a fixed-code "bronze" exception, every smithing/clayforming/grid
  recipe collapsed from a bronze/iron-tier pair back down to one file each, using
  `allowedVariants` across all six real metals and plain `{metal}` substitution
  throughout — no more fixed-output-code special case anywhere.
- `key-jeweled` now has one consolidated grid recipe set (`recipes/grid/
  key-jeweled.json`, 5 entries covering 9 of its 12 jewel states) adapted from
  `examples/TankardsandGoblets`' real `jeweledgoblet.json` grid recipes (per your
  pointer), nail ingredient dropped, pattern shrunk to a 2x2 hammer/chisel/gem/key
  square. `onyx`, `ruby`, `turquoise` are left without a recipe (creative-inventory
  only), matching Tankards and Goblets' own goblet-jeweled recipes, which don't
  craft those three either — presumably no confirmed vanilla item code for them.
  Per your correction, the recipe consumes a **plain key of the matching metal**
  (`vslockandkey:key-*`, all six real alloy/metal states) instead of a raw ingot —
  gem-studding is now something you do to an already-forged/cast key, not an
  alternate way to make one from scratch.
- The anvil grid (`SmithingRecipe`/`ClayFormingRecipe`, both `LayeredVoxelRecipe`) is
  hard-capped at 16 wide x 16 long x `QuantityLayers` tall (`LayeredVoxelRecipe.
  GenVoxels` throws `InvalidOperationException` past that) — confirmed from
  `api/vsapi/Common/Crafting/LayeredVoxelRecipe.cs`, not just convention. The bulk
  key smithing pattern and the new bulk key-mold clayforming pattern
  (`keymold-bulk`, a second `size` variant on the mold block, `requiredUnits: 400`,
  `drop.quantity: 4`) both lay out 4 key shapes as a real 2x2 square within that
  16x16 ceiling (15 wide x 7 tall, single layer) rather than side-by-side across two
  full-width single-key patterns, which would have exceeded it.
- Material cost check, per your ask: one ingot's workpiece is a fixed
  `ItemIngot.VoxelCount = 42` metal voxels (`gamesrc/survival-1.22.5/Item/
  ItemIngot.cs`). The regular single-key pattern fills 15 of those (well under one
  ingot). The bulk (x4) pattern's original single-layer 2x2 square only filled 40 —
  still just *under* one ingot, the opposite of what a 4-key batch should cost. Per
  your suggestion, I doubled it to two identical layers (thickness, not a reshape) =
  80 voxels, i.e. ~1.9 ingots: it now requires a second ingot to complete, using
  95% of that second ingot's capacity rather than the full 168 a naive 4x scale-up
  (4 x 42) would have demanded — a real bulk discount, not a wash.
- Still not done: shape/texture art (placeholders only, as intended); in-game
  verification of the anvil voxel patterns, the two keymold sizes' firing/pouring
  flow, and the keyring's `IHeldBag` slots actually appearing in the inventory
  screen — none of this has been tested against a running client yet (this harness's
  build step compiles the DLL but the asset-symlink step needs elevated/dev-mode
  Windows to run, so launch and test in-game after building normally).

## 10. Client/server execution audit

Prompted by a question about the durability-break pipeline, which surfaced a real
gap. Findings and fixes, per execution path:

- **`IsLockedForInteractPatch` (the Harmony patch) — fixed.** A Harmony patch targets
  a *physical compiled method*, not a logical "side". In singleplayer the client's
  predictive call and the embedded server's authoritative call are the same process
  and the same patched method — both go through this `Prefix`. On dedicated
  multiplayer this is moot for a different reason: `harmony.PatchAll()` only ever
  runs from `StartServerSide`, which a pure client process never executes, so the
  patch simply isn't installed there at all. Net effect: the *read* (is this locked,
  is there a matching key) was already safe to run on both invocations — it's
  side-effect-free. But the *write* (`KeyAccessUtil.DamageKey`, which now calls
  `Collectible.DestroyItem` — see below) was not gated at all, so in singleplayer a
  single unauthorised unlock would have run it twice: once from the client-side
  predictive invocation, once from the server's. Fixed by gating the whole
  damage block on `forPlayer.Entity.World.Side == EnumAppSide.Server` — checking the
  *call's own* world/side, not `VSLockAndKeyModSystem.Api.Side`, since that's a
  static field that both the client-side and server-side `ModSystem` instances
  write to in singleplayer (same process, two instances, last write wins) and is
  therefore not a reliable way to ask "which side is this specific call on".
- **Durability break moment — fixed, per your earlier question.** `KeyAccessUtil.
  DamageKey` now calls `Collectible.DestroyItem(byEntity.World, byEntity, keySlot)`
  once the custom `vlkDurability` counter (kept, for the explicit "uses remaining"
  tooltip text a bare vanilla bar can't convey) hits zero, instead of a bare
  `TakeOut(1)`. Confirmed safe to call from our synthetic `KeyringContentRefSlot`:
  `ItemSlot.Itemstack` is a plain non-virtual property, so `DestroyItem`'s direct
  `itemSlot.Itemstack = null;` still writes the same backing field our overridden
  `MarkDirty()` reads afterward, and `GetTool()` returns null for our keys (no
  `"tool"` attribute in `key.json`) so `DestroyItem`'s tool-refill branch is a no-op.
  Only safe now that the call site above is server-gated.
- **`ItemKey.OnHeldInteractStart` (filing-dialog trigger) — tightened.** The
  file+key-combo *decision* (do we intercept this interact-start at all) now runs
  identically on both sides, so client and server agree on `handling` even though
  only the client actually opens `GuiDialogKeyFile`. No world state was ever mutated
  here either way — this is a consistency/polish fix (avoiding a default-handling
  mismatch between sides), not a correctness one.
- **`VSLockAndKeyModSystem.OnBindKeyPacket` (the actual key-binding write) — audited,
  no change needed.** Already properly server-authoritative: registered only via
  `StartServerSide`, and re-derives everything itself from the server's own state
  (`fromPlayer.InventoryManager.ActiveHotbarSlot`, `fromPlayer.Entity.LeftHandItemSlot`,
  `fromPlayer.GetGroup(...)`) rather than trusting anything from the packet except
  the player's *intent* (which target was picked). The one part of the packet taken
  at face value, `DisplayName`, is cosmetic tooltip text only.
- **`CollectibleBehaviorKeyring` (`IHeldBag`) — audited, no change needed.** This
  only ever runs as part of vanilla's own already-server-validated inventory
  slot-transfer system; we don't introduce any separate client-trusts-itself path
  here.
- **Known, accepted asymmetry:** `ModConfig` and the Harmony patch are only ever
  loaded/installed via `StartServerSide`. On dedicated multiplayer this means a
  client never gets our added "locked, no key" feedback pre-emptively — it falls
  through to vanilla's own owner/group check, which can locally say "not locked"
  for an authorised-but-keyless owner, only for the server to reject it a moment
  later once the (server-only) key gate applies. This is the same "no client
  prediction, server is authoritative, a rejected action just doesn't happen"
  pattern you confirmed is fine and that vanilla itself uses in places like
  `ItemPlumbAndSquare` (client-side handling is a bare `PreventDefaultAction`, all
  real logic server-only). I left this as-is rather than installing the Harmony
  patch on the client process too, since doing that safely would require solving a
  second problem — `Harmony.UnpatchAll(HarmonyId)` unpatches by that shared string
  ID, so if both a client-side and server-side `ModSystem` instance ever separately
  installed and disposed patches under the same ID (relevant in singleplayer, where
  both instances exist in one process), one instance unloading could rip out the
  other's still-active patch — without being able to test that scenario live, I'd
  rather leave the existing, narrower server-only installation in place than
  introduce an unverified new failure mode.

## 9. Reference material consulted

- `examples/TankardsandGoblets` — variant/gem-studding JSON pattern for keys.
- `examples/Thievery` — a different (Harmony-patch + lockpicking minigame) approach
  to the same vanilla lock system, used for inspiration on *where* vanilla exposes
  hooks (`ModSystemBlockReinforcement`, `BlockBehaviorLockable`), not copied.
- `gamesrc/survival-1.22.5` — authoritative vanilla reinforcement/lock source.
- `workspaces/LocksAffectReinforcement` — sibling workspace already Harmony-patching
  a neighboring method on `ModSystemBlockReinforcement`; source of the patch pattern
  (Harmony id, `PatchAll`/`UnpatchAll` lifecycle in `ModSystem.Start`/`Dispose`) used
  here.
