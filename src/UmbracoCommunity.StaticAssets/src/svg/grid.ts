import { html } from 'lit-html';

export const grid = html`
    <svg width="25" height="25" viewBox="0 0 25 25" fill="none" xmlns="http://www.w3.org/2000/svg">
        <rect x="0.5" y="0.5" width="6" height="6" rx="1.5" stroke="var(--stroke, white)" />
        <rect x="9.5" y="0.5" width="6" height="6" rx="1.5" stroke="var(--stroke, white)" />
        <rect x="18.5" y="0.5" width="6" height="6" rx="1.5" stroke="var(--stroke, white)" />
        <path
            d="M2 9.5C1.17157 9.5 0.5 10.1716 0.5 11V14C0.5 14.8284 1.17157 15.5 2 15.5H5C5.82843 15.5 6.5 14.8284 6.5 14V11C6.5 10.1716 5.82843 9.5 5 9.5H2Z"
            stroke="var(--stroke, white)"
        />
        <rect x="9.5" y="9.5" width="6" height="6" rx="1.5" stroke="var(--stroke, white)" />
        <rect x="18.5" y="9.5" width="6" height="6" rx="1.5" stroke="var(--stroke, white)" />
        <rect x="0.5" y="18.5" width="6" height="6" rx="1.5" stroke="var(--stroke, white)" />
        <rect x="9.5" y="18.5" width="6" height="6" rx="1.5" stroke="var(--stroke, white)" />
        <rect x="18.5" y="18.5" width="6" height="6" rx="1.5" stroke="var(--stroke, white)" />
    </svg>`;
