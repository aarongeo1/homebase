import js from '@eslint/js';
import { defineConfig, globalIgnores } from 'eslint/config';
import reactHooks from 'eslint-plugin-react-hooks';
import globals from 'globals';
import tseslint from 'typescript-eslint';

// Architecture rules live here so they are enforced, not just documented (ADR-0001).
// Note: a later `no-restricted-imports` entry REPLACES an earlier one for matching
// files, so every block repeats the global bans via `banned()`.
const BANNED_PACKAGES = [
  { name: 'react-router-dom', message: "React Router 8 removed react-router-dom. Import from 'react-router' (RouterProvider from 'react-router/dom')." },
  { name: 'axios', message: 'Use api from @/shared/api — it is typed against contracts/openapi.yaml.' },
];
const DEEP_DOMAIN_IMPORT = { group: ['@/domains/*/**'], message: "Import other areas through their public API, e.g. '@/domains/billing' (ADR-0001)." };
const DEEP_SHARED_IMPORT = {
  group: ['@/shared/*/*', '!@/shared/api/mock', '!@/shared/api/mock/*'],
  message: "Import shared code from its index: '@/shared/ui', '@/shared/lib' or '@/shared/api'.",
};
const banned = (...patterns) => ['error', { paths: BANNED_PACKAGES, patterns: [DEEP_DOMAIN_IMPORT, ...patterns] }];

export default defineConfig([
  globalIgnores(['dist', 'coverage', 'src/shared/api/generated']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, tseslint.configs.recommended, reactHooks.configs.flat.recommended],
    languageOptions: { globals: globals.browser },
    rules: {
      'no-restricted-imports': banned(DEEP_SHARED_IMPORT),
      'no-restricted-syntax': [
        'error',
        { selector: "CallExpression[callee.name='fetch']", message: 'Call the backend through api from @/shared/api, never fetch() directly.' },
      ],
    },
  },
  {
    // shared/ is the bottom layer: it may import its own siblings directly, never domains or the shell.
    files: ['src/shared/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': banned({
        group: ['@/domains', '@/domains/*', '@/app', '@/app/*'],
        message: 'shared/ must not depend on domains or the app shell (ADR-0001).',
      }),
    },
  },
  {
    files: ['src/domains/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': banned(DEEP_SHARED_IMPORT, {
        group: ['@/app', '@/app/*'],
        message: 'Domains must not depend on the app shell (ADR-0001).',
      }),
    },
  },
  {
    files: ['src/shared/api/client.ts'],
    rules: { 'no-restricted-syntax': 'off' },
  },
]);
