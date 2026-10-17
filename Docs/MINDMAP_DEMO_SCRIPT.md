 WINJAY XR - The Living Mind Map: Demo Script (10 minutes)

This is the beat sheet for the "hero demo": a living mind map where agents
grow, connect and challenge ideas, and "you" - the curator - prune, lock
and speak into the map. Everything you see is recomputed from world state,
never baked.

- Web version: `http://localhost:8765/mind.html` (mouse demo)
- Unity version: the XR project opens "directly in mind-map mode"
  (see `Docs/UNITY_SETUP.md`) - pinch, point, speak. This is the hero shot.

For the "submission video (< 3 min)", use the 3-minute cut at the bottom.

------------

 Setup

"Web (fastest to rehearse):"

```bash
cd WebVisualizer/Winjay.Gateway
dotnet run -- --port 8765
 open http://localhost:8765/mind.html
```

"Unity:" open the project, press Play - the mind map builds itself.
The control panel floats in front of you.

"Controls at a glance"

| Action | Web | Unity (mouse fallback) | Unity (headset) |
|------------|------------|------------|------------|
| Run / pause | RUN | RUN | pinch RUN |
| One action | STEP | STEP | pinch STEP |
| Fresh world | RESET | RESET | pinch RESET |
| Inspect a node | click it | left-click it | pinch it |
| Prune / lock / accept | buttons in the inspector | buttons on the card | pinch the card's buttons |
| Lock (shortcut) | - | - | keep pinching a node 1.5 s (careful: it is easy to trigger) |
| Speak an idea | type in the SAY bar + SPEAK | - | SAY preset buttons |
| Rewind | drag the rewind slider | drag the slider handle | grab the handle |
| Jump to live | LIVE | LIVE | pinch LIVE |
| Original machine scenario | - | MACHINE | pinch MACHINE |

Pace the run with the speed slider (web: 80–1500 ms) or use STEP to hold the
map still while you talk.

------------

 The 10-minute flow

 0:00–0:40 - Orientation

"This is not a chat. Nobody types. Agents talk to each other through a map
you can stand inside - and I am the curator of this map."

Point at the root: ""3-Day Vacation"" (cyan = the Explorer grew it). The map
is not a conversation transcript; it is a negotiation you can see. Every
color, every line, every strength number is computed from state - the same
arithmetic runs in the web viewer and in this headset.

 0:40–1:40 - The Explorer grows the map

The Explorer (cyan, 4FC3F7) unfolds the five branches - "Transport",
"Lodging", "Activities", "Food", "Budget" - then fills them with
ideas: Late arrival Day 1, 4-hour road trip, Sunrise trekking 4 AM,
Relaxed beach day, Night market walk, Scenery stops, Street food
stall, Homestay in the village, City hotel.

"The Explorer proposes. It never decides. Every idea is born at strength
0.50 - nothing here is trusted yet."

 1:40–2:40 - The Connector finds what the Explorer cannot see

