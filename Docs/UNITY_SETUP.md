 WINJAY XR - Unity Setup & Running in Simulator / Quest

 Installation status (automated by assistant, 2026-10-06)

- [x] Unity Hub 3.22 installed (via winget)
- [x] Unity 6 LTS `6000.0.42f1` installed to `D:\Unity\6000.0.42f1`.
      Android SDK/NDK/JDK modules were installed manually on 2026-10-07
      (see "Build a Quest APK" below) - they are only needed for Quest APK
      builds, not for editor play with the Meta XR Simulator.
- [x] Complete Unity project: `Unity\WinjayXR\` (scripts + packages + Core sources)
- [x] All scripts verified with the "offline compile check"
      (`Tools/unity-compile-check/check.sh`), which runs two passes: editor
      shape (all 48 scripts, editor defines on) and build shape (46 runtime
      scripts, no editor defines - the shape that ships), compiled against the
      real Unity 6 reference assemblies and the mirrored package APIs -
      0 errors, 0 warnings.
- [x] "Important honesty note (updated 2026-10-07):" Unity 6 batchmode
      launched, resolved its license, and imported the project. A scripted
      Quest APK build was attempted on 2026-10-06 and failed with
      `UnityException: Android SDK not found`; on 2026-10-07 every missing
      module (platform-tools, build-tools 34, platforms 33/34/35, NDK r27c,
      OpenJDK 17, cmdline-tools, CMake) was downloaded from the official Unity
      release manifest, extracted into
      `Editor/Data/PlaybackEngines/AndroidPlayer` and verified file by file -
      but a Quest APK has not been built yet. A Windows Standalone Play
      session renders the scripted mind map (desktop orbit camera); the
      activated Meta XR Simulator has not yet produced a live OpenXR session
      in the editor, so hand tracking and pinch interaction remain
      unverified, and no headset test was performed. The offline compile
      check proves the scripts compile against the real Unity reference
      assemblies, not that an Android build or XR runtime succeeds.

 Remaining setup and device checks

1. "Unity account/license, only if requested" - the batch build on
   2026-10-06 resolved its licensing entitlement, so licensing did not block
   that attempt. If Unity Hub or the editor asks for activation, sign in and
   complete the requested license step.
2. "Locate editor, if Hub has not registered it" - Unity Hub → Installs → Locate →
   select `D:\Unity\6000.0.42f1\Editor\Unity.exe` (if Hub hasn't found it).
3. "Open the project" - Hub → Add → select the folder
   `D:\zzzzzzzzzzz AntiGravity\_-_-_META VR_-_-_\WINJAY-XR\Unity\WinjayXR`
   → Open. On first open Hub may offer another version - pick `6000.0.42f1`.
   Wait for package import (Meta XR SDK etc., ±10 minutes the first time).
4. "Check the Console" - it should be clean (no red errors). If anything
   looks off, run the offline check first:
   `bash Tools/unity-compile-check/check.sh` (needs only the .NET SDK).
5. "Build a Quest APK" - the Android modules are installed (SDK, NDK r27c,
   OpenJDK 17, CMake 3.22.1; verified file by file on 2026-10-07), so no Hub
   setup is needed anymore. Use "File > Build Settings" → Android platform
   → "Build" (or "Build and Run" with the headset connected). A scripted
   alternative exists: `WinjayXR.EditorTools.WinjayBuildScript.BuildQuest`
   via `Unity.exe -batchmode -quit -projectPath <project> -executeMethod ...`
   (needs an activated license). If the editor reports missing tools, check
   "Edit > Preferences > External Tools" and point the paths at
   `D:\Unity\6000.0.42f1\Editor\Data\PlaybackEngines\AndroidPlayer\{SDK,NDK,OpenJDK}`.

 Automated setup (once, 2 clicks)

Menu bar: "WINJAY → Full Setup (loaders + scene)"

This creates the scene `Assets/Scenes/Main.unity`, enables XR Plug-in
Management with OpenXR only, the "Meta XR Feature" (hand tracking +
passthrough via OpenXR - in SDK 71 the SDK also enables it automatically when
the project opens), Android player settings (arm64, IL2CPP, minSdk 32), plus
the app name "WINJAY XR" + icon (`Assets/Textures/WinjayIcon.png`).

> If any step fails (see Console), the manual fallbacks:
> - `Project Settings > XR Plug-in Management` → check "OpenXR" for
>   PC Standalone and Android. Do not install or enable the separate Oculus
>   XR Plug-in alongside OpenXR; Meta recommends OpenXR.
> - OpenXR tab → Android → check "Meta XR Feature".
> - `Meta > Meta XR Simulator > Activate` for the Simulator. Meta XR
>   Simulator 71 registers this menu path in the installed package.

 Run the demo - the Living Mind Map (default mode)

The project opens "directly in mind-map mode" - the hero demo. The world
builds itself: agents grow the "3-Day Vacation" map in front of you.

1. Enable the Simulator: menu `Meta > Meta XR Simulator > Activate`.
   The menu changes to "Deactivate" when the Simulator is active. The
   installed package is Meta XR Simulator 71.0.0.
2. Open the scene `Assets/Scenes/Main.unity` (File > Open Scene).
3. For editor simulation, select "File > Build Profiles > Windows >
   Switch Platform". Android is only needed for Quest APK builds.
4. Press "Play". In the plain editor without a Simulator, control the
   camera with right-click-drag + scroll - the custom orbit camera only
   yields when a real XR display is running.
5. The story paces itself. Intended interactions:
   - "Pinch" a node → its card opens (label, creator, strength, phase,
     lock/ghost status + curator buttons).
   - "Card buttons": PRUNE (red), LOCK (GOLD) / UNLOCK, CLOSE; on a ghost:
     ACCEPT / REJECT.
   - "Pinch-hold 1.5 s" on a node also locks it (shortcut - easy to
     trigger by accident while gesturing; prefer the card button).
   - "Panel": RUN/STEP/RESET, the three SAY presets (voice-text nodes;
     real mic is the live path), the REWIND scrubber (grab the handle) and
     "LIVE" to jump back, "MACHINE" to switch to the original
     evidence→hypothesis→decision scenario.
6. The full 10-minute beat sheet: `Docs/MINDMAP_DEMO_SCRIPT.md`.

"Local validation status (updated 2026-10-07):" the map renders in Windows
Standalone Play. Without a live XR session the Meta XR components stay off by
design (exactly one informational log line: "desktop preview mode"), and the
`Unable to set TrackingOrigin` error spam seen in earlier Play sessions is
fixed - XR components are only created when a display is actually running.
The Simulator has not yet produced a live OpenXR session in the editor
(`xrCreateSession: XR_ERROR_RUNTIME_FAILURE`), so hand tracking and pinch
input remain unverified; treat editor runs as a mouse-driven render check
until a headset or a working Simulator session is available.

 B. On a real Meta Quest

1. Enable Developer Mode on your Meta account (Meta Quest app /
   developer.oculus.com).
2. Connect the headset via USB-C.
3. "File > Build Settings" → Android platform → "Switch Platform" →
   "Build and Run". The APK lands at `Builds/WinjayXR.apk` - it can also
   be sideloaded via `adb install Builds/WinjayXR.apk` or SideQuest.
   (For a scripted build there is a batchmode entry point:
   `Unity.exe -batchmode -quit -projectPath <project> -executeMethod
   WinjayXR.EditorTools.WinjayBuildScript.BuildQuest` - needs an activated
   license; the GUI path above is the normal one.)
4. In the headset: passthrough on (the Winjay world floats in your room),
   hand tracking active - pinch to interact. Voice: press a SAY preset on
   the panel (the preset line is what the engine processes; headset-mic
   speech-to-text is the post-hackathon upgrade).

 Unity project structure

```
Unity/WinjayXR/
├── Packages/manifest.json           Meta XR SDK Core 71 + Simulator 71 + OpenXR
└── Assets/
    ├── link.xml                       preserve Assembly-CSharp (IL2CPP reflection)
    ├── Textures/WinjayIcon.png        app icon (applied automatically by Setup)
    ├── Winjay.Core/                 truth layer source copy (not a DLL)
    ├── Winjay.Agents/               mock agent copy (incl. mind agents)
    └── Scripts/
        ├── WinjayBootstrap.cs         any scene becomes the Winjay world (mind mode default)
        ├── WinjaySceneController.cs   machine scenario: engine + runner
        ├── WinjayWorldView.cs         machine scenario spatial renderer
        ├── WinjayInteraction.cs       machine scenario interaction
        ├── WinjayMindController.cs    mind map: local engine + DemoMindStory driver
        ├── WinjayMindView.cs          mind map spatial renderer (nodes, edges, trails, days)
        ├── WinjayMindInteraction.cs   mind map panel/card/hand interaction + rewind
        ├── XrEnvironmentSetup.cs      passthrough MR + hand tracking
        ├── WinjayPalette.cs           colors/positions identical to the web viewer
        ├── WinjayPrimitives.cs        procedural geometry + XR display poll + LabelAutoFit
        ├── Billboard.cs / EditorCameraOrbit.cs
        └── Editor/
            ├── WinjayProjectSetup.cs  menu WINJAY → Full Setup
            ├── WinjayBuildScript.cs   APK build from the command line
            └── TextFitProbe.cs        one-shot diagnostic: measures TextMesh label fit
