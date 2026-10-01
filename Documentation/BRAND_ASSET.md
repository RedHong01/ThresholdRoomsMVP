# FrontRooms brand mark

`Assets/Brand/FrontRoomsLogo.svg` is the supplied editable source artwork. Unity 6 does not include the SVG importer in this project, so the same mark is also exported to `Assets/Resources/Brand/FrontRoomsLogo.png` at 965×192, 8-bit RGBA with transparency. The title overlay loads that PNG at runtime and creates a UI `Image` sprite; the original SVG remains beside it for future vector import or design handoff.

The black mark is shown on the light title surface so no recolouring or raster redraw changes the supplied logo.
