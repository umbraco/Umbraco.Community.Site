import { html } from "lit";

export const arrowLeft = html`<svg
  width="44"
  height="44"
  viewBox="0 0 44 44"
  fill="none"
  xmlns="http://www.w3.org/2000/svg"
>
  <circle cx="22" cy="22" r="22" fill="var(--circle-fill, transparent)" />
  <path
    style="fill: var(--fill, #000)"
    d="M12,23.1h20c0.6,0,1-0.4,1-1V22c0-0.6-0.4-1-1-1H12c-0.6,0-1,0.4-1,1v0.1C11,22.6,11.5,23.1,12,23.1z"
  />
  <path
    style="fill: var(--fill, #000)"
    d="M12.8,22.6l7.1-7.1c0.4-0.4,0.4-1,0-1.4l-0.1-0.1c-0.4-0.4-1-0.4-1.4,0l-7.1,7.1c-0.4,0.4-0.4,1,0,1.4l0.1,0.1
	C11.8,23,12.4,23,12.8,22.6z"
  />
  <path
    style="fill: var(--fill, #000)"
    d="M19.9,28.4l-7.1-7.1c-0.4-0.4-1-0.4-1.4,0l-0.1,0.1c-0.4,0.4-0.4,1,0,1.4l7.1,7.1c0.4,0.4,1,0.4,1.4,0l0.1-0.1
	C20.3,29.4,20.3,28.8,19.9,28.4z"
  />
</svg>`;

export const arrowRight = html`<svg
  width="44"
  height="44"
  viewBox="0 0 44 44"
  fill="none"
  xmlns="http://www.w3.org/2000/svg"
>
<circle cx="22" cy="22" r="22" fill="var(--circle-fill, transparent)"/>
<path
    style="fill: var(--fill, #000)" d="M32,23.1H12c-0.6,0-1-0.4-1-1V22c0-0.6,0.4-1,1-1h20c0.6,0,1,0.4,1,1v0.1C33,22.6,32.6,23.1,32,23.1z"/>
<path
    style="fill: var(--fill, #000)" d="M31.2,22.6l-7.1-7.1c-0.4-0.4-0.4-1,0-1.4l0.1-0.1c0.4-0.4,1-0.4,1.4,0l7.1,7.1c0.4,0.4,0.4,1,0,1.4l-0.1,0.1
	C32.3,23,31.6,23,31.2,22.6z"/>
<path
    style="fill: var(--fill, #000)" d="M24.2,28.4l7.1-7.1c0.4-0.4,1-0.4,1.4,0l0.1,0.1c0.4,0.4,0.4,1,0,1.4l-7.1,7.1c-0.4,0.4-1,0.4-1.4,0l-0.1-0.1
	C23.8,29.4,23.8,28.8,24.2,28.4z"/>
</svg>`;

export const modernArrow = html`
  <svg xmlns="http://www.w3.org/2000/svg" width="44" height="44" viewBox="0 0 33 33" fill="none">
    <g>
      <path d="M16.5967 9.87891L23.1568 16.4391L16.5967 22.9992" stroke="#283A97" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
      <path d="M10.0365 16.4391L23.1568 16.4391" stroke="#283A97" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
    </g>
  </svg>
`;
