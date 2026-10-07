# ORBIT DEPOT

Run a fulfillment floor on a space station: receive cargo, rack it, pick orders, pack, sort and ship across the galaxy. It's an idle-arcade warehouse game in Unity 6 WebGL, for phones and desktop.

## The floor (one loop, seven stations)
| # | Station | You do | Hire |
|---|---|---|---|
| 01 | **Inbound dock** | A space truck backs in and unloads loose cartons. ELI (dock crew) builds pallets of 8. You grab a pallet. | PALLY (pallet-jack bot) moves pallets 01 → 02 |
| 02 | **Bulk storage** | Store pallets here, then break them down into cartons. | Restocker moves 02 → 03 |
| 03 | **Restock (back of rack)** | Fill the pick rack: 32 slots, 48 with the taller rack. | |
| 04 | **Pick (front of rack)** | Orders stream in. Each pick turns one unit into a blue tote. | Picker moves 04 → 05A |
| 05A / B / C | **Pack line** | Drop totes on the infeed belt. Stand at the packing table to pack. Packed boxes ride into the cart (12). Take the cart's boxes. | Packer works the table |
| 06A / B | **Sort** | Drop boxes in the chute. A robot arm sorts them into bins A–D. Forklift the fullest bin. | Sorter moves 05C → 06A; Forklift moves 06B → 07 |
| 07 | **Outbound dock** | Bins load onto the space truck, and you're paid per box ($8 base). Collect the cash. | AUTO-PAY banks it for you |

### Systems
- **Power Pack:** a bonus station. 60 s of 2× packing and 2× pay; it's a rewarded ad on portals and recharges for free otherwise.
- **Peak season:** 2× orders and 1.5× pay for 45 s.
- **Expansion pads:**
  - Crew hires.
  - Taller rack.
  - Bigger trucks.
  - Auto-pay.
  - Express orders.
  - Opening the next depot (LUNA DEPOT → MARS HUB → EUROPA PORT); each pays 4× more.
- **Progress:**
  - Upgrades terminal: speed, carry capacity, profit and crew speed.
  - Quests.
  - Offline earnings.
  - Autosave to PlayerPrefs plus a localStorage mirror.
  - Full live-floor snapshot: everything on pallets, racks, belts, bins and in your hands.
- **Guided first loop:** the tutorial walks you through 01 → 07 once, with an arrow and hints.

### Look
- A dark navy deck and in-world signboards (dark cards with coloured titles and big white status).
- Painted lanes and zone outlines, "WALK HERE" rings numbered by station, and callouts like "NEEDS A PACKER".
- The floor logo, and space trucks docking outside the back wall.

## Assets (all CC0)
- KayKit **Space Base Bits**: space trucks, landing pads, base modules, containers.
- Kenney **Factory Kit**: conveyors, boxes, scanner, screens, robot arm, hopper.
- Kenney **Mini Characters**: crew and player, with procedural hard hats.
- Kenney **Furniture/Space** kits, carried over from Space Diner.

The engine is shared with Space Diner (zones, carry stacks, pads, nav grid, HUD).

## Build
```bash
"C:/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Unity.exe" -batchmode -nographics -projectPath unity -executeMethod DepotBuild.WebGL -quit -logFile build.log
node tools/serve.mjs 8140
```

URL flags:
- `?bot=1` runs the autopilot: it plays the whole loop and buys pads.
- With `?dev=1` (localhost only):
  - `fresh=1` resets the save.
  - `speed=N` speeds the game up.
  - `cheat=1` plus the M key adds money.
