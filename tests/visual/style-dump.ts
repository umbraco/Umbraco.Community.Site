export function dumpComputedStyles(skipSelectors: string[]): string {
  const inherited = [
    'color', 'font-family', 'font-size', 'font-style', 'font-weight', 'letter-spacing',
    'line-height', 'text-align', 'text-transform', 'text-decoration-line', 'white-space',
  ];
  const own: Record<string, string[]> = {
    display: ['inline', 'block'],
    position: ['static'],
    'background-color': ['rgba(0, 0, 0, 0)'],
    'background-image': ['none'],
    'text-decoration-color': [],
    'margin-top': ['0px'], 'margin-right': ['0px'], 'margin-bottom': ['0px'], 'margin-left': ['0px'],
    'padding-top': ['0px'], 'padding-right': ['0px'], 'padding-bottom': ['0px'], 'padding-left': ['0px'],
    'border-top': ['0px none'], 'border-right': ['0px none'], 'border-bottom': ['0px none'], 'border-left': ['0px none'],
    'border-radius': ['0px'],
    'box-shadow': ['none'],
    'outline-style': ['none'],
    'row-gap': ['normal'], 'column-gap': ['normal'],
    'grid-template-columns': ['none'],
    'max-width': ['none'], 'min-height': ['auto', '0px'],
    'aspect-ratio': ['auto'],
    'object-fit': ['fill'],
    opacity: ['1'],
    transform: ['none'],
    filter: ['none'],
    'z-index': ['auto'],
    overflow: ['visible'],
  };
  const skipTags = new Set(['script', 'style', 'link', 'meta', 'noscript', 'template', 'title', 'head', 'base']);
  const lines: string[] = [];

  const shorten = (prop: string, value: string) =>
    /^border-(top|right|bottom|left)$/.test(prop) && / none /.test(value) ? '0px none' : value;

  const describe = (style: CSSStyleDeclaration, parent: CSSStyleDeclaration | null) => {
    const parts: string[] = [];
    for (const prop of inherited) {
      const v = style.getPropertyValue(prop);
      if (!parent || parent.getPropertyValue(prop) !== v) parts.push(`${prop}:${v}`);
    }
    for (const [prop, defaults] of Object.entries(own)) {
      const v = shorten(prop, style.getPropertyValue(prop)).trim();
      if (prop === 'text-decoration-color') {
        if (style.getPropertyValue('text-decoration-line') !== 'none') parts.push(`${prop}:${v}`);
      } else if (!defaults.includes(v)) {
        parts.push(`${prop}:${v}`);
      }
    }
    return parts.join('; ');
  };

  const walk = (root: Element | ShadowRoot, path: string, parentStyle: CSSStyleDeclaration | null) => {
    let index = 0;
    for (const el of root.children) {
      const tag = el.localName;
      if (skipTags.has(tag)) continue;
      const elPath = `${path}>${tag}:${index++}`;
      const style = getComputedStyle(el);
      const r = el.getBoundingClientRect();
      const box = `@${Math.round(r.left)},${Math.round(r.top + window.scrollY)} ${Math.round(r.width)}x${Math.round(r.height)}`;
      const cls = typeof el.className === 'string' && el.className ? ` .${el.className.trim().split(/\s+/).join('.')}` : '';
      lines.push(`${elPath}${cls} ${box} { ${describe(style, parentStyle)} }`);

      for (const pseudo of ['::before', '::after']) {
        const ps = getComputedStyle(el, pseudo);
        const content = ps.getPropertyValue('content');
        if (content && content !== 'none' && content !== 'normal') {
          lines.push(`${elPath}${pseudo} { content:${content}; ${describe(ps, style)} }`);
        }
      }

      if (tag === 'svg' || skipSelectors.some(s => el.matches(s))) continue;
      if (el.shadowRoot) walk(el.shadowRoot, `${elPath}/shadow`, style);
      walk(el, elPath, style);
    }
  };

  walk(document.documentElement, '', null);
  return lines.join('\n') + '\n';
}
