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
│   │   ├── CheckChestBehaviour.cs        ← [Mejora 7c] va al cofre y comunica resultado
│   │   ├── FollowGuardBehaviour.cs
│   │   ├── InvestigateBehaviour.cs
│   │   ├── PatrolBehaviour.cs
│   │   └── WatchBehaviour.cs
│   ├── Communication/                    ← [Mejora 7a] infraestructura base FIPA-ACL
│   │   ├── AgenteFIPAACL.cs
│   │   └── ACLMessage.cs
│   ├── Control/
│   │   ├── ActionProposal.cs
│   │   └── ControlSubsystem.cs
│   ├── Deliberative/
│   │   ├── DeliberativeLayer.cs
│   │   ├── GuardianDeliberativeLayer.cs  ← [Mejora 7c] reescrito con FSM ContractNet
│   │   └── PatrolDeliberativeLayer.cs    ← [Mejora 7c] reescrito con FSM ContractNet
│   ├── Reactive/
│   │   ├── ReactiveLayer.cs
│   │   ├── GuardianReactiveLayer.cs
│   │   └── PatrolReactiveLayer.cs
│   ├── Sensors/
│   │   ├── HearingSensor.cs
│   │   ├── MovementSensor.cs
│   │   ├── SwordHitBox.cs
│   │   └── VisionSensor.cs
│   └── Social/                           ← [Mejora 7b] capa social (lenguaje ACL)
│       └── SkeletonSocialLayer.cs
├── Camera/                               ← [Mejora 8] agente cámara
│   ├── CameraReactiveLayer.cs
│   ├── CameraDeliberativeLayer.cs
│   └── CameraSocialLayer.cs
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
| `Deliberative/PatrolDeliberativeLayer.cs` | [Mejora 7c] FSM ContractNet completa (iniciador y participante); sin lenguaje ACL |
| `Deliberative/GuardianDeliberativeLayer.cs` | [Mejora 7c] ídem para guardia estático |
| `Communication/AgenteFIPAACL.cs` | [Mejora 7a] Clase padre abstracta: inbox, envío unicast, registro estático, límite msgs/frame |
| `Communication/ACLMessage.cs` | [Mejora 7a] Contenedor de mensaje FIPA con todos sus campos |
| `Social/SkeletonSocialLayer.cs` | [Mejora 7b] Hereda AgenteFIPAACL; define todo el lenguaje ACL (performatives, payloads, API traducción), timer aleatorio coordinado |
| `Behaviours/CheckChestBehaviour.cs` | [Mejora 7c] Behaviour que ejecuta el compromiso ContractNet: ir al cofre y comprobar estado |
| `Camera/CameraReactiveLayer.cs` | [Mejora 8] Suscribe a VisionSensor; al detectar al ladrón activa CameraDeliberativeLayer para iniciar ContractNet |
| `Camera/CameraDeliberativeLayer.cs` | [Mejora 8] FSM solo Iniciador; arranca cuando la Reactiva avista al ladrón; nunca propone movimiento |
| `Camera/CameraSocialLayer.cs` | [Mejora 8] Hereda AgenteFIPAACL; vocabulario ContractNet del lado Iniciador (REQUEST, CFP, ACCEPT/REJECT_PROPOSAL) |
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

## Mejora 7a — Infraestructura base de mensajería ✓

**Implementación:** `Agent/Communication/ACLMessage.cs` es la clase plana con todos los campos FIPA-ACL (`performative`, `sender`, `receivers`, `content`, `conversationId`, `inReplyTo`, `protocol`, `replyBy`). `Agent/Communication/AgenteFIPAACL.cs` es el `MonoBehaviour` abstracto que proporciona: registro estático `Todos` (actualizado en `OnEnable`/`OnDisable`), cola `inbox` drenada en `Update` con límite `mensajesPorFrame`, envío unicast con `EnviarMensaje`, factoría `CrearMensaje` (rellena `sender` y genera GUID para `conversationId`), y hook virtual `ActualizarAgente` para que las subclases añadan lógica por frame sin sobreescribir `Update`.

## Mejora 7b — Capa Social ✓

**Implementación:** `Agent/Social/SkeletonSocialLayer.cs` hereda de `AgenteFIPAACL` y define todo el lenguaje ACL del protocolo Contract Net. Contiene las constantes de performatives, los structs de payload (`ContentRequest`, `ContentCFP`, `ContentPropose`, `ContentInform`), la API de métodos que llama la Deliberativa (sin tocar ACLMessage), los eventos que dispara hacia la Deliberativa (sin lenguaje FIPA), y el timer aleatorio coordinado mediante `TIMER_MAX`. Los mensajes con performative desconocido o content de tipo incorrecto generan automáticamente un `NOT_UNDERSTOOD` al emisor.

**Nota de diseño:** `InformarResultado` fue ampliado en Mejora 8 para aceptar `tarea` como parámetro y enviar a cualquier `AgenteFIPAACL` sin filtrar por tipo. `BroadcastInform` también acepta `tarea` como parámetro opcional. El único método aún acoplado a una tarea concreta es `EnviarCFP`, cuyo `ContentCFP` asume que el coste se calcula por distancia a `posicionReferencia`.

