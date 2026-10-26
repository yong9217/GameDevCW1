# Base Defense Art Handoff

Unity version used for validation: 6000.6.4f1. Rendering: Built-in Render Pipeline.

## Use these eight prefabs

Open `Assets/BaseDefenseArt/Prefabs`. This is the only prefab folder in this handoff.

| Prefab | Contents |
| --- | --- |
| Tower_Arrow | Crossbow tower with the approved stone pedestal and simplified timber material |
| Tower_Cannon | Cannon tower with the approved stone pedestal and painted details |
| Tower_Magic | Crystal tower with the approved stone pedestal; upper crystal assembly enlarged 1.5 times |
| Enemy_Normal | Normal enemy |
| Enemy_Fast | Fast enemy |
| Enemy_Heavy | Heavy enemy |
| Enemy_Boss | Hammer boss with gold devil-face emblems on both red banners |
| Base | Approved stone base with guardian banner |

Nested prefab instances have been unpacked and saved into the eight entry prefabs. There are no additional internal prefab files to choose from.

## Included animation and attack effects

Each enemy includes a skinned mesh, bones, material, texture, Animator Controller, and four animation states: `Idle`, `Walk`, `Attack`, and `Death`.
Use `animator.Play("Walk")`, `animator.Play("Attack")`, or `animator.Play("Death")` to play the corresponding state. Final gameplay code should control state changes.

- Arrow tower: arrow flight, muzzle and impact sparks, recoil.
- Cannon tower: cannonball flight, muzzle and impact bursts, recoil.
- Magic tower: rotating crystal, lightning chains, impact sparks.

Existing attack demonstration scripts are retained: `TowerPreviewWeapon`, `TowerPreviewEffects`, `TowerPreviewTarget`, and `TowerPreviewCrystalSpin`. Some effects are generated at runtime rather than stored as separate effect prefabs.
The demonstration weapon logic selects a `TowerPreviewTarget` with a matching `lane` value. The gameplay programmer can reuse the visual effects while connecting the final targeting, movement, health, and damage systems.

## What the other files do

- `Dependencies/Meshes`: tower, pedestal, base, enemy and banner geometry.
- `Dependencies/Materials`: surface colours, textures and shader settings used by the prefabs.
- `Dependencies/Textures`: image data referenced by the materials.
- `Dependencies/Animations`: 16 enemy animation clips and four Animator Controllers.
- `Dependencies/Shaders`: rendering code used by the materials.
- `Dependencies/Scripts`: the four existing tower attack/effect scripts.
- `.meta` files: Unity asset IDs and import settings. Prefabs reference assets using these IDs, not just filenames. Keep and commit every matching `.meta` file; deleting them can break references or reset import settings. Unity normally hides these companion files in its Project window.

The dependencies are required by the selected prefabs or their existing effect scripts. Old preview scenes, editor tools, alternative models, upgrade towers, test files, Library and Temp folders are excluded. Only one README is included.

## Add to the shared repository

Copy `Assets/BaseDefenseArt` into your Unity project's `Assets` directory and commit the entire folder, including all `.meta` files. Keep its `Dependencies` folder.
Alternatively import `BaseDefense_Handoff.unitypackage`; use one import method.

If an earlier version was already imported, let Unity preserve the existing GUIDs. Avoid keeping a second copy of the same assets in the same project.

## Validation

The handoff is checked in an independent Unity project for prefab loading, mesh/material references, missing scripts, all 16 enemy animation states, and runtime firing/hits from all three towers.