Green "support" lines appear: Street food stall → Budget ("local food
keeps the budget alive"), Sunrise trekking → Scenery stops. Then the first
wow moment - a "teal curved arc": Homestay in the village ↔ Night market
walk.

"The Connector doesn't grow the map; it connects it. Village evenings spill
into the night market - nobody planned that. That bridge is worth +0.05
strength to both ideas, and the arc is curved on purpose: bridges are
unexpected."

 2:40–3:30 - The Challenger marks the tensions

Red edges snap in, vibrating: Late arrival Day 1 → Sunrise trekking ("a 4 AM
start collides with the late arrival") and Budget → Sunrise trekking ("trek
fees exceed the budget limit"). The trek's strength drops 0.50 → 0.30
(−0.20 per conflict).

"The Challenger doesn't delete anything. It marks collisions and the numbers
crack. Watch the trek's ring: red and shrinking. The map is arguing with
itself - visibly."

 3:30–4:40 - Curator intervention 1: prune the trek

Prune ""Sunrise trekking 4 AM"" (select it → PRUNE). It shrinks to nothing
and its edges die with it.

"I am the curator. I cut it. Now watch - the agents are not asleep."

A "ghost" appears: the Connector floats "Sunset at the cliff", translucent
and pulsing, attached to Scenery stops - strength 0.25, below everything.

"When I killed the trek, the Explorer's scenery idea lost its support. The
Connector reacted: a ghost suggestion - 'Sunset at the cliff' keeps the
scenery idea alive. It is unproven. It sits at 0.25 and it must earn its
way in."

 4:40–5:40 - Curator intervention 2: accept the ghost, lock the beach

Accept the ghost (its ring loses the ghost shimmer - a real node now, still
weak at 0.25; the Connector immediately supports it: Sunset → Scenery
stops, +0.15). Then select ""Relaxed beach day"" and "LOCK (GOLD)".
A gold ring clamps around it.

"Accepted. And this one -" (lock the beach) "- is now gold. Locked means
frozen: I have decided. The agents must now plan around it. Gold is the
curator's word, and it outranks any number."

 5:40–6:50 - Curator intervention 3: say what matters to you

Say ""I easily get motion sick"" (Unity: pinch the SAY preset; web: type it
in the SAY bar). A gold node with "your" trail appears near Transport -
human ideas start at 0.70, and human links carry 1.5× weight.

"My idea. Gold trail, mine. Humans are not voters here - we are the ones
who narrow the world."

The map answers, in order, without anyone clicking anything:

1. The "Challenger" shoots a red edge: I easily get motion sick → 4-hour
   road trip - "the long winding road versus motion sickness".
2. The "Explorer" reacts to the collision by growing an alternative:
   ""Night train"" under Transport.

"I said one sentence. The Challenger attacked the road trip - and the
Explorer found the night train. I didn't plan that. The map did."

 6:50–8:00 - The map settles: three days, one open question

The Explorer quietly assigns "Day 1": Late arrival Day 1 + Night
train; "Day 2": Relaxed beach day (gold) + Sunset at the cliff;
"Day 3": Street food stall + Homestay in the village. Colored discs
glow under each day's nodes.

One idea keeps "blinking red" - "4-hour road trip", the weakest open idea
at 0.30. The map refuses to pretend it is fine.

"Day 1, Day 2, Day 3 - a real itinerary, grown by negotiation. And this
one" (point at the blinking road trip) "stays unresolved. The map honestly
shows what it has not decided."

 8:00–9:10 - Rewind: the whole story is state

Drag the rewind slider back. Nodes vanish, edges fold away, colors return to
who created them - cyan Explorer, green Connector, orange Challenger, gold
you. Stop anywhere and walk through it. Then LIVE.

"The entire history is one event log. I can scrub time - for a judge, the
audit trail of every idea: who made it, who attacked it, who killed it, and
what I, the curator, decreed."

 9:10–10:00 - Close

"WINJAY XR: models talk to models - through a map. No chat lines. You watch
state, you steer state, and the state answers. That is the whole product."

Optional: pinch "MACHINE" to jump into the original non-mindmap scenario
(the evidence → hypothesis → falsify → decision state machine) to show this
is one engine with two faces.

------------

 The 3-minute video cut (submission)

| Time | Beat |
|------------|------------|
| 0:00–0:20 | Orientation: "not a chat - a map you stand inside" |
| 0:20–0:50 | Explorer grows the map (+ the strength 0.50 line) |
| 0:50–1:20 | Connector's teal bridge arc (wow 1) + Challenger's red tensions |
| 1:20–1:50 | Curator prunes the trek → ghost appears → accept |
| 1:50–2:20 | Lock the beach gold; say "I easily get motion sick" → collision → Night train (wow 2) |
| 2:20–2:45 | Days settle + the one blinking node; rewind scrub (wow 3) |
| 2:45–3:00 | Close on the tagline |

------------

 Scripted vs live - the honesty note

The agents are "deterministic mock agents", and this is by design (judged
25% "technical": a deterministic, replayable truth layer; LLMs can be swapped
in later without changing the protocol).

- Each agent has a "scripted intent list". Before acting, every step checks
  "world state" - `exists`, `alive`, `pruned`, `locked`, `has link` - and
  only fires when the state actually allows it.
- Because the guards read real state, "your live actions always win": prune
  the trek early, and the scripted prune step is simply skipped; accept the
  ghost and the next steps continue from there. The story bends around you.
- Every value you see (strength, ripples, day assignment, the blinking node)
  is "recomputed" from the current map by the engine - nothing is animated
  fake. Support +0.15, conflict −0.20, bridge +0.05, human ×1.5, ghost 0.25.

Say this out loud in the demo - it is a strength, not a caveat.

 Voice lines (record these exactly)

| Line | Fires |
|------------|------------|
| "I easily get motion sick" | the scripted showcase node (gold, yours) |
| "Slow mornings, please" | a second human idea (good candidate for Q&A improvisation) |
| "Rent a scooter" | a third human idea - try it to show collisions can hit anything |

 Reference - the exact story data

Nodes (id · label · creator): VAC 3-Day Vacation (root) · TRN Transport ·
STAY Lodging · ACT Activities · FOOD Food · BUD Budget · ARR Late
arrival Day 1 · RIDE 4-hour road trip · TREK Sunrise trekking 4 AM ·
BEACH Relaxed beach day · NIGHT Night market walk · SCN Scenery stops ·
STREET Street food stall · HOMESTAY Homestay in the village · HOTEL
City hotel - all Explorer; SUNSET Sunset at the cliff - Connector (ghost
first); MOTION I easily get motion sick - Human.

Scripted beats and their guards (from `DemoMindScenario.cs`):

|    | 	Beat |	 Fires when |
|---|------------   |	     ------------|
| 1 | Challenger: ARR→TREK, BUD→TREK conflicts | both ends alive |
| 2 | Curator prunes TREK | both conflicts exist |
| 3 | Connector floats ghost SUNSET | TREK pruned |
| 4 | Curator accepts SUNSET (or REJECT it - the story re-routes) | SUNSET exists, still a ghost |
| 5 | Connector supports SUNSET→SCN | SUNSET accepted |
| 6 | Curator locks BEACH | TREK pruned |
| 7 | Curator says MOTION | BEACH locked |
| 8 | Challenger: MOTION→RIDE conflict | both alive |
| 9 | Explorer grows TRAIN Night train | the MOTION→RIDE conflict exists |
| 10 | Phases: ARR+TRAIN→Day 1, BEACH+SUNSET→Day 2, STREET+HOMESTAY→Day 3 | TRAIN exists, TREK pruned, BEACH locked, SUNSET accepted |

The story settles when the agents have nothing left to do "and" no curator
step is pending. Max 600 ticks; the web viewer warns if the tick limit is
hit.

 Recording checklist

- [ ] Both scenarios rehearsed on the web viewer first (RESET between runs)
- [ ] Headset: passthrough on, hands tracked, panel readable at 1.5 m
- [ ] Disable notifications; battery > 50%; FPS stable (no heavy models -
      it is all procedural geometry)
- [ ] Say your lines clearly - "I easily get motion sick" is the trigger
- [ ] After the take: verify the final map shows Day 1/2/3 + one blinking node
