# Basketball Rush - Before / After Build Report

Date: 2026-11-20
Unity: 6000.6.9f1
Pipeline: Universal Render Pipeline 17.6.0
Input: Input System 1.20.1

## 1. Summary

The project was upgraded from a basic 60-second drag-to-shoot prototype into a more complete arcade basketball game with better feel, scoring depth, audio feedback, pause/best-score support, and a stronger arena presentation.

During the work, the project's `Assets`, `ProjectSettings`, `Packages`, and `.git` folders were accidentally removed while cleaning up a temporary test copy. The latest scene was recovered from the still-open Unity editor, and the rest of the project was rebuilt. The restored project is at the original path and passes an automated smoke test with 0 errors and 0 warnings.

## 2. Before

### Gameplay
- Drag upward and release to shoot the ball.
- Fixed throw arc and power curve.
- 60-second score attack, one point per basket.
- Ball reset after 1 second on the floor.
- Ball reset immediately after a successful basket.
- No streak, combo, swish detection, countdown, best score, or pause.
- No mid-shot feedback beyond a small "+1" popup.

### Presentation and audio
- Procedural rim, net, particle burst, trail, and shot glow.
- Static net that only rotated slightly when scoring.
- Six generated sound effects: shot release, court bounce, rim hit, backboard hit, score, time-up.
- Ball collision audio used one 2D AudioSource with no pitch variation.
- No music, crowd ambience, haptics, or countdown sounds.
- HUD was fully runtime-generated with legacy UI Text.
- No best-score persistence and no settings.

### Scene and visual quality
- Indoor arena made from simple primitives.
- Court, backboard, ball, rim, and pole used basic URP materials.
- Bloom, vignette, and tonemapping existed through the global volume.
- No crowd, LED boards, banners, jumbotron, court logo, or arena lighting accents.
- No normal maps on court or ball.

### Mobile readiness
- Android/iOS build modules not installed.
- Android package ID was still a Unity template ID.
- All screen orientations were allowed.
- No target-frame-rate or sleep-timeout management.
- No on-device performance overlay.

## 3. After - Phase 1: Gameplay Feel

| Area | Change |
|---|---|
| Floor reset | Court reset delay increased from 1s to 2s. |
| Basket reset | Added `basketResetDelay` (1s) so the ball visibly drops through the net before resetting. |
| Swish | `HoopScoreSystem` now checks whether the ball touched the rim or backboard. Clean baskets are marked as SWISH with dedicated HUD feedback. |
| Impact audio | Ball uses a pool of four 3D AudioSources with randomized pitch and speed-based volume. |
| Haptics | Light/medium/heavy vibration on release, rim/backboard contact, and scoring via Android `VibrationEffect` or iOS `Handheld.Vibrate`, with cooldown and PlayerPrefs toggle. |
| Pause | Pause button in the HUD, pause panel with Resume, P/Esc support, automatic pause when the app loses focus, frozen timer/physics/audio. |
| Input robustness | Drag start/end now uses held-state transitions, so brief or low-FPS presses are less likely to be missed. |
| Physics | Ball uses `ContinuousSpeculative` collision detection in flight. |

## 4. After - Phase 2: Score Attack

| Area | Change |
|---|---|
| Best score | Saved with PlayerPrefs, shown on the start screen and game-over screen, with a NEW BEST state. |
| Countdown | 3-2-1-GO countdown with generated beep tones. The timer and throwing are locked until GO. |
| Streak/combo | Consecutive baskets build a streak. Every third basket adds a bonus point (capped), and the bonus is included in the score popup. Misses reset the streak. |
| Score events | `HoopScoreSystem` exposes `BasketScored` and `ShotMissed` events used by the HUD and arena scoreboard. |
| Feedback | Animated score popup with points, SWISH, and streak text; popup rise/fade/scale; MISS feedback; timer turns red at 10 seconds or less. |
| Scoring data | HUD shows current streak below the score panel. |

## 5. After - Phase 3: Visual Polish