## Mejora 7c — Protocolo ContractNet en la Deliberativa ✓

**Implementación:**
- `Agent/Behaviours/CheckChestBehaviour.cs`: behaviour que va al cofre (a velocidad caminar) y al llegar (`MovementSensor.OnDestinoAlcanzado` con tolerancia de distancia para descartar llegadas espurias) consulta `VisionSensor.ComprobarEstadoCofre()` y dispara `OnComprobacionCompletada(bool cofreIntacto)`. Activación/desactivación explícita por la Deliberativa. No accede a `GameManager` directamente — el sensor actúa de intermediario respetando la arquitectura TM.
- `Agent/Deliberative/PatrolDeliberativeLayer.cs`: FSM completa (Iniciador + Participante) con estados `IDLE`, `INIT_WAIT_RESPONSES`, `INIT_WAIT_PROPOSALS`, `INIT_WAIT_RESULT`, `PART_VOLUNTEERED`, `PART_PROPOSED`, `PART_EXECUTING`. Cierra cada fase por número de respuestas recibidas o por `timeoutFase`. `GenerarPropuesta()` solo devuelve `ActionProposal` cuando está en `PART_EXECUTING` (delega en `CheckChestBehaviour.Evaluate()`); en cualquier otro estado devuelve `null`.
- `Agent/Deliberative/GuardianDeliberativeLayer.cs`: FSM solo Iniciador (estados `IDLE`, `INIT_WAIT_RESPONSES`, `INIT_WAIT_PROPOSALS`, `INIT_WAIT_RESULT`). Como Participante siempre responde `REFUSE` (no puede moverse). `GenerarPropuesta()` devuelve siempre `null`.
- `Agent/Control/ControlSubsystem.cs`: arbitraje tolerante a `null` en ambas propuestas (`if (propReactiva != null && propReactiva.solicitaControl)`).
- Difusión final: el ganador envía `INFORM_RESULT` solo al Iniciador (Contract Net) y `INFORM` a todos los demás agentes (fuera del Contract Net). El Iniciador solo cierra su FSM al recibir `INFORM_RESULT` — no hace broadcast. `SkeletonSocialLayer` reinicia el timer al recibir cualquiera de los dos mensajes con `tarea="comprobarCofre"`.
- `VisionSensor` tiene un nuevo método público `ComprobarEstadoCofre()` que devuelve `true` si el cofre está intacto, usado por `CheckChestBehaviour` al llegar al cofre. `VisionSensor.ComprobarCofre()` comprueba distancia + ángulo + raycast (igual que la detección del ladrón) para que solo detecten el robo los guardias que realmente tienen línea de visión al cofre.
- `CheckChestBehaviour`: `distanciaParada = 2f`, `toleranciaCofre = 3f` — el guardia se detiene a 2 metros del cofre y esa distancia cuenta como llegada válida para evitar que se suba encima o pase de largo.

## Mejora 8 — Cámaras de vigilancia ✓

**Implementación:** la cámara es un agente TM completo con tres capas y sin movimiento. `Camera/CameraReactiveLayer.cs` suscribe a `VisionSensor` y llama a `CameraDeliberativeLayer.IniciarProtocolo(pos)` al detectar al ladrón, una sola vez por avistamiento. `Camera/CameraDeliberativeLayer.cs` gestiona la FSM del protocolo Contract Net como Iniciador exclusivo (estados `IDLE → WAIT_RESPONSES → WAIT_PROPOSALS → WAIT_RESULT`); no hereda `DeliberativeLayer` porque la cámara no propone movimiento ni usa `ControlSubsystem`. `Camera/CameraSocialLayer.cs` hereda `AgenteFIPAACL` y usa los structs de `SkeletonSocialLayer` directamente para compatibilidad de tipos en el pattern matching.

Del lado de los guardias: `PatrolDeliberativeLayer` acepta la tarea `investigarAvistamiento` además de `comprobarCofre`; al ganar activa el nuevo `GoToPositionBehaviour` con la posición recibida en el CFP; al llegar notifica a la cámara con `social.InformarResultado` y hace broadcast `INFORM` al resto de patrulleros. `GuardianDeliberativeLayer` no necesitó cambios — ya rechazaba cualquier REQUEST. `SkeletonSocialLayer` fue actualizado: `InformarResultado` unificado (sin filtro de tipo, con parámetro `tarea`), `BroadcastInform` con parámetro `tarea`, resets de timer para `investigarAvistamiento`. `VisionSensor` recibió dos null-checks para funcionar sin `MovementSensor` ni referencia al cofre.

---

# Mejoras planificadas

A continuación se describen los cambios que se quieren implementar en el proyecto, con su motivación y el impacto esperado en la arquitectura.

---

## Mejora 7 — Implementación de la comunicación (FIPA-ACL Contract Net)

El objetivo es dotar a los agentes de comunicación real mediante el protocolo **FIPA Contract Net**. La arquitectura se divide en tres subtareas con dependencias en cascada: 7a → 7b → 7c.

