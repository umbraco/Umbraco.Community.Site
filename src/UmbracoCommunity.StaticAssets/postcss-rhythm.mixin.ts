/**
 * Generates a set of immutable utility classes for controlling margins and padding on all logical properties
 * eg top, right, bottom, left, x, y.
 *
 * Values are generated using -xs, -sm, -md, -lg, -xl suffix, outputting classes like (all are suffixed !important for immutability):
 * .pt-md { padding-top: var(--unit-md); }
 * .m { margin: var(--unit); }
 * .mx-xs { margin-left: var(--unit-xs); margin-right: var(--unit-xs); } // this is two separate rules
 *
 * The root $unit value is 1rem, and should be scaled via setting a base font size
 * @param mixin
 * @param property
 * @returns
 */
export function getRhythm(mixin, property) {
  const ruleset = {};
  const key = property[0];

  const logicalProps = ["top", "right", "bottom", "left"];
  const unitModifiers = ["-xxs", "-xs", "-sm", "", "-md", "-lg", "-xl", "-0"];

  unitModifiers.forEach((modifier, i) => {
    const rule =
      i === unitModifiers.length - 1 ? "0 !important" : `calc(var(--unit${modifier}))!important`;

    ruleset[`.${key}${modifier}`] = {
      [property]: rule,
    };

    logicalProps.forEach((prop) => {
      let axis = prop === "top" || prop === "bottom" ? "y" : "x";
      ruleset[`.${key}${prop[0]}${modifier}, .${key}${axis}${modifier}`] = {
        [`${property}-${prop}`]: rule,
      };
    });
  });

  return ruleset;
}
