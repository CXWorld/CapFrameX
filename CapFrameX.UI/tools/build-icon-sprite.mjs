/**
 * Builds `public/icons.svg` from the Tabler outline set.
 *
 * A sprite of the icons this interface actually uses, committed rather than generated at build
 * time: it is a handful of kilobytes, it lets the browser cache one file, and it keeps the icon
 * set out of the dependency graph of every build. Run this again after adding a name below.
 *
 *   node tools/build-icon-sprite.mjs
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const source = join(here, '..', 'node_modules', '@tabler', 'icons', 'icons', 'outline');
const target = join(here, '..', 'public', 'icons.svg');

/** Every icon the interface refers to by name. */
const icons = [
  'activity',
  'arrows-left-right',
  'camera',
  'chart-line',
  'check',
  'chevron-down',
  'cloud',
  'download',
  'eye',
  'file-description',
  'search',
  'settings',
  'stack-2',
  'x',
];

const symbols = icons.map((name) => {
  const svg = readFileSync(join(source, `${name}.svg`), 'utf8');
  const body = svg.replace(/^[\s\S]*?<svg[^>]*>/, '').replace(/<\/svg>\s*$/, '').trim();

  // Tabler draws on a 24 unit grid with a 2 unit stroke; the stroke width is set here so a single
  // <use> is enough at the call site.
  return `  <symbol id="${name}" viewBox="0 0 24 24" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round">\n${body}\n  </symbol>`;
});

const sprite = `<svg xmlns="http://www.w3.org/2000/svg" style="display:none">
${symbols.join('\n')}
</svg>
`;

writeFileSync(target, sprite, 'utf8');
console.log(`wrote ${target} with ${icons.length} icons`);