La comunicación se añade **por encima** de la arquitectura TM existente. `ControlSubsystem`, `ReactiveLayer` y los sensores **no se modifican**.

---

### Mejora 7a — Infraestructura base de mensajería

**Ficheros nuevos:** `Agent/Communication/AgenteFIPAACL.cs`, `Agent/Communication/ACLMessage.cs`

Esta subtarea es el cimiento de todo el sistema de comunicación. No contiene lógica de protocolo ni conocimiento del juego: es pura infraestructura de transporte.

#### `ACLMessage`

Clase de datos plana que representa un mensaje FIPA-ACL. Contiene todos los campos estándar del protocolo:

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `performative` | `string` | Acto comunicativo (ej. `"cfp"`, `"propose"`). Siempre se rellena. |
| `sender` | `string` | Id del agente emisor. |
| `receivers` | `List<string>` | Id del único destinatario (unicast; en la práctica siempre contiene un elemento). |
| `content` | `object` | Payload del mensaje. Su tipo real lo conoce sólo la capa Social. |
| `conversationId` | `string` | GUID asignado por el Iniciador al abrir la conversación. Todos los mensajes de una misma negociación comparten este id. |
| `inReplyTo` | `string` | `conversationId` del mensaje al que responde, para encadenar turnos. |
| `protocol` | `string` | Siempre `"fipa-contract-net"` en este proyecto. |
| `replyBy` | `float` | Tiempo absoluto de juego (`Time.time`) antes del cual se espera respuesta. Usado para el deadline de CFP. |

> **Campos FIPA omitidos — `language` y `ontology`:** el estándar los incluye para entornos heterogéneos donde agentes de distintos dominios comparten plataforma. En este proyecto no son necesarios: `language` es redundante porque el `content` siempre es un struct C# tipado (el receptor ya sabe qué esperar según el `performative`); `ontology` es redundante porque solo existe un dominio semántico (el juego). Incluirlos añadiría campos que siempre estarían vacíos y que nadie leería.

#### `AgenteFIPAACL`

`MonoBehaviour` abstracto. Es la única clase del sistema que puede enviar y recibir mensajes. Todas las entidades comunicantes (esqueletos a través de su `SkeletonSocialLayer`, cámaras a través de su `CameraSocialLayer`) deben heredar de esta clase.

**Responsabilidades:**

- **Registro estático:** mantiene `AgenteFIPAACL.Todos`, lista de todas las instancias activas en escena. Permite a cualquier agente localizar a otros sin referencias directas en el Inspector. Se actualiza en `OnEnable` / `OnDisable`.
- **Inbox:** cola `Queue<ACLMessage>` privada. Cualquier agente puede depositarle mensajes llamando a `EnviarMensaje`.
- **Límite de procesamiento por frame:** el `Update()` de esta clase drena la cola de `inbox` procesando como máximo `mensajesPorFrame` mensajes (valor por defecto `2`, configurable en Inspector). Esto garantiza que una avalancha de mensajes no congele la pantalla. Los mensajes sobrantes esperan al frame siguiente.
- **API de envío (sólo unicast):** `EnviarMensaje(ACLMessage msg, AgenteFIPAACL destinatario)`. Deposita el mensaje directamente en el inbox del destinatario. **No existe una versión multicast o broadcast:** quien quiera enviar a varios agentes debe hacer un `foreach` explícito desde la capa Social.
- **Hook abstracto:** `protected abstract void ProcesarMensaje(ACLMessage msg)`. Cada subclase (`SkeletonSocialLayer`, `CameraSocialLayer`) implementa aquí cómo reacciona al recibir un mensaje.
- **Factoría de mensajes:** `CrearMensaje(string performative, string conversationId = null)` construye un `ACLMessage` con `sender` ya relleno y un `conversationId` nuevo (GUID) si no se proporciona uno.

---

### Mejora 7b — Capa Social

**Fichero nuevo:** `Agent/Social/SkeletonSocialLayer.cs` ✓ implementado

Esta subtarea define el **lenguaje** del sistema. Es la única capa que conoce FIPA-ACL: sabe qué significa `"cfp"`, cómo se estructura un `PROPOSE`, cuándo hay que responder `NOT_UNDERSTOOD`. Las capas Deliberativa y Reactiva **nunca ven un `ACLMessage`**.

No existe una clase `AgentLayer` intermedia abstracta. `SkeletonSocialLayer` hereda directamente de `AgenteFIPAACL` y contiene en sí misma todo el vocabulario y la lógica de comunicación de los esqueletos. `CameraSocialLayer` también hereda directamente de `AgenteFIPAACL` con su propio vocabulario mínimo. Esta decisión evita una jerarquía intermedia artificial cuyo único contenido sería constantes y structs compartidos.

#### `SkeletonSocialLayer`

Hereda de `AgenteFIPAACL`. Contiene todo el lenguaje ACL del protocolo Contract Net y la lógica de timer coordinado.

**Performatives:** constantes `string` estáticas definidas aquí, que representan todos los actos comunicativos del protocolo Contract Net:

