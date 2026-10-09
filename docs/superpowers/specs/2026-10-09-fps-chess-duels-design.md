# FPS Chess: duels, themed rooms and online play

Status: approved in brainstorming on 2026-10-09, pending written-spec review.
Replaces the current FPS mode (walk on a giant board, captures are instant shots).

## 1. Goal

Turn the FPS mode into an FPS Chess game in the spirit of the Steam game:

- **Board phase:** normal chess on a 3D board, seen from above.
- **Duel phase:** every capture starts a first-person duel between the attacking and the defending piece, fought on the
  board itself. The winner keeps or takes the square.
- **Opponents:** the computer (chess AI plus a shooter bot) or a human on another device via a server on gulfinson.

The 2D mode, local hot-seat, saving, the chess AI and the clock stay as they are, so course requirements 1 to 4 remain
covered by the existing code.

## 2. Decisions

| Topic | Decision |
|---|---|
| Attacker loses the duel | Attacker dies and is removed; the defender stays on its square; the turn counts as played. |
| Duel location | On the chess board, which stands on a table in a themed room. Other pieces are static cover; small obstacles stand on the table around the board. |
| Duel format | 1v1, single round, HP-based, 3 s countdown, 60 s limit. On timeout the defender wins. The chess clock is paused during a duel. |
| Falling off the table | Counts as losing the duel. |
| Piece classes | Unequal HP following chess value, own weapon and perk per piece (section 5). |
| Exposed king | If a lost duel leaves the attacker's king attacked, that king may be captured. Capturing it starts a duel against the king; if the king loses, the game ends. The opponent may also play another move. |
| Other game end rules | Standard: checkmate, stalemate, repetition, fifty-move rule, insufficient material. |
| Architecture | Authoritative match server from the start. Against the computer the server runs embedded in the game and the client talks to it over loopback; online the same server runs on gulfinson. |
| Server code | Lives in this repo (same Godot project, started with `--server`). Deployed only on gulfinson, never as a service on the laptop. |
| Online transport | WebSocket (`WebSocketMultiplayerPeer`) behind nginx with TLS, e.g. `wss://chess.gulfgulfinson.de`. |
| Saving | Only games against the computer, only in the board phase. Online games are not saved. |
| Movement authority | Client-authoritative movement; server validates shots against the positions it knows. Good enough for a uni project, not cheat-proof. |

## 3. Architecture

```
src/Chess.Core            pure C#, unit tested, no Godot
  Game (+ duel variant)   capture -> PendingDuel -> ResolveDuel(outcome); king capture; save format v2
  Duels/                  FighterClass data, DuelState (HP, ammo, cooldowns, armor, regen, timer, winner)
game/                     Godot 4.7 .NET project, client and server in one
  Net/MatchServer         per room: GameSession, starts duels, runs the bot, validates hits, owns a physics world
  Net/LobbyServer         names, room list, create/join by 4-letter code, protocol version check
  Net/MatchClient         sends inputs, receives state; drives the board and duel views
  Arena/                  themed room scenes (table, board, obstacles), shared by client and server
  Board3D/                board phase view and camera
  Duel/                   first-person fighter, weapons, duel HUD, bot
gulfinson                 headless export started with --server, systemd service, nginx vhost (wss)
```

**One code path for both modes.** "Against the computer" starts an embedded `LobbyServer` + `MatchServer` on a random
`127.0.0.1` port, connects to it as a normal client and creates a private room with a bot opponent. Client and server
use separate `MultiplayerAPI` branches in the same scene tree. Online play differs only in the address.

