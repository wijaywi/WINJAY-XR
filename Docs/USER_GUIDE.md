 WINJAY XR - User Guide & Feature Reference

"Models talk to models. Humans watch the state."

This document explains how to use WINJAY XR and describes every feature of the
application. It is written for judges, reviewers, and anyone running the demo.

------------

 1. What is WINJAY XR?

WINJAY XR is a "spatial multi-agent system" where AI agents negotiate with
each other through a shared map that you can stand inside. You never read chat
lines or agent messages. Instead, you "watch state": colors, positions,
strength numbers, and edge types tell the whole story.

The human is not a user - the human is the "curator". You prune weak ideas,
lock decisions gold (agents must work around them), accept ghost suggestions,
and speak your own ideas into the map. Every curator action triggers real
agent reactions.

There are two worlds in one engine:

| World | Purpose |
|------------|------------|
| "Living Mind Map" (default) | A mind map grows around "3-Day Vacation on a Limited Budget". Three agents (Explorer, Connector, Challenger) grow, connect, and challenge ideas while you curate. This is the hero demo. |
| "Machine Scenario" | The original evidence → hypothesis → falsify → decision state machine. Press MACHINE to switch. |

Both worlds share the same truth layer (`Winjay.Core`), the same event journal,
and the same deterministic arithmetic. Strength, ripples, day assignments, and
the blinking unresolved node are all recomputed from live state - nothing is
animated fake.

------------

 2. Running the Application

 Web viewer (fastest to rehearse)

```bash
cd WebVisualizer/Winjay.Gateway
dotnet run -- --port 8765
```

- Machine world: `http://localhost:8765/`
- Living Mind Map: `http://localhost:8765/mind.html`

The web viewer includes a rewind scrubber, inline inspector (accept/reject
ghost, lock/unlock, prune), a SAY box, and a speed slider (80–1500 ms).

 Unity / Meta Quest

Open the Unity project (`Unity/WinjayXR`) in Unity 6 LTS `6000.0.42f1`.
Press "Play" - the mind map builds itself. Full installation, license,
Simulator, and Quest APK build instructions: "`Docs/UNITY_SETUP.md`".

In the headset: passthrough MR is on (the world floats in your real room),
hand tracking is active, and all controls are pinched directly with fingers.

On desktop without a headset: use the mouse. Right-click-drag orbits the
camera, scroll zooms, left-click selects nodes and presses buttons.

------------

 3. Controls at a Glance

| Action | Web | Unity (mouse) | Unity (headset) |
|------------|------------|------------|------------|
| Run / pause | RUN button | RUN button | pinch RUN |
| One action | STEP button | STEP button | pinch STEP |
| Fresh world | RESET button | RESET button | pinch RESET |
| Inspect a node | click it | left-click it | pinch it |
| Prune / lock / accept | inspector buttons | card buttons | pinch card buttons |
| Lock (shortcut) | - | - | hold pinch on a node for 1.5 s |
| Speak an idea | type in SAY bar + SPEAK | SAY preset buttons | pinch SAY preset buttons |
| Rewind | drag the rewind slider | drag the slider handle | grab the handle |
| Jump to live | LIVE button | LIVE button | pinch LIVE |
| Switch to Machine scenario | - | MACHINE button | pinch MACHINE |

------------

 4. The Living Mind Map - Features

 4.1 Agents and Their Roles

Three agents with visible personalities negotiate the map:

| Agent | Color | Role |
|------------|------------|------------|
| "Explorer" | Cyan (4FC3F7) | Grows branches and fills them with ideas: Transport, Lodging, Activities, Food, Budget. Every idea starts at strength 0.50 - nothing is trusted yet. |
| "Connector" | Green (81C784) | Finds distant connections the Explorer cannot see. Creates green "support" edges (+0.15 strength) and teal curved "bridge" arcs (+0.05 to both ends). Also floats "ghost suggestions" when the map loses coverage. |
| "Challenger" | Orange (FFB74D) | Marks tensions. Creates red vibrating "conflict" edges (−0.20 strength to the target). Does not delete anything - just makes collisions visible. |

Every node carries a colored trail showing who created it. Human ideas have a
gold trail and start at strength 0.70; human links carry 1.5× weight.

 4.2 Edge Types