```
REQUEST, AGREE, REFUSE, CFP, PROPOSE,
ACCEPT_PROPOSAL, REJECT_PROPOSAL,
INFORM_DONE, INFORM_RESULT, FAILURE,
NOT_UNDERSTOOD, CANCEL
```

**Payloads:** structs definidos dentro de `SkeletonSocialLayer` que definen el contenido de cada tipo de mensaje que lleva datos propios:

| Struct | Usado en | Campos |
|--------|----------|--------|
| `ContentRequest` | REQUEST | `string tarea` |
| `ContentCFP` | CFP | `string tarea`, `Vector3 posicionReferencia` |
| `ContentPropose` | PROPOSE | `float coste` |
| `ContentInform` | INFORM_RESULT | `string tarea`, `bool exito`, `object datos` |

`AGREE`, `REFUSE`, `ACCEPT_PROPOSAL`, `REJECT_PROPOSAL`, `FAILURE`, `CANCEL` y `NOT_UNDERSTOOD` no necesitan struct propio: el performative más el `conversationId` son suficientes para identificar su significado.

**API hacia arriba — métodos que la Deliberativa llama** (sin tocar ACL):

| Método | Acción interna |
|--------|----------------|
| `EnviarRequest(string tarea, List<AgenteFIPAACL> destinatarios)` | Crea ACLMessage(REQUEST, ContentRequest), pausa el timer propio a TIMER_MAX, lo envía a cada SkeletonSocialLayer en bucle `foreach` |
| `ResponderRequest(string convId, bool acepto, AgenteFIPAACL iniciador)` | Crea ACLMessage(AGREE o REFUSE) y lo envía al iniciador |
| `EnviarCFP(string convId, List<AgenteFIPAACL> participantes, Vector3 refPos, string tarea)` | Crea ACLMessage(CFP, ContentCFP) y lo envía a cada participante |
| `EnviarPropuesta(string convId, float coste, AgenteFIPAACL iniciador)` | Crea ACLMessage(PROPOSE, ContentPropose) y lo envía al iniciador |
| `AceptarPropuesta(string convId, AgenteFIPAACL ganador)` | Crea ACLMessage(ACCEPT_PROPOSAL) y lo envía al ganador |
| `RechazarPropuesta(string convId, AgenteFIPAACL perdedor)` | Crea ACLMessage(REJECT_PROPOSAL) y lo envía al perdedor |
| `InformarResultado(string convId, bool exito, object datos, List<AgenteFIPAACL> destinatarios)` | Crea ACLMessage(INFORM_RESULT, ContentInform) y lo envía a cada destinatario |
| `Rechazar(string convId, AgenteFIPAACL destinatario)` | Crea ACLMessage(REFUSE) — para rechazar un CFP o REQUEST cuando el agente está ocupado |
| `Fallar(string convId, AgenteFIPAACL iniciador)` | Crea ACLMessage(FAILURE) y lo envía al iniciador |
| `Cancelar(string convId, List<AgenteFIPAACL> participantes)` | Crea ACLMessage(CANCEL) y lo envía a cada participante |

**API hacia arriba — eventos que la Deliberativa escucha** (instrucciones desconstruidas, sin lenguaje):

| Evento | Payload | Cuándo se dispara |
|--------|---------|-------------------|
| `OnRequestReceived` | `string from, string tarea, string convId` | Al recibir REQUEST |
| `OnRequestResponseReceived` | `string from, bool acepto, string convId` | Al recibir AGREE o REFUSE a un REQUEST propio |
| `OnCFPReceived` | `string from, string tarea, Vector3 refPos, string convId` | Al recibir CFP |
| `OnProposalReceived` | `string from, float coste, string convId` | Al recibir PROPOSE |
| `OnProposalAccepted` | `string convId` | Al recibir ACCEPT_PROPOSAL |
| `OnProposalRejected` | `string convId` | Al recibir REJECT_PROPOSAL o REFUSE |
| `OnResultReceived` | `string from, bool exito, object datos, string convId` | Al recibir INFORM_RESULT |
| `OnFailureReceived` | `string convId` | Al recibir FAILURE |
| `OnCancellationReceived` | `string convId` | Al recibir CANCEL |

**Manejo automático de errores:** si llega un mensaje con performative desconocido o `content` de tipo incorrecto, `SkeletonSocialLayer` genera y envía un `NOT_UNDERSTOOD` al emisor sin notificar a la Deliberativa. Los `NOT_UNDERSTOOD` entrantes se ignoran silenciosamente para evitar bucles.

**Timer aleatorio coordinado:**

- `timerMin`, `timerMax` (configurable en Inspector, ej. 20–40 s): rango para el sorteo del timer.
- `TIMER_MAX` (constante muy alta, ej. `9999f`): valor que efectivamente "pausa" el timer.
- `timerActual`: se sortea en `[timerMin, timerMax]` al iniciar la escena.
- En cada `Update()`, si `timerActual > 0` decrementa con `Time.deltaTime`. Cuando llega a `0`, avisa a la Deliberativa mediante el evento `OnTimerExpired` para que intente ser Iniciador.

