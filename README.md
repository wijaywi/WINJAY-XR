# WINJAY XR

**"Models talk to models. Humans watch the state."**

Meta VR entry Track: Productivity.
Special award target: *Best Agentic Interaction / Boldest Original Concept*.

WINJAY XR turns multi-agent communication into a **spatial state machine**:
the user never reads agent chat, the user **watches state**.

```
RESEARCHER ──► ANALYZER ──► FALSIFIER ──► DECISION STATE
     │              │             │
     └──── semantic protocol ─────┘
```

In the spatial world:

| Semantic concept | Spatial representation |
|---|---|
| evidence | objects |
| hypothesis | nodes |
| uncertainty | visual state |
| conflict | physical tension |
| dependency | connection |
| provenance | source trail |
| state change | movement |
| contradiction | collision |

## Architecture principles

1. **WINJAY CORE = the truth layer.** State machine, event bus, transition
   validation, provenance, delta state. No Unity dependency — Unity is just
   one renderer (Web UI / Desktop / XR share the same state and protocol).
2. **Agents never mutate state directly.** The flow is always:
   `Agent → PROPOSE → State Engine → VALIDATE → COMMIT`.
   Stale proposals (old versions), illegal transitions, and unknown entities
   are rejected.
3. **Deterministic.** The same event order always produces the same state.
4. **Mock first, LLM later.** Researcher/Analyzer/Falsifier/Decision are
   mock agents; the `AgentProposal` interface does not change when they are
   replaced by real LLMs.

## State machine (v0.1)

```
                 ┌── SUPPORTED ──► RESOLVED
                 │
CREATED ──► INVESTIGATING
                 │
                 └── CONFLICTED ──► FALSIFYING ──► FAILED
```

Note: the transition table adds two edges required by the DemoScenario01
document (the document lists 6 core transitions, but its own demo scenario
needs a second supporting evidence item and a contradiction that arrives
after the hypothesis is already SUPPORTED):

- `SUPPORTED ──SUPPORT──► SUPPORTED` (additional supporting evidence)
- `SUPPORTED ──CONTRADICT──► CONFLICTED` (late contradiction)

## The Living Mind Map (hero demo)

The same truth layer, a second world: a multi-agent **mind map** grows around
one question — *"3-Day Vacation on a Limited Budget"* — while the human works
as a **curator** inside it. Three agents with visible personalities:

| Agent | Role in the map |
|---|---|
| **Explorer** (blue) | grows branches: Transport, Lodging, Activities, Food, Budget |
| **Connector** (green) | finds distant connections — teal "bridge" arcs + ghost suggestions |
| **Challenger** (orange) | marks tensions — red vibrating edges |

Edge semantics: plain = branch/part-of, green = **supports**, red vibrating =
**contradicts**, teal curved arc = unexpected **bridge**. Every node carries a
trail showing who created it — Explorer / Connector / Challenger / **you**.

The curator is not a user, it is a participant:
**prune** weak nodes, **lock** a decision gold (agents must work around it),
**speak** an idea into the map (your words become a node weighted higher than
agent nodes). Every curator action triggers real agent reactions.

Everything visible comes from the event log. Strength is pure arithmetic
(support +0.15, conflict −0.20, bridge +0.05, human links ×1.5, locked nodes
frozen) recomputed after every commit — ripples are genuine, not animated.
Rewinding the timeline = folding a shorter prefix of the journal.

- **Web viewer:** `http://localhost:8765/mind.html` — scrubber, inline
  inspector (accept/reject ghost, lock/unlock, prune), speak box.
- **Unity / Quest:** the app **starts in mind-map mode**; the MACHINE button
  switches to the state-machine world. The full story auto-runs, and a
  presenter can perform the curator steps live (pinch / click) — the scripted
  fallbacks detect the world state and skip themselves.
- **Full 10-minute beat sheet:** `Docs/MINDMAP_DEMO_SCRIPT.md`.

## Repository structure

```
WINJAY-XR/
├── README.md
├── WinjayCore/
│   ├── Winjay.Core/      # netstandard2.1 — truth layer (Unity-compatible)
│   ├── Winjay.Tests/     # xUnit — automated tests for every transition
│   └── Winjay.Host/      # console runner (Stage 2)
├── MockAgent/
│   └── Winjay.Agents/    # mock researcher/analyzer/falsifier/decision (Stage 2)
├── WebVisualizer/
│   └── Winjay.Gateway/   # ASP.NET Core Kestrel + WebSocket gateway,
│                         #   Three.js viewers: / (machine) + /mind.html (mind map)
├── Unity/                # Meta XR SDK project (Stage 4)
├── Tools/
│   ├── sync-unity-sources.bat       # Core/Agents → Unity Assets
│   └── unity-compile-check/check.sh # offline compile check (no license needed)
└── Docs/                 # demo scripts & submission (Stage 5)
```

## Project stages

1. **Stage 1** — WINJAY CORE: state machine + event bus + validation + automated tests.
2. **Stage 2** — Mock Agents + DemoScenario01 end-to-end + delta-state journal.
3. **Stage 3** — Winjay Gateway + spatial web visualizer (Three.js) over WebSocket.
4. **Stage 4** — Unity XR layer: Meta XR SDK + Meta XR Simulator.
5. **Stage 5** — Polish & submission: pinch interaction, icon + poster, demo < 3 min.
   (*Voice input* is cut: mock agents are deterministic scripts — there is no
   voice input that could be processed meaningfully, and the Meta Voice SDK
   cannot be tested without a headset. Voice interaction is noted as a
   post-hackathon next step.)

