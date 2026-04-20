# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**SMA_p1** is a Unity 6 (6000.3.6f1) game demonstrating Multi-Agent Systems (SMA = Sistemas Multi-Agente in Spanish) using the **TouringMachines (TM)** architecture. It's an academic project: a medieval castle stealth game where a player-controlled Thief must steal treasure and escape while avoiding AI-controlled guards.

- **Renderer:** Universal Render Pipeline (URP)
- **Resolution:** 1024×768
- **Scenes in build order:** `Castle` (0), `Victoria` (1)

## Architecture: TouringMachines Agent Model

Each guard AI is composed of independently communicating subsystems. All agent scripts live under `Assets/Scripts/Agent/`.

### Data Flow

```
Sensors → (events) → Behaviours → ReactiveLayer / DeliberativeLayer → ControlSubsystem (Arbiter) → AgentActuator
```

### Key Rule: Reactive Priority
`ControlSubsystem.cs` arbitrates every frame. **Reactive layer always wins** if it requests control — this implements the TM inhibition rule. Deliberative proposals are only executed when the reactive layer yields.

### Folder Structure

```
Assets/Scripts/
├── Agent/
│   ├── Animation/
│   │   └── AgentActuator.cs
│   ├── Behaviours/
│   │   ├── IBehaviour.cs
│   │   ├── AttackBehaviour.cs
│   │   ├── ChaseBehaviour.cs
│   │   ├── FollowGuardBehaviour.cs
│   │   ├── InvestigateBehaviour.cs
│   │   ├── PatrolBehaviour.cs
│   │   └── WatchBehaviour.cs
│   ├── Control/
│   │   ├── ActionProposal.cs
│   │   └── ControlSubsystem.cs
│   ├── Deliberative/
│   │   ├── DeliberativeLayer.cs
│   │   ├── GuardianDeliberativeLayer.cs
│   │   └── PatrolDeliberativeLayer.cs
│   ├── Reactive/
│   │   ├── ReactiveLayer.cs
│   │   ├── GuardianReactiveLayer.cs
│   │   └── PatrolReactiveLayer.cs
│   └── Sensors/
│       ├── HearingSensor.cs
│       ├── MovementSensor.cs
│       ├── SwordHitBox.cs
│       └── VisionSensor.cs
├── Manager/
│   ├── GameManager.cs
│   ├── MenuVictoria.cs
│   └── ZonaSalida.cs
├── Shared/
│   └── NoiseEmitter.cs
├── Thief/
│   ├── ThirdPersonCamera.cs
│   └── ThiefMove.cs
└── World/
    └── DynamicBars.cs
```

### Subsystems

| Script | Role |
|--------|------|
| `Control/ControlSubsystem.cs` | Arbiter — calls both layers, picks winner via inhibition rule |
| `Control/ActionProposal.cs` | Plain C# data object passed between layers and actuator |
| `Reactive/ReactiveLayer.cs` | Abstract base — subclass per guard type |
| `Reactive/PatrolReactiveLayer.cs` | Patrol guard: Attack > Chase > WatchExit > Investigate > FollowGuard > Patrol priority chain |
| `Reactive/GuardianReactiveLayer.cs` | Static guard: Attack > Chase > Investigate > FollowGuard > Watch priority chain |
| `Deliberative/DeliberativeLayer.cs` | Abstract base — subclass to add deliberative planning |
| `Deliberative/PatrolDeliberativeLayer.cs` | Stub (planning not yet implemented) |
| `Deliberative/GuardianDeliberativeLayer.cs` | Stub (planning not yet implemented) |
| `Behaviours/IBehaviour.cs` | Interface common to all behaviours: `Evaluate() → ActionProposal?` |
| `Behaviours/AttackBehaviour.cs` | Active when thief visible AND in range — move + attack |
| `Behaviours/ChaseBehaviour.cs` | Active when thief visible but NOT in range — pursue |
| `Behaviours/InvestigateBehaviour.cs` | Active when thief heard but NOT visible — move to noise position at walk speed |
| `Behaviours/FollowGuardBehaviour.cs` | Active when another guard is heard running (and no direct thief info) — move to guard position |
| `Behaviours/PatrolBehaviour.cs` | Fallback for patrol guard — move between two scanned points |
| `Behaviours/WatchBehaviour.cs` | Fallback for static guard — oscillate vision ±watchAngle° |
| `Behaviours/WatchExitBehaviour.cs` | Active when chest is detected stolen — run to exit zone |
| `Sensors/VisionSensor.cs` | Distance + FoV (180°) + raycast LOS; publishes events on state change; scans environment in 16 directions for patrol planning; polls `DynamicBars.todas` each frame and re-triggers `EscanearEntorno()` when a visible bar changes state |
| `World/DynamicBars.cs` | Cyclic obstacle: rises from floor on a timer, stays up briefly, then retracts. Exposes `EstaArriba` property and registers in static `DynamicBars.todas` list so `VisionSensor` can poll it without direct references. Carries a `NavMeshObstacle` to carve the NavMesh while raised |
| `Sensors/HearingSensor.cs` | Overlap-radius detection via `NoiseEmitter`; distinguishes thief from guard noise; applies positional imprecision |
| `Sensors/MovementSensor.cs` | Monitors NavMeshAgent arrival; publishes `OnDestinoAlcanzado` |
| `Sensors/SwordHitBox.cs` | Trigger on weapon; reloads scene on Thief hit |
| `Shared/NoiseEmitter.cs` | Emits a noise radius based on movement speed; used by both thief and guards; polled by `HearingSensor` |
| `Animation/AgentActuator.cs` | Executes `MoverA()`, `Rotar()`, `IniciarAtaque()` on the NavMeshAgent |