**Coordinación del timer mediante mensajes:**
- Al recibir un `REQUEST` → `timerActual = TIMER_MAX` (este agente sabe que hay una conversación en marcha).
- Al enviar un `REQUEST` (este agente es el Iniciador) → `timerActual = TIMER_MAX` en el propio agente.
- Al recibir `INFORM_RESULT` que cierra la conversación → sortear nuevo `timerActual ∈ [timerMin, timerMax]`.
- Si el timer llega a `0` pero en el mismo frame llega un `REQUEST` de otro agente, el mensaje del inbox tiene prioridad: el que llegó a inbox primero gana; el otro aborta su intento de iniciar y sortea un timer nuevo.

De esta forma la sincronización emerge de los propios mensajes FIPA: `REQUEST` = "estoy iniciando, pausaos"; `INFORM_RESULT` final = "conversación terminada, reiniciad vuestros timers".

---

### Mejora 7c — Protocolo ContractNet en la Deliberativa

**Ficheros modificados:** `Agent/Deliberative/PatrolDeliberativeLayer.cs`, `Agent/Deliberative/GuardianDeliberativeLayer.cs`, `Agent/Behaviours/PatrolBehaviour.cs`
**Fichero nuevo:** `Agent/Behaviours/CheckChestBehaviour.cs`

**Motivación (requisito de juego):** cada cierto tiempo aleatorio, uno de los guardias debe acercarse al cofre para comprobar que sigue intacto. No deben ir varios a la vez: solo el más cercano. Si el cofre ha sido robado, ese guardia avisa a todos y se activa el modo alerta; si está intacto, simplemente vuelve a patrullar. Este comportamiento se implementa íntegramente mediante el protocolo Contract Net: el timer aleatorio decide cuándo actuar, el CFP con coste = distancia al cofre decide quién va, y el INFORM_RESULT propaga el resultado a todos.

Esta subtarea implementa la lógica de alto nivel del protocolo. La Deliberativa conoce el **flujo de pasos** del protocolo, pero no conoce el lenguaje: solo llama a métodos de `SkeletonSocialLayer` y escucha sus eventos.

#### Roles por tipo de agente

**`GuardianDeliberativeLayer` — solo Iniciador**

El guardia estático no puede entrar al castillo, por lo que nunca se ofrece como candidato para ir al cofre. Su único rol es iniciar la conversación y propagar el resultado. Al recibir un REQUEST de otro Iniciador, responde REFUSE directamente.
- `GenerarPropuesta()` siempre devuelve null — el guardia no se mueve.
- `WatchBehaviour` sigue siendo fallback permanente en la Reactiva sin cambios, ya que la comunicación corre en paralelo sin competir con el movimiento.

**`PatrolDeliberativeLayer` — Iniciador y Participante**

El patrullero puede tanto iniciar una conversación (si su timer expira primero) como participar en una iniciada por otro agente.
- Como Iniciador: igual que el guardián.
- Como Participante: responde AGREE o REFUSE al REQUEST, propone su coste si recibe CFP, ejecuta `CheckChestBehaviour` si gana.
- `GenerarPropuesta()` devuelve `ActionProposal` hacia el cofre si está en estado EJECUTANDO, si no null.

#### Cómo la Deliberativa obtiene el control (patrullero)

`PatrolBehaviour` deja de ser fallback permanente. Cuando el patrullero ya tiene un destino activo y el NavMeshAgent está en camino, `PatrolBehaviour.Evaluate()` devuelve `null`. El NavMeshAgent mantiene su destino por inercia — nadie le ha dicho que pare. Al llegar (`OnDestinoAlcanzado`) o al detectar un cambio de entorno (`OnEscaneoCompletado`), `PatrolBehaviour` calcula un nuevo destino y devuelve `ActionProposal` una sola vez.

Durante los frames en que `PatrolBehaviour` devuelve `null` y la Reactiva entera devuelve `null`, el `ControlSubsystem` cede a la Deliberativa, que puede gestionar la FSM del ContractNet con normalidad.

#### Flujo completo del protocolo

**Fase 0 — Reclutamiento:**

```
Iniciador (guardián o patrullero): timer llega a 0
  → social.EnviarRequest(tarea, todos los SkeletonSocialLayer del registro)
  → espera respuestas hasta replyBy

Patrullero que recibe REQUEST:
  → si está libre: social.ResponderRequest(convId, acepto=true)
  → si está ocupado: social.ResponderRequest(convId, acepto=false)

Guardián que recibe REQUEST de otro Iniciador:
  → social.ResponderRequest(convId, acepto=false)  — nunca puede ir al cofre

Iniciador: recoge AGREEs y REFUSEs antes del deadline
  → construye lista de voluntarios (solo patrulleros que dijeron AGREE)
  → si nadie aceptó: aborta, sortea timer nuevo
  → si hay voluntarios: pasa a Fase 1
```

**Fase 1 — ContractNet:**

