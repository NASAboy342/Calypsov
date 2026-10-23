import { h, type Component, type FunctionalComponent } from 'vue'

/**
 * Extra profile icons, defined as raw SVG markup (same 24x24 / 1.8 stroke / currentColor line style
 * as the components in `components/icons/`) instead of one .vue file each — they're only ever used
 * as picker options, so this keeps ~18 small glyphs in one place. The markup is static, trusted
 * strings, never user input.
 */
function glyph(markup: string): Component {
  const Glyph: FunctionalComponent = () =>
    h('svg', {
      viewBox: '0 0 24 24',
      fill: 'none',
      stroke: 'currentColor',
      'stroke-width': 1.8,
      'stroke-linecap': 'round',
      'stroke-linejoin': 'round',
      'aria-hidden': 'true',
      innerHTML: markup,
    })
  return Glyph
}

export const EXTRA_PROFILE_ICONS: { key: string; icon: Component }[] = [
  { key: 'briefcase', icon: glyph('<rect x="3" y="7" width="18" height="13" rx="2"/><path d="M9 7V5a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2M3 13h18"/>') },
  { key: 'user', icon: glyph('<circle cx="12" cy="8" r="3.5"/><path d="M5 20a7 7 0 0 1 14 0"/>') },
  { key: 'heart', icon: glyph('<path d="M12 20s-7-4.4-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 5.6-7 10-7 10z"/>') },
  { key: 'star', icon: glyph('<path d="M12 3.5l2.6 5.3 5.9.9-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8-4.3-4.1 5.9-.9z"/>') },
  { key: 'key', icon: glyph('<circle cx="8" cy="15" r="4"/><path d="M11 12l9-9M16 7l3 3M14 9l2 2"/>') },
  { key: 'cloud', icon: glyph('<path d="M7 18a4 4 0 0 1-.5-8A5.5 5.5 0 0 1 17 8.5 4.5 4.5 0 0 1 17.5 18z"/>') },
  { key: 'laptop', icon: glyph('<rect x="5" y="5" width="14" height="10" rx="1.5"/><path d="M3 19h18"/>') },
  { key: 'code', icon: glyph('<path d="M8 8l-4 4 4 4M16 8l4 4-4 4M14 5l-4 14"/>') },
  { key: 'gamepad', icon: glyph('<rect x="3" y="8" width="18" height="10" rx="5"/><path d="M8 11v4M6 13h4"/><circle cx="15.5" cy="12" r=".6"/><circle cx="17.5" cy="14" r=".6"/>') },
  { key: 'book', icon: glyph('<path d="M4 5a1 1 0 0 1 1-1h6v16H5a1 1 0 0 1-1-1zM20 5a1 1 0 0 0-1-1h-6v16h6a1 1 0 0 0 1-1z"/>') },
  { key: 'camera', icon: glyph('<path d="M4 8h3l1.5-2h7L17 8h3a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V9a1 1 0 0 1 1-1z"/><circle cx="12" cy="13" r="3.5"/>') },
  { key: 'music', icon: glyph('<path d="M9 18V6l10-2v12"/><circle cx="6.5" cy="18" r="2.5"/><circle cx="16.5" cy="16" r="2.5"/>') },
  { key: 'wallet', icon: glyph('<path d="M4 7a2 2 0 0 1 2-2h11v3"/><rect x="4" y="7" width="16" height="12" rx="2"/><circle cx="16" cy="13" r="1"/>') },
  { key: 'card', icon: glyph('<rect x="3" y="6" width="18" height="13" rx="2"/><path d="M3 10h18M7 15h3"/>') },
  { key: 'coffee', icon: glyph('<path d="M5 9h12v6a4 4 0 0 1-4 4H9a4 4 0 0 1-4-4zM17 10h1.5a2.5 2.5 0 0 1 0 5H17M8 3v2M12 3v2"/>') },
  { key: 'plane', icon: glyph('<path d="M21 3L3 10.5l7 2.5 2.5 7zM10 13l11-10"/>') },
  { key: 'sun', icon: glyph('<circle cx="12" cy="12" r="4"/><path d="M12 3v2M12 19v2M3 12h2M19 12h2M5.6 5.6L7 7M17 17l1.4 1.4M5.6 18.4L7 17M17 7l1.4-1.4"/>') },
  { key: 'moon', icon: glyph('<path d="M20 14.5A8 8 0 0 1 9.5 4 8 8 0 1 0 20 14.5z"/>') },
]
