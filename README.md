# Moose.HapisMods

A collection of Harmony mods for Rust powering the Hapis custom map.

## Note
These plugins have been passed around many times over the years and are quite literally a total mess. If you feel so inclined to clean them up and fix/add features, all PRs are welcome. 

## Mods

- **Hapis.CargoPath** — Tracks Cargo Ship spawn/stop points read from the map data. Also reads the cargo path from RustEdit 2 maps.
- **Hapis.Deployables** — Manages custom deployable prefabs saved in the map data.
  - Spawns RustEdit 2 map entities
  - RustEdit 2 settings: indestructible entities, sign images and radiation zones
  - Sign images work with Rust's new sign file storage, and still load after Rust clears the map data
  - Custom map signs can't be drawn on or picked up (admin with god and fly on can)
  - Door spam E fix for map doors, no more stuck or out of sync doors
  - Door brushing fix, no more rubber-banding next to moving map doors
- **Hapis.IO** — Restores electrical/IO connections from map data and protects managed IO entities from damage and unauthorized access.
  - Reads IO from RustEdit 2 maps
  - Custom apartments: rooms numbered B1, B2... and kept after a restart, mailboxes linked
  - Guard toll opens the door for the payer and their team, closes when they walk away
  - Master key break-in works standing at the door
  - Elevator buttons move one floor at a time
- **Hapis.NPC** — Custom NPC spawner and AI behavior system (scientists, bandits, scarecrows, and more), including equipment and targeting logic.
  - Reads NPC spawners from RustEdit 2 maps
  - NPCs take cover, peek, reload, heal, strafe and retreat (when `UseRustEditNPCLogic` is off)
  - NPCs can't see through deployables or glass
  - NPCs underground (tunnels, tunnel entrances) can't see or shoot through the ground
  - Scientists carry an LR300 (1 in 10 get a pistol)
  - NPCs that spawn before the navmesh is ready keep checking until they can walk
  - NPC AI wakes when a player is within 150 m and sleeps 30 s after the last one leaves, so far away NPCs cost nothing
  - Scientists run up and down oil rig stairs (custom 2019 small and large rigs included)
  - RustEdit 2 CH47 landing zones: hacking a linked crate calls a CH47 and turns on linked alarms and nearby sirens
  - CH47 heavies wander and explore the rig stairs, walk toward the nearest player every minute, and only charge once they spot you or get shot
  - Navmesh kept at sea around oil rigs, CH47 landing zones and NPC spawners so rig NPCs can move
- **Hapis.Extra** — Extra Hapis features.
  - Chinook drops at named spots (Legacy Offices, Milly, Sewer) on timers, `/chinook <name>` and `/chinookgone`
  - Old locked crates removed
  - Crude pumps and water sources filled
  - Taking oil and adaptors from pumps blocked
  - Startup console command
  - Cargo ship docks at the Hapis barge: the dock route is added to the cargo path on start, the ship turns in, slides alongside without hitting the barge or shore, and eases away after `dock_time`
  - Cargo event timer pauses while the ship is docked (crates keep their timers and stay lootable)
  - Config in `HarmonyConfig/HapisExtras.json`

## Shared
`src/Shared` is built into every mod: Title-Case logging and the RustEdit 2 map reader. All the mods work on RustEdit 1 and RustEdit 2 maps.
