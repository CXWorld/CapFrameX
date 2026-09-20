// @ts-check
const eslint = require('@eslint/js');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

/**
 * Lint rules.
 *
 * The prefix is what everything else in this repository already assumes: `cx-` for components,
 * `cx` for directive attributes, matching the token names and the mockup's class names.
 */
module.exports = tseslint.config(
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      ...tseslint.configs.recommended,
      ...tseslint.configs.stylistic,
      ...angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/directive-selector': [
        'error',
        { type: 'attribute', prefix: 'cx', style: 'camelCase' },
      ],
      '@angular-eslint/component-selector': [
        'error',
        { type: 'element', prefix: 'cx', style: 'kebab-case' },
      ],
    },
  },
  {
    files: ['**/*.html'],
    extends: [...angular.configs.templateRecommended, ...angular.configs.templateAccessibility],
    rules: {},
  },
);