| Edge | Appearance | Meaning |
|------------|------------|------------|
| Branch | Plain line | Part-of / belongs-to relationship |
| Support | Green curve | One idea strengthens another (+0.15) |
| Conflict | Red vibrating line | Two ideas collide (−0.20 to target) |
| Bridge | Teal curved arc | Unexpected cross-domain connection (+0.05 to both) |

 4.3 Node States

| Visual | Meaning |
|------------|------------|
| Solid sphere + colored trail | Active node, owned by its creator |
| Translucent pulsing sphere | "Ghost suggestion" - unproven, strength 0.25, must earn support |
| Gold ring clamped around node | "Locked" - frozen by the curator; agents must plan around it |
| Blinking red | Unresolved - the weakest open idea; the map refuses to pretend it is fine |
| Shrinking to nothing | Being pruned |
| Colored disc under a node | Assigned to Day 1, Day 2, or Day 3 |

 4.4 Strength Arithmetic

Strength is pure arithmetic, recomputed after every commit:

| Event | Effect |
|------------|------------|
| New node (agent) | Starts at 0.50 |
| New node (human) | Starts at 0.70 |
| Ghost suggestion | Starts at 0.25 |
| Support edge added | +0.15 to target |
| Conflict edge added | −0.20 to target |
| Bridge arc added | +0.05 to both ends |
| Human link | ×1.5 multiplier |
| Locked node | Frozen - strength no longer changes |

 4.5 Curator Actions

You are the curator. Your actions are first-class events in the journal:

- "Prune" - Select a node and press PRUNE. It shrinks to nothing and its
  edges die with it. Agents react: if pruning removes coverage, the Connector
  may float a ghost suggestion to fill the gap.

- "Lock (Gold)" - Select a node and press LOCK (GOLD). A gold ring clamps
  around it. The node's strength freezes and agents must plan around it. Gold
  is the curator's word and outranks any number. Long-pinch (1.5 s) on a node
  in the headset also locks it (shortcut - easy to trigger accidentally;
  prefer the card button).

- "Unlock" - Press UNLOCK on a locked node to release it back to normal
  strength arithmetic.

- "Accept Ghost" - When a ghost suggestion appears (translucent, pulsing),
  select it and press ACCEPT. It becomes a real node at strength 0.25 and
  must earn support. The Connector typically supports it immediately (+0.15).

- "Reject Ghost" - Press REJECT on a ghost to dismiss it permanently.

- "Say" - Press one of the SAY preset buttons (or type in the web SAY bar).
  Your words become a gold-trail node attached to the root. Human ideas start
  at 0.70 and human links carry 1.5× weight. The map answers: the Challenger
  may attack a conflicting idea, and the Explorer may grow an alternative.

  Preset lines:
  - "I easily get motion sick" - the scripted showcase node
  - "Slow mornings, please" - good for Q&A improvisation
  - "Rent a scooter" - shows collisions can hit anything

 4.6 Rewind and Replay

The entire history is one event journal. Drag the rewind slider (web) or grab
the handle (Unity) to scrub time. Nodes vanish, edges fold away, colors return
to their creators. Stop anywhere and inspect the state at that moment.

Press "LIVE" to jump back to the present.

Rewinding is not a video - it is folding a shorter prefix of the journal and
recomputing everything from scratch. The same event order always produces the
same state.

 4.7 Day Assignment

When the story settles, the Explorer assigns days:

- "Day 1": Late arrival Day 1 + Night train
- "Day 2": Relaxed beach day (gold) + Sunset at the cliff
- "Day 3": Street food stall + Homestay in the village

Colored discs glow under each day's nodes. One idea keeps blinking red -
"4-hour road trip", the weakest open idea at 0.30. The map honestly shows
what it has not decided.

------------

 5. The Control Panel

The panel rides with the camera (screen-right, below the node band) so it
never overlaps the map. In the headset it floats in front of the wearer.

| Element | Purpose |
|------------|------------|
| Title | "WINJAY XR - LIVING MIND MAP" |
| Status line | Tick count, event count, running/settled/ready |
| Step note | Last actor + action (e.g., "Connector · supports SUNSET→SCN") |
| Settle note | Status line when settled and live |
| REWIND track + handle | Grab and drag to scrub time |
| LIVE button | Jump back to the present |
| View label | "live - N events" or "viewing X / N" |
| RUN / STEP / RESET | Playback controls |
| MACHINE | Switch to the original state-machine scenario |
| SAY presets | Three voice-placeholder buttons |

------------

 6. The Node Card

Pinch or click a node to open its floating card. The card shows:

- "Title" - the node's label in quotes
- "Meta" - creator, strength, phase, lock status, ghost status
- "Why" - context-sensitive hint (e.g., "accept to bring it into the map")
- "Action buttons":
  - Ghost nodes: ACCEPT / REJECT
  - Regular nodes: LOCK (GOLD) / UNLOCK, PRUNE (not on root), CLOSE

The card is opaque so map text behind it cannot ghost through.

------------

 7. The Machine Scenario

Press "MACHINE" to switch from the mind map to the original state-machine
world. This is the same engine with a different face:

```
                 ┌── SUPPORTED ──► RESOLVED
                 │
CREATED ──► INVESTIGATING
                 │
                 └── CONFLICTED ──► FALSIFYING ──► FAILED
```

Agents: Researcher (cyan), Analyzer, Falsifier (orange), Decision. Evidence
appears as octahedra, hypotheses as large spheres, contradictions as jagged
red edges. Confidence bars, provenance particles, and spatial anchoring make
the state machine visible.

Controls are the same: RUN / STEP / RESET, plus the rewind scrubber.

------------

 8. Technical Notes

- "Deterministic": The same proposal order always produces the same state
  and visuals. This is the foundation for demo replay and recording.

- "One engine, three renderers": Console host, web viewer (Three.js), and
  Unity XR all fold the same event model. Unity runs the engine locally - no
  server needed in the headset.

- "Mock first, LLM later": The `IWinjayAgent` interface does not change
  when agents are replaced with real LLMs. API keys never enter Unity.

- "No chat": The user only sees state (colors, tension, strength). The
  human is the curator - prune, lock, speak - and the agents react to those
  interventions like any other world event.

- "Voice placeholder": The SAY buttons process their preset text as a
  human-input node. Headset-mic speech-to-text is the post-hackathon upgrade
  (the Meta Voice SDK cannot be tested without a physical headset).

- "Pinned versions": Unity 6000.0.42f1, Meta XR SDK 71.0.0, OpenXR 1.13.1,
  XR Hands 1.5.0, InputSystem 1.11.2, XR Management 4.5.0.

------------

 9. Demo Script and Submission

- "Full 10-minute beat sheet": `Docs/MINDMAP_DEMO_SCRIPT.md`
- "3-minute video cut": See the table at the bottom of the demo script
- "Submission checklist": `Docs/SUBMISSION_CHECKLIST.md`
- "Unity setup and troubleshooting": `Docs/UNITY_SETUP.md`

------------

 10. Troubleshooting

| Symptom | Cause | Fix |
|------------|------------|------------|
| "Unable to set TrackingOrigin" spam in Console | Editor Play without a live XR session creates OVRManager, which retries against a non-running subsystem | Fixed automatically - the app skips Meta XR setup when no XR display is active. Desktop preview mode works cleanly. |
| Text overlapping in Game view | Panel was in the map's sightline | Fixed - panel now rides with the camera at a fixed screen slot. Backgrounds are opaque. |
| Simulator activated but no hand tracking | Meta XR Simulator fakes hmdPresent but may fail xrCreateSession | Treat as render-only check. Hand tracking requires a physical headset or a working Simulator session. |
| Android build fails "Android SDK not found" | SDK/NDK modules not installed | Install via Unity Hub (Installs → gear → Add modules) or follow the manual extraction guide in UNITY_SETUP.md. |
| Menu WINJAY not visible | Looking in the wrong dropdown | WINJAY is a top-level menu bar item, between Meta and Window. Not inside Meta XR Tools. |

------------

 11. Frequently Asked Questions

 General

"Q: Is WINJAY XR a chatbot in VR?"
No. There is no chat interface, no text bubbles, and no conversational AI you
talk to. Agents communicate through a shared state machine; the human watches
state (colors, strength numbers, edge types) and steers it with curator
actions. The tagline is literal: "Models talk to models. Humans watch the
state."

"Q: Why are the agents mock / scripted instead of real LLMs?"
Determinism. The same event order always produces the same map, which makes
the demo reproducible, replayable, and recordable. The `IWinjayAgent` interface
is designed so that real LLMs can be swapped in later without changing the
protocol - API keys never enter Unity. This is stated openly in the demo and
is a technical strength, not a limitation.

"Q: Does the app require an internet connection?"
No. The Unity version runs the engine entirely locally inside the headset or
editor. The web viewer runs on a local Kestrel server (`localhost:8765`). No
cloud services, no external APIs.

