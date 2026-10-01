import js from '@eslint/js';
import globals from 'globals';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import tseslint from 'typescript-eslint';

/**
 * Flat config, ESLint 10.
 *
 * `reactHooks.configs.recommended` is worth knowing about: in v7 of the plugin it carries
 * sixteen rules rather than the two (`rules-of-hooks`, `exhaustive-deps`) it used to. The extra
 * ones come from the React Compiler's analysis and are the reason this preset is now the most
 * useful thing in the file - `set-state-in-effect`, `purity`, `immutability` and
 * `set-state-in-render` reject, at lint time, the mistakes that otherwise show up as an infinite
 * render loop at runtime.
 *
 * We take the rules without enabling the compiler itself. Memoisation stays manual and visible,
 * so `useMemo` and `useCallback` are still something you choose rather than something a build
 * step does behind you.
 *
 * Note the TypeScript pin in package.json: typescript-eslint caps the TypeScript version it
 * supports, so the type-aware rules below are what stop us bumping TypeScript to 7.
 */
export default tseslint.config(
  { ignores: ['dist'] },
  {
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
    },
    plugins: {
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
    },
  },
);
