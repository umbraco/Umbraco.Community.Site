// https://www.nbdtech.com/Blog/archive/2008/04/27/calculating-the-perceived-brightness-of-a-color.aspx
export function hexToRgb(hex: string) {
  return {
    r: parseInt(`0x${hex[1]}${hex[2]}`, 16) || 0,
    g: parseInt(`0x${hex[3]}${hex[4]}`, 16) || 0,
    b: parseInt(`0x${hex[5]}${hex[6]}`, 16) || 0,
  };
}

export function isColorDark(color: string) {
  const colorAsRgb = hexToRgb(color);
  const colorIsDark =
    Math.sqrt(
      0.241 * colorAsRgb.r * colorAsRgb.r +
        0.691 * colorAsRgb.g * colorAsRgb.g +
        0.068 * colorAsRgb.b * colorAsRgb.b
    ) < 130;

  return colorIsDark;
}

export function getAccessibleTextColor(color: string) {
  const colorAsRgb = hexToRgb(color);
  // http://www.w3.org/TR/AERT#color-contrast
  const brightness = Math.round(
    (colorAsRgb.r * 299 + colorAsRgb.g * 587 + colorAsRgb.b * 114) / 1000
  );

  return brightness > 125 ? "var(--black, #000000)" : "var(--white, #ffffff)";
}