```
Iniciador:
  → social.EnviarCFP(convId, voluntarios, posicionCofre)
  → espera propuestas hasta replyBy

Patrullero voluntario: recibe CFP
  → calcula coste = distancia(self, posicionCofre)
  → social.EnviarPropuesta(convId, coste)
  (si ya no puede: social.Rechazar(convId))

Iniciador: recibe PROPOSEs antes del deadline
  → el de menor coste gana
  → social.AceptarPropuesta(convId, ganador)
  → social.RechazarPropuesta(convId, perdedor)  [bucle sobre los no ganadores]

Patrullero ganador: recibe ACCEPT_PROPOSAL
  → entra en estado EJECUTANDO
  → GenerarPropuesta() devuelve ActionProposal hacia el cofre

Patrullero perdedor: recibe REJECT_PROPOSAL
  → vuelve a estado normal

Patrullero ganador: llega al cofre (via CheckChestBehaviour)
  → si GameManager.hasLoot == true  → social.InformarResultado(convId, exito=false, datos="cofre robado")
  → si GameManager.hasLoot == false → social.InformarResultado(convId, exito=true, datos="cofre intacto")
  → vuelve a patrullar

Iniciador: recibe INFORM_RESULT
  → si exito==false: envía INFORM_RESULT(alerta) a todos los del REQUEST [bucle]
  → si exito==true:  envía INFORM_RESULT(fin) a todos los del REQUEST [bucle]

Todos los que reciben el INFORM_RESULT de cierre:
  → SkeletonSocialLayer sortea timer nuevo
```

**Excepciones:**
- `NOT_UNDERSTOOD` lo gestiona `SkeletonSocialLayer` automáticamente; la Deliberativa no lo ve.
- `FAILURE` → Iniciador lo trata como `INFORM_RESULT(exito=false)`.
- `CANCEL` → patrulleros en estado EJECUTANDO abortan y vuelven a patrullar.

#### `CheckChestBehaviour`

Behaviour (implementa `IBehaviour`) que `PatrolDeliberativeLayer` activa cuando el patrullero ha ganado el contrato.

- `Initialize(Transform cofre, MovementSensor movSensor, PatrolDeliberativeLayer deliberativa)`.
- `Evaluate()`: devuelve `ActionProposal` con destino = posición del cofre a velocidad caminar mientras no ha llegado.
- Suscribe a `MovementSensor.OnDestinoAlcanzado`: al llegar, lee `GameManager.hasLoot`, notifica a la Deliberativa con el resultado y se desactiva.

#### Regla de prioridad con ControlSubsystem

La Reactiva siempre puede interrumpir al patrullero comprometido (si ve al ladrón, lo persigue). Al ceder de nuevo, la Deliberativa retoma `CheckChestBehaviour` — el NavMeshAgent seguía teniendo el destino del cofre en cola.

Modificarlo para que tolere nulls: if (propReactiva != null && propReactiva.solicitaControl)




## Mejora 8 — Cámaras de vigilancia

**Ficheros nuevos:** `Camera/CameraReactiveLayer.cs`, `Camera/CameraDeliberativeLayer.cs`, `Camera/CameraSocialLayer.cs`

La cámara es un agente TM completo, equivalente en estructura a los esqueletos. Tiene capa reactiva, capa deliberativa y capa social. No tiene `ControlSubsystem` ni `AgentActuator` porque no se mueve, pero ejerce el protocolo Contract Net en el rol de **Iniciador exclusivo**: nunca es participante.

**Disparador reactivo:** cuando el `VisionSensor` detecta al ladrón, la `CameraReactiveLayer` notifica a la `CameraDeliberativeLayer`, que lanza inmediatamente el protocolo. A diferencia de los esqueletos, cuyo iniciador es el timer, el iniciador de la cámara es un evento de percepción.

La separación de responsabilidades se mantiene igual que en los esqueletos: `CameraReactiveLayer` percibe y decide cuándo hay que actuar; `CameraDeliberativeLayer` gestiona el flujo del protocolo sin conocer ACL; `CameraSocialLayer` traduce cada paso al lenguaje FIPA.

#### `CameraReactiveLayer`

`MonoBehaviour` que actúa como sensor de disparo.

- Referencia a `VisionSensor` (el mismo componente reutilizado de los guardias, colocado en el GameObject de la cámara).
- En `Awake()` suscribe a `VisionSensor.OnLadronAvistado` y `VisionSensor.OnLadronPerdido`.
- Al dispararse `OnLadronAvistado`: llama a `deliberativa.IniciarProtocolo(sensor.PosicionLadron)`.
- Sólo activa el protocolo **una vez por avistamiento**: el flag `protocoloActivo` lo bloquea hasta recibir `OnLadronPerdido`. El protocolo en curso no se cancela — la deliberativa lo cierra por su cuenta.

#### `CameraDeliberativeLayer`

`MonoBehaviour` independiente. **No hereda de `DeliberativeLayer`** por tres razones:
1. `DeliberativeLayer` exige implementar `GenerarPropuesta() → ActionProposal`, un contrato para proponer movimiento que la cámara nunca necesita — devolvería `null` siempre.
2. `ControlSubsystem` no existe en la cámara: el árbitro resuelve conflictos entre movimiento reactivo y deliberativo; la cámara no tiene movimiento, así que no hay conflicto que arbitrar.
3. El ciclo de vida es distinto: las deliberativas de los esqueletos son *preguntadas* cada frame por `ControlSubsystem`; `CameraDeliberativeLayer` es *notificada* por la reactiva al ocurrir un evento de percepción.