"Q: What happens if I do nothing and just watch?"
The story auto-runs from start to finish. The agents grow, connect, and
challenge the map on their own. Your interventions are optional but they change
the outcome - prune early and the Connector floats a ghost; say something and
the Challenger attacks a conflicting idea.

 Mind Map

"Q: What does the gold ring around a node mean?"
The node is "locked" by the curator. Its strength is frozen and agents must
plan around it. Gold is the curator's word and outranks any computed number.
Unlock it with the UNLOCK button on the card.

"Q: What is a ghost suggestion?"
A translucent, pulsing node at strength 0.25. It appears when the map loses
coverage (e.g., after you prune a node). Ghosts are unproven - they must earn
support to survive. Accept one to make it real; reject it to dismiss it.

"Q: Why does a node keep blinking red?"
It is the weakest open idea in the settled map. The map refuses to pretend it
is fine. In the default story this is "4-hour road trip" at strength 0.30 -
it conflicts with "I easily get motion sick" and nobody resolved it.

"Q: Can I break the story by acting out of order?"
No. Every scripted beat checks world state before firing. If you prune the
trek early, the scripted prune step simply skips itself. If you accept the
ghost before the script reaches it, subsequent steps continue from there. The
story bends around you - it never breaks.

"Q: What do the colored discs under nodes mean?"
Day assignment. When the story settles, the Explorer assigns nodes to Day 1,
Day 2, or Day 3. Each day gets a distinct disc color. This is a real itinerary
grown by negotiation, not a pre-baked schedule.

"Q: Why does my SAY input create a gold node?"
Human ideas carry more weight than agent ideas. Gold trail = yours. Strength
starts at 0.70 (vs. 0.50 for agents) and human links carry a 1.5× multiplier.
Humans are not voters - they are the ones who narrow the world.

 Controls and Interaction

"Q: I accidentally locked a node by holding pinch too long. How do I undo it?"
Open the node's card (pinch/click the node) and press UNLOCK. The 1.5-second
long-pinch shortcut is convenient but easy to trigger while gesturing; the card
button is the safer path.

"Q: The rewind slider is hard to grab in VR. Any tips?"
Point your laser at the handle and pinch. Drag left to go back in time, right
to return to live. Release to commit. Alternatively, press LIVE to jump
straight to the present.

"Q: Can I use a mouse and keyboard instead of hand tracking?"
Yes. Left-click selects nodes and presses buttons. Right-click-drag orbits the
camera. Scroll zooms. The SAY bar on the web viewer accepts typed text. Hand
tracking is the intended primary input on Quest but mouse fallback works for
rehearsal and desktop review.

"Q: Do the SAY preset buttons actually use my voice?"
No. They process their preset text as a human-input node directly. Headset-mic
speech-to-text is noted as a post-hackathon upgrade - the Meta Voice SDK
cannot be tested without a physical headset. The preset lines are what the
engine processes.

 Technical

"Q: Is the mind map the same in the web viewer and in Unity?"
Yes. Both run the same `Winjay.Core` engine with the same event journal and
the same arithmetic. The only difference is the renderer (Three.js vs. Unity).
Strength values, edge types, day assignments, and the blinking node are
identical.

"Q: What does "deterministic" mean in practice?"
Given the same sequence of proposals and curator actions, the engine always
produces the exact same state. This means the demo is reproducible: record the
event log once and replay it anywhere. Rewinding is folding a shorter prefix
of that log and recomputing from scratch.

"Q: Can I modify the story or add my own scenario?"
Yes. The story is defined in `DemoMindScenario.cs` (agent intents + guards)
and `DemoMindStory.cs` (curator beats). Both read world state before every
step, so custom scenarios follow the same pattern: define intents, guard them
with state checks, and let the engine validate everything.

"Q: What Unity version and SDK versions are required?"
Unity 6 LTS `6000.0.42f1`, Meta XR SDK 71.0.0, OpenXR 1.13.1, XR Hands 1.5.0,
InputSystem 1.11.2, XR Management 4.5.0. Full setup instructions:
`Docs/UNITY_SETUP.md`.

"Q: Does the app collect any data or send telemetry?"
No. Everything runs locally. No analytics, no cloud calls, no user accounts.

------------

WINJAY XR: models talk to models - through a map. No chat lines. You watch
state, you steer state, and the state answers. That is the whole product.