**The server owns all game state:** chess legality (`Chess.Core`), duel rules (`DuelState`), hit validation (raycasts in
the room's own `World3D`), the duel timer, the bot and the clock. Clients render and send inputs.

## 4. Core changes (Chess.Core)

### 4.1 Duel variant

- `GameSetup` gets `Variant` (`Classic` default, `Duel`). Classic behaviour is unchanged and all existing tests stay green.
- In `Duel`, a capture move does not complete immediately: `Game` enters `PendingDuel { Move, Attacker, Defender,
  AttackerSquare, DefenderSquare }`. No other move is accepted until `ResolveDuel(DuelOutcome)` is called.
  En passant duels use the captured pawn's square as the defender square.
- `DuelOutcome.AttackerWon`: the move is applied normally (promotion included).
- `DuelOutcome.DefenderWon`: the attacker is removed from its origin square, the defender stays, the side to move
  switches, the halfmove clock resets, castling rights are updated as if the attacker had moved away.
- **Capturable king:** in `Duel`, if the opponent's king is attacked on your turn, capturing it is a legal move, even if
  your own king is in check. King duel won by the attacker ends the game with the new status `KingCaptured`.
  Otherwise the normal legality rule still applies to the mover (own king may not be left in check by a normal move).
- Status evaluation (checkmate, stalemate, draws) is unchanged and runs after each completed move or resolved duel.
- `PlayedMove` gets an optional `DuelOutcome`. SAN for a lost capture gets a display suffix: `Rxe8 ✗`.

### 4.2 Save format v2

- `version: 2`. Each move string is UCI, optionally followed by `:A` (attacker won) or `:D` (defender won) for duel
  captures, e.g. `"d1h5"`, `"h5f7:A"`, `"e2e8:D"`. Loading replays moves and outcomes through `Game`.
- `setup.variant` is stored. Version 1 files still load (as `Classic`).
- Saving while a duel is pending is rejected.

### 4.3 Chess AI

- Move generation includes king captures in the duel variant; the AI scores a king capture as a win.
- The AI does not model duel win probabilities (out of scope, documented).

### 4.4 GameSession

- Exposes `DuelStarted(PendingDuel)` and `ResolveDuel(outcome)`; pauses the clock while a duel is pending;
  `MovePlayed` fires after resolution with the outcome. Undo is disabled in the duel variant.

## 5. Duel rules (Chess.Core/Duels)

Fighters have the size of their piece (pawn 1.5 m to king 2.9 m, as today); eye height is 85 % of the piece height.
Base move speed 7 m/s, jump velocity 5.5 m/s.

| Piece | HP | Weapon | Damage | Interval | Magazine | Reload | Spread | Perk |
|---|---|---|---|---|---|---|---|---|
| Pawn | 100 | Semi-auto rifle | 22 | 0.25 s | 12 | 1.6 s | 1.0 deg | none |
| Knight | 120 | Shotgun, 8 pellets | 12 per pellet | 0.9 s | 6 | 2.2 s | 7 deg, max range 25 m | double jump |
| Bishop | 90 | Sniper rifle | 75 | 1.4 s | 5 | 2.5 s | 3 deg hip, 0 deg zoomed | zoom (FOV 30) on RMB |
| Rook | 200 | Minigun | 9 | 0.07 s | 150 | 3.5 s | 2.5 deg, 0.8 s spin-up | 25 % damage reduction, 0.75x speed |
| Queen | 160 | Assault rifle | 14 | 0.10 s | 30 | 2.0 s | 1.5 deg | dash 10 m (3 s cooldown), 2 grenades (70 dmg, 4 m radius, linear falloff, 2 s fuse) |
| King | 140 | Revolver | 45 | 0.6 s | 6 | 2.4 s | 0.5 deg | regenerates 5 HP/s after 3 s without damage |

`DuelState` is pure and tick-driven like `ChessClock`: `Tick(dt)`, `TryFire(fighter)` (interval, ammo, spin-up),
`StartReload`, `ApplyDamage(target, amount)` (armor), `TryDash`, `TryThrowGrenade`, `Fall(fighter)`, regeneration,
countdown, timeout, `Winner`. These numbers are tuning values; they live in one data table so balancing is one edit.

## 6. Board phase (client)

- One scene per themed room: the board on a table, built at piece scale (3 m squares), so table obstacles are huge.
  Themes for the first version: **library** (books, inkwell, candle, glasses), **castle hall** (goblet, candelabra,
  dagger, map scroll), **sci-fi lab** (beakers, data cubes, cables). The server picks the theme per game and tells
  both clients.
- Camera above the board at ~55 deg from the player's side; RMB drag orbits, wheel zooms, both clamped.
- Click a piece, legal targets light up (green move, red capture), click a target. Promotion picker reused.
- Pieces slide, knights jump. HUD: turn, clocks, move list with duel outcomes, captured pieces.
- Pause menu: resume, save (computer games, board phase), resign, main menu.

## 7. Duel phase (client)

- On every capture the camera flies (~1 s) into the head of the local player's involved piece; both players are always
  involved in 1v1. After the duel it flies back; the loser bursts; the board updates.
- Controls: WASD, mouse, LMB fire, RMB aim/zoom, R reload, Space jump (double for knight), Shift run (dash for queen),
  G grenade (queen).
- HUD: own HP and opponent HP bars, ammo, timer, opponent class.
- Falling below the table surface by more than 5 m loses the duel.

## 8. Bot (server side)

- Navmesh baked at runtime from the board, pieces and obstacles of the current position (`NavigationServer3D`, works
  headless).
- States: approach until line of sight, then strafe and shoot; reload behind cover when empty if cover is near.
- Difficulty follows the AI level from the menu: reaction time 0.6 s (level 1) to 0.2 s (level 4), aim error
  6 deg to 1.5 deg.

## 9. Network

- **Lobby:** player name; create a game (time control, colour or random) giving a 4-letter room code and a public list
  entry; join from the list or by code. On connect the client sends `ProtocolVersion`; a mismatch returns a clear
  error.
- **Rooms:** many per process; each has its own `World3D` for hit validation and bot navigation.
- **Client to server:** `SubmitMove(uci)`, `DuelInput(position, yaw, pitch, tick)` at ~30 Hz, `Fire(origin, direction,
  tick)`, `Reload`, `Dash`, `ThrowGrenade(direction)`, `Resign`.
- **Server to client:** `GameState` (start FEN, moves, clocks, theme, colours), `MovePlayed`, `DuelStarted` (classes,
  spawn squares), `DuelSnapshot` at ~30 Hz (opponent pose, both HP, ammo, timer), `Hit`, `DuelEnded(outcome)`,
  `GameEnded(result)`.
- **Hit validation:** the shot origin must be within 1.5 m of the server's last known pose of the shooter; the server
  raycasts against fighter hitboxes, pieces and obstacles in the room world.
- **Disconnects:** 60 s to reconnect with room code plus a reconnect token; otherwise the remaining player wins by
  forfeit.
- **Deployment on gulfinson:** Godot export preset "Linux Server" (dedicated server, visuals stripped; needs the 4.7.2
  .NET export templates), synced to the server, systemd service listening on 127.0.0.1, nginx vhost with WebSocket
  upgrade and Certbot TLS. Port and subdomain are chosen against `lab:~/README.md`, deployed via the `/deploy` skill,
  and the README's service list and log are updated in the same task.

## 10. Error handling

- Invalid moves or duel inputs from a client are rejected and logged with the room code; malformed messages close the
  connection.
- An exception inside a room ends that room (both players get an error result) without taking down the process.
- Client shows connection loss, version mismatch and server errors as messages and returns to the menu.

## 11. Testing

- **Core (TDD):** duel variant transitions, failed captures (including castling rights and en passant), king capture
  and `KingCaptured`, AI taking an exposed king, save format v2 and v1 compatibility, every `DuelState` rule
  (fire interval, ammo, reload, spin-up, armor, regen, dash and grenade cooldowns, fall, timeout winner).
- **Server integration:** headless Godot scene starts a server and two scripted clients on localhost, plays moves,
  triggers a duel, sends scripted shots, and checks the outcome and resulting position.
- **Visual:** the existing capture tool, extended for board view, duel view and each theme.
- Done means `dotnet test` and the Godot build both pass, plus a manual playtest by Phillip.

## 12. Build order

Sub-project 1, playable against the computer:

1. Core duel variant and save format v2.
2. Core `DuelState` and class table.
3. Embedded server, loopback client, lobby skeleton; board phase in the first themed room.
4. Duel phase: fighter controller, weapons, perks, HUD, transitions.
5. Bot.
6. Remaining two themes.

Sub-project 2, online:

7. Lobby UI, public list and room codes, reconnect.
8. Server export and deployment on gulfinson.

## 13. Out of scope

Accounts, rankings, chat, spectators, saving online games, more than two players, web or mobile builds, anti-cheat beyond
server-side validation, AI that weighs duel odds.
