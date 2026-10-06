# Vaanam (வானம்)

Stretch your arms and fly. A webcam-controlled flying adventure for children aged
5 to 10, in Tamil and English. Built with Unity 6 for Windows.

The child stands in front of the webcam, raises both arms, and a bird on screen
raises its wings with them. "Fly!" and the world starts moving. Tilting the arms
or leaning steers, raising the arms climbs, folding them dives, a hop jumps a
wall, a crouch ducks under a bar. Two children can fly side by side in the same
camera frame. After 15 minutes the sun sets, the game stops itself, and asks the
child to do one real thing: drink water, draw a kolam, hug amma.

Camera frames never leave the device. No ads, no purchases, no streaks.

## Run it

1. Open the project in Unity 6000.3.8f1 (Unity Hub).
2. Menu **Vaanam > Build Main Scene** (only needed once, or after deleting the scene).
   It also assigns the URP pipeline asset and copies the MediaPipe pose model into
   `Assets/StreamingAssets`.
3. Open `Assets/_Vaanam/Scenes/Vaanam.unity` and press Play.

No webcam? The keyboard always works: **A/D** tilt, **W** raise arms, **S** duck,
**Left Shift** jump, **Space** fold wings. Press **2** for a second keyboard player
(arrows / Right Shift / Enter). **F1** shows the tracking debug overlay.

Parent corner: hold **P** or press **Esc**. Language, session length, voice, music,
camera, stats, new session.

Test keys: **N** skips to level complete, **F2** forces the sunset, **F3** starts a
new session, **F12** saves a screenshot to the persistent data folder.

## Project layout

```
Assets/_Vaanam/
  Scripts/Input/      body tracking: BodyPose, PoseProvider, MediaPipe + keyboard sources,
                      PoseMirrorView (the corner mirror), VoiceCommands
  Scripts/Flight/     FlightController (corridor flight), BirdVisual, FlightCamera
  Scripts/World/      LevelGenerator, LevelDefinition assets, Ring, Star, Obstacle,
                      Scenery, SkyController, VaanamPalette (all materials)
  Scripts/Core/       GameFlow (the state machine), SessionTimer, GameSession, SaveData,
                      Localization (Tamil + English), AudioDirector, ProceduralAudio
  Scripts/UI/         HUD, MessageBoard, SunsetScreen, ParentPanel, UiFactory
  Scripts/Editor/     VaanamSceneBuilder, VaanamBuild, EditorCommandQueue, asset importer
  Resources/UI/Text/  pre-rendered phrase pictures, en/ and ta/   (generated)
  Resources/UI/Icons/ stick-figure icons                            (generated)
  Resources/Voice/en/ English voice lines                           (generated)
  Levels/             three LevelDefinition assets, tune them in the Inspector
Tools/                render-text-sprites.ps1, render-voice-lines.ps1,
                      compile-check.sh, editor-run.ps1
Packaging/            MSIX manifest template and build-msix.ps1 for the Store
```

## Why things are the way they are

- **Tamil is drawn, not typed.** Unity text does not shape Indic scripts, so
  `Tools/render-text-sprites.ps1` renders every phrase in `Localization.cs` with
  Windows' own text engine. Edit a phrase, re-run the script.
- **Voice lines are generated** by `Tools/render-voice-lines.ps1` (Microsoft Heera,
  Indian English). There is no Tamil voice on Windows: record Tamil lines yourself
  and drop them into `Resources/Voice/ta/<key>.wav`. The game falls back to English.
- **All art is procedural** (primitives, generated meshes, generated sounds) so the
  game is complete without any asset files. Replace pieces whenever real art arrives:
  the bird is `BirdVisual`, colours are in the `VaanamPalette` asset.
- **A corridor, not free flight.** The bird always flies forward; a five year old
  cannot plan a turn. Levels are things placed ahead along Z.
- **Nothing punishes.** Obstacles slow the bird for a moment and the voice gives a
  hint. Stars only go up. The session ends on a sunset, not a game over.

## Building for the Microsoft Store

1. **Vaanam > Build Windows Player** writes `Builds/Windows/Vaanam.exe`.
2. Reserve the app name in Partner Center and copy the identity values.
3. `.\Packaging\build-msix.ps1 -IdentityName ... -Publisher ... -PublisherDisplay ...`
   writes `Builds/MSIX/Vaanam_<version>_x64.msix`. Add `-SelfSign` to install locally
   for testing. Upload the unsigned package to Partner Center.

## Developer tools

- `Tools/compile-check.sh` compiles the scripts with Unity's own Roslyn in seconds,
  without opening (or fighting with an open) Unity editor.
- `Tools/editor-run.ps1 build-scene play "wait:10" screenshot stop` drives the OPEN
  editor from a terminal through `Temp/vaanam-commands/`. Commands: `ping`, `refresh`,
  `build-scene`, `build-player`, `play`, `stop`, `screenshot`, `state:<GameState>`,
  `voice:<Go|Jump|Duck|Stop>`.