## Running the demo (Stage 2)

```bash
cd WinjayCore
dotnet test                                    # 70 automated tests
dotnet run --project Winjay.Host               # DemoScenario01 (conflict → FAILED → REJECT)
dotnet run --project Winjay.Host -- --resolve  # ScenarioResolve (survival → RESOLVED → ACCEPT)
dotnet run --project Winjay.Host -- --step     # step-by-step mode
```

## Running the spatial visualizer (Stage 3)

```bash
cd WebVisualizer/Winjay.Gateway
dotnet run -- --port 8765
# machine world:  http://localhost:8765/
# living mind map: http://localhost:8765/mind.html
```

The Kestrel gateway serves static pages + the `/ws` WebSocket. Every
connection gets its own **isolated Winjay world** (engine + agents + runner):

| Client message | Purpose |
|---|---|
| `{type:"run"}` | auto-run tick by tick |
| `{type:"step"}` | one manual tick |
| `{type:"reset"}` | fresh world |
| `{type:"speed", ms}` | tick interval (50–5000) |
| `{type:"scenario", name}` | `conflict` / `resolve` |

Server messages: `hello` (scenario + speed), `tick` (batch of proposal steps +
semantic events + new entity snapshot), `done` (tick count).

Spatial mapping in the Three.js scene:

| Entity | Representation |
|---|---|
| Agents (4) | glowing anchor core, spinning ring, pulse on action |
| Evidence | octahedron; color = source (Researcher cyan / Falsifier orange) |
| Hypothesis H001 | large sphere; color = state; trembles when CONFLICTED/FALSIFYING |
| Confidence | bar above H001 — green→red, live number |
| Support | green curve evidence → H001 |
| Contradiction | jagged red edge (physical tension) + X marker at CF001 |
| Conflict | red X, flash on CONFLICT_DETECTED |
| Decision | box; green ACCEPT / red REJECT |
| Provenance | particles flying agent → entity on ENTITY_CREATED |
| Spatial anchoring | faint floor lines to every entity |

Three.js (r160) and OrbitControls are vendored locally (`wwwroot/vendor/`) —
no CDN, no extra NuGet packages.

## Running in Unity XR (Stage 4)

Unity 6 LTS `6000.0.42f1` + Meta XR SDK 71.0.0 + Meta XR Simulator.
Full guide (installation, license, Simulator, Quest build):
**`Docs/UNITY_SETUP.md`**.

```
Unity/WinjayXR/
├── Packages/manifest.json     # Meta XR Core SDK 71 + Simulator 71 + OpenXR 1.13
└── Assets/
    ├── Winjay.Core/           # truth layer source (compiled directly by Unity)
    ├── Winjay.Agents/         # mock agent sources
    └── Scripts/               # XR renderer + hand-tracking interaction
```

- **The engine runs INSIDE Unity** — the state machine, mind engine, event
  bus, and scenario runners are exactly the same sources used by the web
  gateway and desktop host; Unity is only the third renderer (Web / Desktop /
  XR share state & journal).
- **Runtime bootstrap**: opening the project in any empty scene immediately
  becomes the Winjay world (camera, light, engine, view, control panel).
  Default mode: **living mind map** (MIND MAP ⇄ MACHINE button swaps worlds).
- **Passthrough MR**: the world floats in the user's real room.
- **Hand tracking + pinch**: RUN/STEP/RESET, the mind-map panel (rewind
  scrubber, SAY presets, ACCEPT/REJECT ghost, LOCK gold, PRUNE), and node
  cards are pinched directly with fingers. Long pinch (1.5 s) on a node locks
  it. Mouse fallback: click / right-drag orbit / scroll.
- **No headset**: Meta XR Simulator, or the plain editor.
- **Offline compile check**: `bash Tools/unity-compile-check/check.sh` —
  compiles all 48 Unity scripts with the dotnet SDK's Roslyn against the real
  Unity 6 reference assemblies (netstandard 2.1 profile), with the package
  APIs (InputSystem / Meta XR SDK 71 / OpenXR) mirrored as stubs; API shapes
  cross-checked against the real SDK 71 package source (internal
  `OVRHand.HandType`, `PointerPose` as Transform, `MetaXRFeature`).

## Definition of Done v0.1

- [x] State machine runs (without Unity)
- [x] Evidence / Hypothesis / Conflict / Decision objects
- [x] State transition validation (PROPOSE → VALIDATE → COMMIT)
- [x] Event bus + delta state
- [x] Mock Researcher / Analyzer / Falsifier / Decision (Stage 2)
- [x] End-to-end demo (Stage 2)
- [x] Spatial graph (Stage 3) — WebGateway + Three.js viewer
- [x] Living mind map: core engine + agents + scripted story (deterministic
      strength, lock/prune/ghost, journal replay)
- [x] Mind-map web viewer (`/mind.html`) — fold-from-log rewind + inspector
- [x] Unity XR layer (Stage 4) — both worlds (mind map + state machine),
      hand tracking, rewind scrubber, curator gestures — code-complete and
      compile-checked offline (remaining: first Unity launch = license +
      package import, a user step — see Docs/UNITY_SETUP.md)
- [ ] Demo video < 3 min + submission (Stage 5)


## Legal & Disclaimer

**IMPORTANT NOTICE:**
This software is provided purely as an experimental, open-source research module.
 As stated in the `LICENSE` (MIT License), this software is provided **"AS IS"**. The creators, authors, and contributors accept **ZERO LIABILITY** for hardware damage, or legal repercussions resulting from compiling, modifying, or integrating this code. 
