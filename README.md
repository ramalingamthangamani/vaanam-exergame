# Vaanam (வானம்)

A webcam-controlled flying adventure for children aged 5 to 10. Stand in front of the
camera, stretch your arms, and fly. No controller, no touchscreen, no sitting.

Built in Unity 6 (6000.3.8f1) for Windows and the Microsoft Store.

## Why

Children glued to phones move less, play alone, and lose attention span. Vaanam makes
movement the only way to play, adds two-player co-op in one camera frame, and ends
every session on purpose after 15 to 20 minutes.

## Project layout

```
Assets/_Vaanam/
  Scenes/            Game scenes (create Prototype.unity first)
  Scripts/
    Input/           BodyPose, IPoseSource, PoseProvider, KeyboardPoseSource,
                     MediaPipePoseSource (your task), OneEuroFilter
    Flight/          FlightController
    Core/            SessionTimer
    UI/
  Prefabs/  Art/  Audio/Voice/{ta,en}  Fonts/  Settings/
Packages/
  com.github.homuler.mediapipe-0.16.3.tgz   MediaPipe Unity plugin (local)
```

Everything game-specific lives under `_Vaanam` so imported assets never mix with yours.

## How input flows

```
Webcam -> MediaPipePoseSource ─┐
                               ├─> PoseProvider (filter + calibrate) -> FlightInput -> FlightController
Keyboard -> KeyboardPoseSource ─┘
```

Game code only ever reads `PoseProvider.Instance.Current`. Swapping or improving the
tracker never touches gameplay.

## First run

1. Open the project in Unity Hub (add `E:\Vaanam`, editor 6000.3.8f1).
2. Let packages import. If asked to enable the new Input System and restart, say yes.
3. Create `Assets/_Vaanam/Scenes/Prototype.unity`:
   - Empty GameObject `PoseProvider` with components `PoseProvider` + `KeyboardPoseSource`.
   - A Capsule named `Player` with `FlightController`. Drag a child cube into
     `visualModel`. Rigidbody is added automatically.
   - A big Plane as ground, some cubes as obstacles, Main Camera as a child of
     Player placed behind and above it.
4. Press Play. A/D tilt, W flap, hold Space for wings open. Watch the debug overlay.

## Roadmap

- [ ] Week 1: Prototype scene flies well on keyboard. Tune FlightController values.
- [ ] Week 2: MediaPipePoseSource working. Test with a real child for 10 minutes.
- [ ] Week 3: Calibration screen (stand here, stretch arms, done). Rings to fly through.
- [ ] Week 4: One full level with art, music, and the sunset session end.
- [ ] Week 5: Two players in one frame (MediaPipe num_poses = 2).
- [ ] Week 6: Tamil + English UI, voice lines, parent dashboard.
- [ ] Week 7: Windows build, MSIX packaging, Microsoft Store submission.

## Rules we do not break

- Camera frames never leave the device. Say so on the store page.
- No ads, no in-app purchases, no streaks that punish a child for stopping.
- Big movements, forgiving detection. A child flapping badly must still fly.
- Keyboard fallback always works.
