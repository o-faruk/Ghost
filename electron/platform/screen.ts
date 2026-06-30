import { screen } from 'electron';

export interface DisplayInfo {
  width: number;
  height: number;
  scaleFactor: number;
  x: number;
  y: number;
}

/**
 * Returns geometry for the primary display in logical pixels + the DPI scale factor.
 * MVP: single monitor only. scaleFactor is the multiplier to convert logical → physical px.
 * Apply it when translating OmniParser bbox coords (physical) → overlay window coords (logical).
 */
export function getPrimaryDisplayInfo(): DisplayInfo {
  const display = screen.getPrimaryDisplay();
  return {
    width: display.bounds.width,
    height: display.bounds.height,
    scaleFactor: display.scaleFactor,
    x: display.bounds.x,
    y: display.bounds.y,
  };
}
