
Clayxel v0.51 beta
Created by Andrea Interguglielmi.
Twitter: https://twitter.com/andreintg
Downloaded from: https://andrea-intg.itch.io/clayxels 
Please consider donating to support this software!

When used for any interactive application or game, 
please consider crediting the software or its creator, thank you : )

Usage:
1) Add an empty gameObject in scene, then add the Clayxel component to it.
2) Use the custom inspector to perform basic operations like adding solids.
3) After you add a solid, use the solid's custom inspector to change primitive type, blending and color.
4) Mess around, it's easy : )

Tips:
- Primitives can have negative blending to subtract other primitives.
- Solids are evaluated from the last one to the first in the hierarchy. 
- Drag solids up and down the hierarchy to change the final result, 
this is especially handy to isolate subtractive blends.
- Use many clayxel containers to make large and complex models, it's faster than increasing the size of a single container.

For requests and bug reports: https://andrea-intg.itch.io/clayxels/community

Change log:
v0.51
- improved picking to be more responsive
- added new emissiveIntensity parameter
- new user-defined file for custom primitives: userClay.compute
- urp and hdrp frozen mesh shader
- all negative blended shapes are now visualized with wires
- performance optmizations to on grids with large sizes
- custom materials override
- added menu entry under GameObject/3D Objects/Clayxel Container
- new solids added are now centered within the grid
- added Clayxels namespace
- Clayxel component is now Clayxels.ClayContainer
- ClayObjects are auto-renamed unless the name is changed by the user
- misc bug fixes and optimizations

v0.5
- initial HDRP and URP compatibility
- use #define CLAYXELS_INDIRECTDRAW in Clayxels.cs to unlock better performance on modern hardware 
- restructured mouse picking to be more robust and work on all render pipelines
- core optimizations to allow for thousands of solids
- misc bug fixes
v0.462
- fixed? occasional tiny holes in frozen mesh
v0.461
- fixed disappearing cells when next to grid boundaries
v0.46
- added shift select functionality when picking clay-objects
- improved, clayxels texture now use full alpha values (not a cutoff)
- improved ugly sharp corners on negative shapes
- improved, grids now grow/shrink from the center to facilitate resolution changes
- fixed solids having bad parameters upon solid-type change in the inspector (it now reverts to good default values)
- fixed disappearing clayxels when unity looses and regain focus (alt-tabbing to other apps)
- fixed: glitchy points on seams when solids go beyond bounds
- fixed: scaling grids caused clayxels to change size erroneously
- fixed: inspector undo should work as expected now
- fixed: building executables containing clayxels caused errors
- fixed freeze to mesh on bigger grids caused some solids to disappear
v0.45
- mac support
- clayxels can now be textured and oriented to make foliage and such
v0.43
- new surface shader, integrates with scene lights with shadows and Unity's PostProcess Stack
- selection highlight when hitting "p" shortcut
- inspector multi-edit for all selected clayObjects
v0.42
picking bug fixed
v0.41
new shader for mobile and Mac OSX
v0.4
first beta released
