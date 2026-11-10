# Kingdom Sentinels (COMP3218 CW1)

**Version 1.0**

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