| Area | Change |
|---|---|
| Court | Added a procedural wood normal map, higher smoothness, and subtle metallic response. |
| Ball | Added a procedural pebble normal map, higher smoothness, optional clear coat, and enabled the emission keyword so the existing shot glow is visible. |
| Backboard | Glass-style transparent material, new orange frame, and white target square with slight emission. |
| Rim and net | Glossier rim material with subtle emission, denser net (14 ropes, 6 rows), thicker rope, stronger sway. |
| Lighting | Enabled emissive ceiling panels, two new spotlights (court key and hoop key), warmer ambient intensity, and a rim flash light on scores. |
| Arena | Added tiered stands with generated crowd texture, scrolling LED boards, hanging banners, a world-space jumbotron with live score/time/best, a center-court logo, airborne dust, and a reflection probe. |
| Post-processing | Added color adjustments, warm white balance, subtle film grain and chromatic aberration on desktop, plus the existing Bloom/Vignette/Tonemapping profile. |
| Mobile visuals | Film grain and chromatic aberration are skipped on mobile, reflection probe resolution is reduced, and dust particle counts are lowered. |

## 6. After - Phase 4: Mobile Preparation

| Area | Change |
|---|---|
| Frame rate | Runtime tuning: 60 FPS during gameplay, 30 FPS in menus/pause. |
| Battery/sleep | Screen never sleeps during gameplay; system sleep timeout is restored in menus and pause. |
| Orientation | Landscape-only player settings; build script enforces landscape left/right. |
| App IDs | Android/iPhone/Standalone IDs set to `com.defaultcompany.basketballrush`. |
| Android build settings | Min SDK 26, ARM64, IL2CPP, medium managed stripping. |
| iOS build settings | iPhone + iPad, full screen, iOS 13.0 minimum. |
| Build menu | `Basketball Rush > Builds` for Android APK, Android AAB, and iOS Xcode project. CI methods are also available. |
| Diagnostics | Performance overlay toggled with F1 or a 3-finger tap, showing FPS, worst frame time, resolution, aspect ratio, DPI, memory, GPU, battery level/status, target FPS, and time scale. |

Not completed: on-device Android performance/battery tests, physical iPhone/iPad aspect-ratio tests, touch tests on hardware, and final platform builds. Android and iOS build modules are not installed on this Windows machine; iOS also requires macOS and Xcode.

## 7. Verification

- Restored project imports and compiles in batch mode with 0 errors.
- Automated smoke test results:
  - 0 errors, 0 warnings.
  - Scene, URP pipeline, ball, court, backboard, rim, pole, and net verified.
  - GameHUD and ArenaVisuals initialized.
  - Score and streak increased after simulated baskets.
  - Pause set time scale to 0 and resume restored it.
  - Best score saved and displayed.
  - Jumbotron, post FX volume, key spotlight, reflection probe, stands, LED boards, banners, court logo, and dust objects verified.

## 8. Recovery and Caveats

- The exact latest scene was recovered through Unity's Save As: `Assets/Scenes/SampleScene.unity` (63,660 bytes).
- The rest of the project was rebuilt from the Nov 17 scene backup, shell generators for textures/materials/audio/environment, the recovered basketball image, and the current script sources.
- The old damaged folder is preserved at `D:\Unity Projects\Basketball.broken_20261120`.
- Recovery inputs are preserved at `D:\Unity Projects\BasketballRecoveryInput`.
- The HUD was rebuilt as a clean implementation; its layout may differ from the lost version and should be reviewed in Play mode.
- The original custom HUD font was lost; Bebas Neue (OFL) is now used.
- The original `.git` history was lost. Initialize a new repository and make an external backup before continuing.

## 9. Recommended Next Steps

1. Open the project and play-test the recovered scene and rebuilt HUD.
2. Tune HUD panel scale/positions and arena brightness to taste.
3. Create a new Git repository and push a backup.
4. Install Android Build Support + SDK/NDK/OpenJDK through Unity Hub for 6000.6.9f1.
5. Test APK on a mid-range Android device with the performance overlay.
6. Test landscape aspect ratios on a notched phone and an iPad.
7. Build the iOS Xcode project on macOS and test on device.
8. Configure release keystore/signing before publishing.
