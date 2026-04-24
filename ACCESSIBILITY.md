# Accessibility

This document describes the accessibility standards and implementation details for the Umbraco Community Website.

## Standards

The site aims to conform to **WCAG 2.1 Level AA** guidelines where applicable.

## Implementation Details

### Focus Management

- **Focus visible indicators**: All interactive elements display visible focus states using `:focus-visible` to show focus only for keyboard navigation, not mouse clicks
- **Focus outline style**: Consistent focus outline defined via CSS custom properties (`--focus-outline`, `--focus-outline-offset`)
- **Skip links**: Skip-to-content links are provided in the main layout for keyboard users

### Keyboard Navigation

- **All interactive elements are keyboard accessible**: Buttons, links, and custom controls can be operated with keyboard
- **Header navigation dropdowns**: Dropdown menus can be opened with Enter/Space keys; focus moves to first link when opened; menu closes when focus leaves the dropdown area
- **Accordion components**: Use visually-hidden checkboxes (sr-only technique) with `aria-expanded`, `aria-controls`, and `role="region"` for screen reader support. Arrow keys navigate between items (with wrapping), Home/End jump to first/last. Enter/Space toggles open/close.
- **Video playback**: Poster images are keyboard-accessible with `role="button"`, `tabindex="0"`, and Enter/Space key handlers
- **Dialog components**: Trap focus within dialogs when open and return focus when closed

### Semantic HTML

- **Proper heading hierarchy**: Pages use logical heading structure (h1-h6)
- **Landmark regions**: Main content wrapped in semantic elements (`<main>`, `<nav>`, `<header>`, `<footer>`)
- **Lists**: Navigation and grouped content use appropriate list elements

### ARIA Implementation

- **Decorative elements**: All decorative SVGs and icons have `aria-hidden="true"` to hide from assistive technology
- **Interactive icons**: Icons within buttons/links are hidden from screen readers; the parent element provides the accessible name
- **Dialog components**: Dialogs use appropriate ARIA roles and labels

### Icons

- **Lucide Icons**: The site uses Lucide icons for consistent iconography (`src/UmbracoCommunity.StaticAssets/src/svg/lucide-icons.ts`)
- **Icon accessibility**: All icons include `aria-hidden="true"` and use consistent sizing (18x18 default)
- **Razor partials**: Common icons have Razor partials (e.g., `ArrowLeftSvg.cshtml`) for use in server-rendered views
- **Avoid HTML entities**: Use Lucide icons instead of HTML entities like `&larr;` or `&rarr;` for better visual consistency and accessibility

### Color and Contrast

- **Text contrast**: Text colors meet WCAG AA contrast requirements against their backgrounds
- **Color inheritance**: Block components use CSS inheritance for consistent text/bullet colors within rich text content
- **Dark backgrounds**: Components with dark backgrounds use appropriate light text colors (`--color-white`)

### Images

- **Alt text**: Images include meaningful alt text where they convey information
- **Decorative images**: Purely decorative images use empty alt attributes or `aria-hidden`
- **Responsive images**: Images scale appropriately and don't break layout at various viewport sizes

### Forms

- **Labels**: All form inputs have associated labels
- **Error messages**: Form validation errors are clearly communicated
- **Focus management**: Form fields receive focus in logical order
- **Stepped forms**: When `EnableSteppedForm` is active, the `<dc-form-steps>` component validates required fields per step, scrolls to and focuses the first invalid field, and displays Umbraco Forms validation messages. A "Step X of Y" indicator communicates progress. Users can navigate back to previous steps without validation.

### Motion and Animation

- **Prefers-reduced-motion**: The site respects the user's motion preferences via `@media (prefers-reduced-motion: reduce)`
- **Auto-sliding image slider**: The `<dc-image-slider>` component checks `prefers-reduced-motion` on connect and disables auto-slide entirely when the user prefers reduced motion. Cloned images used for infinite looping are marked `aria-hidden="true"` to avoid duplicate screen reader announcements.
- **Animation timing**: Transitions use appropriate durations that don't cause discomfort

## CSS Architecture for Accessibility

### Focus Styles

The site defines global focus style variables in `root.css`:

```css
--focus-outline: 2px solid var(--color-blue);
--focus-outline-offset: 2px;
```

Components apply these consistently:

```css
.element:focus-visible {
  outline: var(--focus-outline);
  outline-offset: var(--focus-outline-offset);
}
```

### Color Inheritance

Block components use CSS inheritance to ensure child elements (including list markers) inherit the correct text color:

```css
/* List markers inherit text color */
li::marker {
  color: inherit;
}

/* Parent sets color once, children inherit */
.dc-block {
  color: var(--color-blue);
}

.dc-block.bg-dark {
  color: var(--color-white);
}
```

### Accessible Hidden Content

Visually hidden content for screen readers uses the `.sr-only` utility class defined in `utilities.css`.

## Testing

Accessibility should be tested:

1. **Keyboard navigation**: Tab through all interactive elements
2. **Screen reader**: Test with NVDA, VoiceOver, or similar
3. **Color contrast**: Use browser dev tools or WAVE extension
4. **Zoom**: Test at 200% zoom level
5. **Reduced motion**: Test with `prefers-reduced-motion` enabled

## Known Limitations

- Third-party integrations (Sessionize, analytics) may have their own accessibility considerations
- User-generated content from Umbraco CMS depends on content editors following accessibility best practices

## Resources

- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
- [MDN Accessibility Documentation](https://developer.mozilla.org/en-US/docs/Web/Accessibility)
- [A11y Project Checklist](https://www.a11yproject.com/checklist/)