Gestiona la FSM del protocolo Contract Net como **Iniciador exclusivo**. Conoce la secuencia de pasos del protocolo, pero no el lenguaje ACL: llama a métodos de `CameraSocialLayer` exactamente igual que `PatrolDeliberativeLayer` llama a `SkeletonSocialLayer`.

**Estados FSM:**

| Estado | Descripción |
|--------|-------------|
| `IDLE` | Esperando avistamiento. |
| `WAIT_RESPONSES` | REQUEST enviado; esperando AGREEs y REFUSEs hasta `replyBy`. |
| `WAIT_PROPOSALS` | CFP enviado a voluntarios; esperando PROPOSEs hasta `replyBy`. |
| `WAIT_RESULT` | Ganador aceptado; esperando INFORM_RESULT del ejecutor (opcional; puede cerrar en WAIT_PROPOSALS). |

**Lógica general:**

- Al recibir `IniciarProtocolo(posicion)` desde la Reactiva: guarda la posición del avistamiento, pasa a `WAIT_RESPONSES` y llama a `social.EnviarRequest("investigarAvistamiento", todosLosEsqueletos)`.
- En `WAIT_RESPONSES`: recoge AGREEs y REFUSEs (vía evento `OnRequestResponseReceived` de la capa social). Cierra la fase cuando ha recibido respuesta de todos los destinatarios o cuando expira `replyBy`. Si no hay voluntarios, vuelve a `IDLE`.
- En `WAIT_PROPOSALS`: recoge PROPOSEs. Cierra al recibir todos o al expirar. Acepta el de menor coste, rechaza el resto. Pasa a `WAIT_RESULT` (o a `IDLE` si hubo 0 propuestas).
- En `WAIT_RESULT`: espera `INFORM_RESULT` del ganador; al recibirlo (o al expirar timeout) vuelve a `IDLE`.
- `GenerarPropuesta()` no existe: esta clase no implementa `DeliberativeLayer`. La cámara no interviene en ningún `ControlSubsystem`.

**Rol Participante:** la cámara nunca responde a un REQUEST ajeno. Si `CameraSocialLayer` recibe un REQUEST, la deliberativa lo ignora (o la social responde REFUSE automáticamente).

#### `CameraSocialLayer`

Hereda de `AgenteFIPAACL`. Define el vocabulario del lado Iniciador del protocolo Contract Net (los performatives coinciden en valor de cadena con los de `SkeletonSocialLayer` para que los guardias los interpreten correctamente).

**Performatives definidos (solo los necesarios para el Iniciador):**

```
REQUEST, CFP, ACCEPT_PROPOSAL, REJECT_PROPOSAL, REFUSE
```

**Payloads:** usa directamente los structs de `SkeletonSocialLayer` (`ContentRequest`, `ContentCFP`, `ContentPropose`, `ContentInform`). Esto es necesario para que el pattern matching en `SkeletonSocialLayer.ProcesarMensaje` funcione correctamente al recibir mensajes de la cámara — si fueran tipos distintos con los mismos campos, el `is ContentRequest` fallaría.

**API hacia arriba — métodos que `CameraDeliberativeLayer` llama:**

| Método | Acción interna |
|--------|----------------|
| `EnviarRequest(string tarea, List<AgenteFIPAACL> destinatarios)` | Crea ACLMessage(REQUEST) y lo envía a cada destinatario |
| `EnviarCFP(string convId, List<AgenteFIPAACL> voluntarios, Vector3 posicion, string tarea)` | Crea ACLMessage(CFP, ContentCFP) y lo envía a cada voluntario |
| `AceptarPropuesta(string convId, AgenteFIPAACL ganador)` | Crea ACLMessage(ACCEPT_PROPOSAL) y lo envía al ganador |
| `RechazarPropuesta(string convId, AgenteFIPAACL perdedor)` | Crea ACLMessage(REJECT_PROPOSAL) y lo envía al perdedor |

**API hacia arriba — eventos que `CameraDeliberativeLayer` escucha:**

| Evento | Payload | Cuándo se dispara |
|--------|---------|-------------------|
| `OnRequestResponseReceived` | `string from, bool acepto, string convId` | Al recibir AGREE o REFUSE |
| `OnProposalReceived` | `string from, float coste, string convId` | Al recibir PROPOSE |
| `OnResultReceived` | `string from, bool exito, object datos, string convId` | Al recibir INFORM_RESULT del ganador |

`ProcesarMensaje(ACLMessage msg)` descarta cualquier otro mensaje (NOT_UNDERSTOOD, CANCEL, etc.) o responde REFUSE si llega un REQUEST ajeno.

#### Integración en los guardias