### Sensor Events (push model)

Sensors fire C# events; behaviours subscribe in `Initialize()` and maintain their own state flags.

| Sensor | Event | Payload |
|--------|-------|---------|
| `VisionSensor` | `OnLadronAvistado` | — |
| `VisionSensor` | `OnLadronPerdido` | — |
| `VisionSensor` | `OnLadronMuyCerca` | — |
| `VisionSensor` | `OnLadronLejos` | — |
| `VisionSensor` | `OnEscaneoCompletado` | `Vector3[] direcciones, float[] distancias` |
| `VisionSensor` | `OnCofresDesaparecido` | — |
| `HearingSensor` | `OnLadronEscuchado` | `Vector3 posicion` (con imprecisión) |
| `HearingSensor` | `OnLadronNoEscuchado` | — |
| `HearingSensor` | `OnEsqueletoEscuchado` | `Vector3 posicion` (con imprecisión) |
| `HearingSensor` | `OnEsqueletoNoEscuchado` | — |
| `MovementSensor` | `OnDestinoAlcanzado` | — |

### Behaviour Pattern

Behaviours are plain C# classes (not MonoBehaviour) implementing `IBehaviour`. Each:
1. Receives dependencies via `Initialize()` (sensors, transform).
2. Subscribes to sensor events and stores local state flags.
3. Returns an `ActionProposal` from `Evaluate()`, or `null` if inactive.

The reactive layer selects the first non-null result in priority order:
```csharp
// PatrolReactiveLayer
attack.Evaluate() ?? chase.Evaluate() ?? investigate.Evaluate() ?? followGuard.Evaluate() ?? patrol.Evaluate();

// GuardianReactiveLayer
attack.Evaluate() ?? chase.Evaluate() ?? investigate.Evaluate() ?? followGuard.Evaluate() ?? watch.Evaluate();
```

### Adding a New Guard Behavior
1. Create a class implementing `IBehaviour` under `Agent/Behaviours/`.
2. Subscribe to the relevant sensor events in `Initialize()`.
3. Return a non-null `ActionProposal` from `Evaluate()` when the behaviour should be active.
4. Inject it into the appropriate reactive layer and add it to the priority chain.

## Game Systems

**`Manager/GameManager.cs`** — Singleton. Tracks `hasLoot` bool. Win: player steals treasure (`PlayerLooted()`) **and** reaches exit zone (`IntentarFinalizarMision()`).

**`Thief/ThiefMove.cs`** — Player controller using `CharacterController` (not NavMesh). WASD input; pressing S triggers a 180° turn animation.

**`Manager/ZonaSalida.cs`** — Exit trigger; checks tag `"Ladron"` on `OnTriggerEnter`.

## Key Parameters (tune in Inspector)

| Location | Parameter | Default |
|----------|-----------|---------|
| `PatrolReactiveLayer` / `GuardianReactiveLayer` | `velocidadCorrer` | 5 m/s |
| `PatrolReactiveLayer` | `velocidadCaminar` | 2 m/s |
| `PatrolReactiveLayer` | `margenPared` | 1.5 units |
| `GuardianReactiveLayer` | `anguloVigilancia` | 90° |
| `GuardianReactiveLayer` | `velocidadRotacionIdle` | 30°/s |
| `VisionSensor` | `viewDistance` | 12 units |
| `VisionSensor` | `viewAngle` | 180° (total) |
| `MovementSensor` | Arrival tolerance | 0.3 units |
| `AgentActuator` | `cooldownAtaque` | 1.5 s |
| `HearingSensor` | `radioEscucha` | 2 units |
| `HearingSensor` | `imprecisionRuido` | 1.5 units |
| `NoiseEmitter` | `radioAndando` | 2 units |
| `NoiseEmitter` | `radioCorriendo` | 3 units |
| `NoiseEmitter` | `umbralCorrer` | 3.5 m/s |

