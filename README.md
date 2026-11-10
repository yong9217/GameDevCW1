# Kingdom Sentinels (COMP3218 CW1)

**Version 2.0**

A tower-defence game made in Unity. This repository is the **complete Unity project**
(`Assets`, `Packages`, `ProjectSettings`), so you can open it and play straight away.
(这是完整的 Unity 工程，直接用 Unity 打开即可运行。)

## How to run it

1. Install **Unity 6000.6.4f1** (Unity 6.6) with Unity Hub. Other versions may import with errors.
2. **Clone this repository into a NEW, empty folder.** Do *not* copy it into an older Unity project
   or its `Assets` folder (that causes duplicate-script compile errors).
3. Unity Hub -> **Add** -> *Add project from disk* -> choose the cloned folder -> open it.
   The first open takes a few minutes while Unity imports everything and downloads the packages.
4. Press **Play**. The editor always starts from the main menu (`Assets/Scenes/MainMenu.unity`),
   like the built game. Click **Start** to begin.

## Controls

| Action | Input |
| --- | --- |
| Pick a tower | Left-click a tower card (bottom bar) |
| Build the picked tower | Left-click the ground |
| Cancel the picked tower | Right-click |
| Move the camera | W / A / S / D |
| Pause | Esc |

Nothing is selected at the start: pick a card first, otherwise clicks on the map build nothing.

## What's new in 2.0

- **Music and sound effects.** Calm music while the tutorial teaches, a dedicated boss intro timed to the
  boss's entrance, victory / defeat themes, and sounds for building, every tower's attack, hits, kills,
  gold and the interface. Tower attack sounds are kept quieter so they never cover the music.
- **Boss feel.** A heavy ground-shaking blow with camera shake and a hit effect, only when the blow really lands.
- **New scale.** The castle is 1.5x bigger; towers and enemies are 0.8x, with their attack ranges scaled to match.
  The camera starts further out, so more of the battlefield is visible.
- **Balance.** Castle 500 HP, boss 2500 HP, 10 starting gold; kill rewards: Normal 1, Fast 1, Heavy 2, Boss 5.
- **Glossary.** Every portrait now frames its model automatically and shows it flat, without light or shadow.
- Clean-up: development test scripts and temporary diagnostics removed.

## Audio credits

- Sound effects: Kenney "Impact Sounds" and "RPG Audio" (CC0) - https://kenney.nl
- Defeat theme: RandomMind, "Medieval: Defeat Theme" (CC0) - https://opengameart.org/content/medieval-defeat-theme
- Background music: RandomMind, "Medieval: The Bard's Tale" (CC0) - https://opengameart.org/node/80558
- Arrow tower, magic tower and boss blow sounds: made for this game.
