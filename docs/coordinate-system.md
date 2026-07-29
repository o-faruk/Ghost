# Coordinate system

Coordinate bugs look exactly like resolver/model failures and will waste days if this contract
is violated anywhere. Read this before touching `Ghost.Core.Models.Geometry` or anything in
`Ghost.App/Interop`.

## The rules

1. **`Ghost.App/app.manifest` declares per-monitor DPI awareness v2.**

   ```xml
   <application xmlns="urn:schemas-microsoft-com:asm.v3">
     <windowsSettings>
       <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
       <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
     </windowsSettings>
   </application>
   ```

   Without this, UIA returns coordinates in one space and Windows lies to WPF about another, and
   the ghost lands offset by tens of pixels on any scaled display.

2. **UIA's `BoundingRectangle` returns physical pixels in virtual-desktop coordinates.** This is
   Ghost's canonical space. Every type in `Ghost.Core` — `PhysicalPoint`, `PhysicalRect`,
   `UiElement.Bounds`, `ScreenSnapshot.WindowBounds` — is in this space. `Ghost.Core` never
   converts to any other space; it has no DPI awareness at all and no dependency on WPF.

3. **Virtual-desktop coordinates can be negative.** A monitor positioned to the left of or above
   the primary display has negative `Left` / `Top`. Never assume `>= 0`. Never clamp to zero.
   Compute the virtual desktop bounds from `SystemParameters.VirtualScreenLeft/Top/Width/Height`
   (in `Ghost.App`) or `GetSystemMetrics(SM_XVIRTUALSCREEN, ...)` (in `Ghost.Core`, if ever
   needed there) — never hardcode `(0, 0)` as the origin.

4. **WPF renders in device-independent pixels (DIPs).** Conversion from physical pixels to DIPs
   happens in exactly one place in the whole codebase: `Ghost.App/Interop/DpiHelper.cs`. The
   overlay window itself is positioned in physical pixels via `SetWindowPos`; only its internal
   `Canvas` children are placed in DIPs, computed by `DpiHelper`.

5. **DPI can differ per monitor and can change at runtime.** The overlay window handles
   `WM_DPICHANGED`. When it fires, `DpiHelper` recomputes the DIP scale for that window and every
   child element is re-placed — nothing caches a stale scale factor across the message.

6. **Ghost never writes screen coordinates that mix the two spaces.** A `PhysicalRect` is never
   compared to, added to, or stored alongside a DIP value. If a bug looks like "off by a
   suspiciously round factor" (1.25, 1.5, 1.75, 2.0), it is almost certainly a physical/DIP mixup,
   not a resolver bug.

## Worked example: two monitors, secondary at 150% scaling, positioned left of primary

Setup:

- **Primary monitor**: 1920×1080 physical pixels, 100% scaling (96 DPI). Its top-left is the
  origin of virtual-desktop space: `(0, 0)`.
- **Secondary monitor**: 1920×1080 physical pixels, 150% scaling (144 DPI), positioned
  immediately to the left of the primary, vertically centered against it (it's shorter in DIP
  terms once scaled, so Windows offsets it vertically to align visually).

Windows lays out the virtual desktop by DIP-equivalent size, not raw physical pixels, when
arranging monitors. At 150% scaling, the secondary monitor's *effective* width in the shared
layout space is `1920 / 1.5 = 1280` DIPs, vs. the primary's `1920 / 1.0 = 1920` DIPs. Windows
still reports monitor bounds in physical pixels for each monitor's native resolution, but the
secondary's origin is placed so that `secondary.Left + secondary.Width(scaled-equivalent)` touches
`primary.Left = 0`.

Concretely, `GetMonitorInfo` might report:

| Monitor | Physical bounds (virtual-desktop space) | Scaling |
|---|---|---|
| Primary | `(0, 0, 1920, 1080)` | 100% |
| Secondary | `(-1920, -180, 1920, 1080)` | 150% |

(The secondary's `Top` is negative here because it's taller in physical pixels than its
DIP-equivalent footprint next to the primary, so Windows shifts it up to keep the two visually
centered — the exact offset depends on the OS's monitor arrangement algorithm, but the sign and
magnitude are the kind of value you should expect, not an error.)

An element sitting near the top-left of a window on the secondary monitor might have:

```
UIA BoundingRectangle (physical, virtual-desktop space):
  Left = -1850, Top = -120, Width = 220, Height = 34
```

This is exactly what `Ghost.Core` stores in `UiElement.Bounds` — negative `Left`/`Top` and all.
No sign-flipping, no clamping.

When `Ghost.App` positions the ring around this element:

1. It takes the `PhysicalRect` as-is from the `Resolution`.
2. `DpiHelper` determines which monitor physically contains that rect (`MonitorFromRect`) and
   reads that monitor's scale factor — **150%, from the secondary monitor, not the primary's
   100%**, even though the number is negative and "looks like" it might belong elsewhere.
3. DIPs for the overlay's `Canvas` children are computed as
   `dip = (physical - monitorOriginPhysical) / scale + monitorOriginDip`, using the secondary
   monitor's own scale factor.
4. The overlay *window* itself remains sized and positioned in raw physical pixels spanning the
   full virtual desktop (or a padded bounding box around the action, per Section 8.6), so step 3's
   DIP math only matters for the `Canvas` children inside it, not the window itself.

If step 2 used the primary monitor's 100% scale by mistake (a common bug when code assumes "DPI
awareness" means "one DPI for the whole desktop"), the ring would land roughly 1.5x too far in
both axes relative to the element — the exact "suspiciously round factor" symptom described in
rule 6.