```

 Syncing Core sources

`Winjay.Core` and `Winjay.Agents` are copied into `Assets/` (Unity compiles
the source directly, so judges can read the code). If you change the sources
under `WinjayCore/` or `MockAgent/`, run:

```
Tools\sync-unity-sources.bat
```

 Technical notes

- "No chat": the user only sees state (colors, tension, strength). In the
  mind map the human is the curator - prune, lock, speak - and the agents
  react to those interventions like any other world event.
- "Deterministic": the same proposal order always produces the same state
  and visuals - the foundation for demo replay/recording. Strength is
  arithmetic (support +0.15, conflict −0.20, bridge +0.05, human ×1.5,
  ghost 0.25), recomputed from live state every time.
- "One engine, three renderers": console host, web viewer (Three.js) and
  Unity XR all fold the same event model. Unity runs the engine locally -
  no server needed in the headset.
- "Labels": `LabelAutoFit` measures the rendered mesh and refits text
  runtime-side, so panel/card text stays readable at any distance without
  hard-coded font-size tuning.
- "XR detection": scripts poll `XRDisplaySubsystem.running` (Unity 6 makes
  `XRDevice.isPresent` a hard compile error); the editor camera yields only
  to a running headset.
- "Mock first, LLM later": `IWinjayAgent` and the mind agent interfaces do
  not change when agents are replaced with real LLMs (API keys never enter
  Unity).
- Pinned versions: Meta XR SDK 71.0.0, OpenXR 1.13.1, XR Hands 1.5.0,
  InputSystem 1.11.2, XR Management 4.5.0.