Cuando un guardia recibe un REQUEST con `tarea="investigarAvistamiento"`:
- `PatrolDeliberativeLayer`: responde AGREE si está en `IDLE`, REFUSE si está ejecutando otra tarea. Guarda `tareaActual = "investigarAvistamiento"`. Si recibe CFP, guarda `posicionInvestigacion = refPos` y propone `coste = distancia(self, refPos)`. Si gana, activa `GoToPositionBehaviour` con esa posición. Al llegar, envía `INFORM_RESULT` a la cámara vía `social.InformarResultado` y hace broadcast `INFORM` al resto de patrulleros para que reinicien sus timers.
- `GuardianDeliberativeLayer`: responde REFUSE siempre (ya lo hacía para cualquier REQUEST).

#### `GoToPositionBehaviour`

Behaviour nuevo (`Agent/Behaviours/GoToPositionBehaviour.cs`) activado por `PatrolDeliberativeLayer` al ganar el contrato `investigarAvistamiento`. Equivalente a `CheckChestBehaviour` pero para una posición `Vector3` arbitraria en vez de un `Transform` fijo. Filtra llegadas espurias por distancia igual que `CheckChestBehaviour`. Dispara `OnLlegadaCompletada` al confirmar la llegada.

#### Configuración en Unity

La cámara es un GameObject con:
- `VisionSensor` (ajustar `viewDistance`, `viewAngle` e `obstacleMask` según el emplazamiento)
- `CameraReactiveLayer`
- `CameraDeliberativeLayer`
- `CameraSocialLayer`

No necesita NavMeshAgent, `NoiseEmitter`, ni `HearingSensor`.

---

## Resumen de impacto por fichero

| Script | Tipo de cambio |
|--------|---------------|
| `Communication/AgenteFIPAACL.cs` | **Nuevo** — clase padre abstracta: inbox, `EnviarMensaje` unicast, registro estático, límite msgs/frame |
| `Communication/ACLMessage.cs` | **Nuevo** — contenedor con todos los campos FIPA-ACL |
| `Social/SkeletonSocialLayer.cs` | **Nuevo** ✓ — `: AgenteFIPAACL`; todo el lenguaje ACL (performatives, payloads, API, eventos), timer coordinado via TIMER_MAX, NOT_UNDERSTOOD automático; `InformarResultado` unificado (sin filtro de tipo, con parámetro `tarea`); `BroadcastInform` con parámetro `tarea`; resets de timer para `investigarAvistamiento` |
| `Deliberative/PatrolDeliberativeLayer.cs` | **Reescrito** — FSM ContractNet Iniciador y Participante; maneja tareas `comprobarCofre` e `investigarAvistamiento`; activa `GoToPositionBehaviour` al ganar contrato de avistamiento |
| `Deliberative/GuardianDeliberativeLayer.cs` | **Reescrito** — solo Iniciador; responde REFUSE a cualquier REQUEST recibido; `GenerarPropuesta()` siempre null |
| `Behaviours/CheckChestBehaviour.cs` | **Nuevo** — behaviour que ejecuta el compromiso ganado: ir al cofre, comprobar `hasLoot`, notificar a Deliberativa |
| `Behaviours/PatrolBehaviour.cs` | **Modificado** — devuelve null cuando el NavMeshAgent ya tiene destino activo; solo devuelve ActionProposal al calcular un nuevo punto de patrulla |
| `Camera/CameraReactiveLayer.cs` | **Nuevo** — suscribe a VisionSensor; activa CameraDeliberativeLayer al avistamiento |
| `Camera/CameraDeliberativeLayer.cs` | **Nuevo** — FSM ContractNet solo Iniciador; disparado por percepción, no por timer |
| `Camera/CameraSocialLayer.cs` | **Nuevo** — `: AgenteFIPAACL`; vocabulario Iniciador (REQUEST, CFP, ACCEPT/REJECT_PROPOSAL); usa structs de `SkeletonSocialLayer` para compatibilidad de tipos |
| `Behaviours/GoToPositionBehaviour.cs` | **Nuevo** — behaviour que mueve a un `Vector3` arbitrario; filtra llegadas espurias por distancia; dispara `OnLlegadaCompletada` |
| `Control/ControlSubsystem.cs` | **Sin cambios** |
| `Reactive/PatrolReactiveLayer.cs` | **Sin cambios** — la cadena devuelve null de forma natural cuando PatrolBehaviour cede |
| `Reactive/GuardianReactiveLayer.cs` | **Sin cambios** — WatchBehaviour sigue como fallback permanente |
| `Sensors/VisionSensor.cs` | **Modificado** — null-check en `MovementSensor` (la cámara no lo tiene) y en `cofre` (la cámara no tiene referencia al cofre) |
| `Sensors/HearingSensor.cs` | **Sin cambios** |
| `Deliberative/DeliberativeLayer.cs` | **Sin cambios** (la firma abstracta sigue siendo válida) |

## Mejora 9 — Añadir más comportamientos vinculados a la comunicación
...

**Nota de implementación:** si se añaden nuevas tareas al protocolo Contract Net, `InformarResultado` ya acepta `tarea` como parámetro. El único punto que requeriría cambio es `EnviarCFP` si la nueva tarea necesitara un payload de CFP distinto a `posicionReferencia`.