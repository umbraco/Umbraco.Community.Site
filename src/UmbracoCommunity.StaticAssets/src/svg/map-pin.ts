import { html } from 'lit-html';

export const mapPin = html`
    <svg width="20" height="32" viewBox="0 0 20 32" fill="var(--fill, none)" xmlns="http://www.w3.org/2000/svg">
        <path
            fill-rule="evenodd"
            clip-rule="evenodd"
            d="M19.3413 10.0066C19.3413 16.5458 9.99388 29.9011 9.99388 29.9011C9.99388 29.9011 0.646484 16.5458 0.646484 10.0066C0.659668 4.83848 4.83897 0.65918 10.0071 0.65918C15.1752 0.65918 19.3545 4.83848 19.3545 10.0066H19.3413Z"
            stroke="var(--stroke, var(--color-blue))"
            stroke-miterlimit="10"
        />
        <path
            d="M10.0064 13.7375C12.067 13.7375 13.7375 12.067 13.7375 10.0064C13.7375 7.94584 12.067 6.27539 10.0064 6.27539C7.94584 6.27539 6.27539 7.94584 6.27539 10.0064C6.27539 12.067 7.94584 13.7375 10.0064 13.7375Z"
            stroke="var(--stroke, var(--color-blue))"
            stroke-miterlimit="10"
            fill="var(--dot-fill, none)"
        />
    </svg>
`;
