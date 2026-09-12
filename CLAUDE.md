# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity stealth-survival game prototype (2-person team, Korean). The player navigates a node-based graph, completes timed skill-check gimmicks to unlock routes, and avoids a monster that hunts via pathfinding + patrol. Hiding spots provide temporary safety. Failed skill checks and noise raise monster aggro, which scales its speed and aggression.

Game design documents are on Notion (links in README.md).

## Build & Development

- **Engine**: Unity with URP v14.0.12
- **Solution**: `GameProject/GameProject.sln` — open in Unity Editor or Visual Studio/Rider
- **No CLI build or test commands** — this is a Unity project; build and run through Unity Editor
- **Scenes** are numbered: `00_Boot` (initial load), `10_Gameplay_Stage1` (main gameplay)

## Code Architecture

All game scripts live under `GameProject/Assets/_Project/Scripts/`, organized into `Core/` and `Gameplay/`.

### Namespace Map

| Namespace | Purpose |
|---|---|
| `Game.Core.StateMachine` | Generic state machine (`IState`, `StateMachine`) |
| `Game.NodeSystem` | Node graph: `Node`, `NodeGraph` (singleton), `NodeLook`, `NodeSelectable` |
| `Game.Gimmick` | `GimmickNode` — interactive objectives with progress/decay/skill-checks |
| `Game.Hiding` | `HideSpot` — safe zones on nodes |
| `Game.Player` | Player states and controllers |
| `Game.Monster` | Monster AI, aggro, patrol |
| `Game.UI` | `SkillCheckUIController` — timing minigame |
| `Game.CameraSystem` | `FirstPersonNodeCamera` — first-person camera with state-driven FOV/limits |

### Core Systems

**EventBus** (`Core/Events/EventBus.cs`) — Static event hub for decoupled cross-system communication. Key events: `OnGimmickCompleted`, `OnSkillCheckFailed`, `OnNoise`, `OnHideEntered/Exited`, `OnMonsterDisableRequested`, `OnMonsterRespawned`. When adding new cross-system interactions, add events here rather than creating direct references.

**StateMachine** (`Core/StateMachine/`) — Simple `Set(nextState)` → `Exit()` → `Enter()` pattern with per-frame `Tick()`. Used by both `PlayerStateMachine` and `MonsterNodeAI`.

### Gameplay Flow

1. **Player** clicks a node → `PlayerNodeMover` validates (active, neighbor) → movement with 0.6s delay (cancellable)
2. **Gimmick interaction**: hold to fill progress bar (0–100) with periodic skill checks. Zone width shrinks as progress rises. Fail/cancel incurs progress loss and aggro increase.
3. **Completed gimmicks** unlock gated nodes (`GimmickNode.unlockNodes`)
4. **Monster** runs a brain loop (0.35s interval) choosing next node toward player, with `randomPickChance` for unpredictability and patrol bias. Aggro (0–100) scales speed and think rate.
5. **Hiding** entered/exited with E key; broadcasts via EventBus.

### Key Runtime Dependencies

- `MonsterNodeAI` finds `PlayerNodeMover` at runtime to track the player
- `PlayerGimmickController` finds `MonsterNodeAI` at runtime for proximity check (`forbidIfMonsterWithin`)
- `NodeGraph` is a singleton accessed by both player and monster systems

## Editor Tools

`Settings/Editor/NodeCreator.cs` provides menu items under the Unity editor to create Node, Hide Node, and Gimmick Node objects with auto-numbering, and to rebuild neighbor links by distance (8m default).

## Conventions

- **Language**: Code comments and commit messages are in Korean
- **Folder rules**: External assets go in `_ThirdParty/`, test files in `_Tests/`
- **Scene naming**: Numeric prefix for load order (`00_`, `10_`, ...)
- **Materials**: Prefixed `M_Proto_` (prototype stage)
- **Communication pattern**: Use `EventBus` for cross-system events, not direct component references
- **Async**: Coroutines for timed sequences (skill checks, monster disable/respawn)