## Working in This Project

This is a Unity project — there are no CLI build/test commands. Development requires:
1. Open the project in **Unity 6000.3.6f1** (or compatible Unity 6 version).
2. Open `Assets/Scenes/Castle.unity` for the main scene.
3. NavMesh must be rebaked (`Window > AI > Navigation`) after geometry changes.
4. The `InputSystem_Actions.inputactions` asset controls player input bindings.

## Tags & Layers Used in Code

- `"Ladron"` — Player/Thief GameObject tag (used by sensors and exit trigger)
- `"Esqueleto"` — Guard GameObject tag (used by `HearingSensor` to distinguish guard noise from thief noise)
- Guards use NavMesh; player uses CharacterController — they do not share movement systems.

## Language Note

Code comments and variable names are in **Spanish** (this is an academic project for a Spanish-language course). Maintain Spanish for comments and UI strings. Behaviour class names are in **English**.

## Coding Style (professor requirements)

- **No `var`:** Always declare variables with their explicit type. Never use `var`.
- **No `[Header]`:** Do not use `[Header(...)]` attributes on serialized fields.


# Mejoras implementadas

## Mejora 5 — Dinamismo de murallas (obstáculos móviles) ✓

**Implementación:** `World/DynamicBars.cs` gestiona rejas que emergen del suelo de forma cíclica. Usan `NavMeshObstacle` para tallar el NavMesh en tiempo de ejecución. Estado observable mediante la propiedad `EstaArriba` y lista estática `DynamicBars.todas`.

## Mejora 6 — Los guardias perciben eventos del entorno ✓

**Implementación (6c):** `VisionSensor.ComprobarRejas()` se ejecuta cada frame. Consulta `DynamicBars.todas`, detecta cambios de estado en rejas dentro del campo de visión y relanza `EscanearEntorno()` para que `PatrolBehaviour` replantee la ruta. No usa eventos: polling directo sobre la propiedad `EstaArriba`.

**Implementación (6a):** `VisionSensor.ComprobarCofre()` se ejecuta cada frame. Cuando `GameManager.hasLoot == true` y el cofre está dentro de `viewDistance`, dispara `OnCofresDesaparecido` una sola vez. `WatchExitBehaviour` suscrito a ese evento corre hacia `zonaSalida` con offset aleatorio (Z solo positivo para no cruzar la puerta). Al llegar, cede el control a `PatrolBehaviour`. `VisionSensor` también suscribe a `MovementSensor.OnDestinoAlcanzado` para rescanear el entorno al llegar a cualquier destino, permitiendo que `PatrolBehaviour` calcule puntos desde la nueva posición.

---

# Mejoras planificadas

A continuación se describen los cambios que se quieren implementar en el proyecto, con su motivación y el impacto esperado en la arquitectura.

---

## Mejora 7: Implementación de la comunicación

--- A

## Mejora 8 — Revisar si el cofre ha sido robado o no

**Situación actual:** La ruta de patrulla se calcula una vez al inicio (raycast al punto más lejano). Una vez se ha visto al ladrón, se persigue hasta perderlo, sitio dónde se recalcula una nueva ruta de patrulla.

**Cambio deseado:**
- Cada x cantidad de tiempo aleatorio (configurable) uno de los agentes (preferiblemente el más cercano al cofre) debe parar de patrullar y acercarse al cofre para revisar que esté en buenas condiciones. Para ello, deberá informar de que es el esqueleto más cercano, para que sólo vaya el a revisar el cofre (que no vayan 2 o más a la vez, irá el más cercano, y no siempre tiene porque ser el mismo el más cercano). Hay 2 posibilidades:
  - Que el cofre haya sido robado, entonces, el esqueleto deberá avisar a los demás de ello y todos activan el modo "alerta".
  - Que el cofre esté intacto, caso en el cual el guardia simplemente vuelve a su punto de patrulla anterior. 

**Impacto en el código:**
- ?

---

## Resumen de impacto por fichero

| Script | Tipo de cambio |
|--------|---------------|

