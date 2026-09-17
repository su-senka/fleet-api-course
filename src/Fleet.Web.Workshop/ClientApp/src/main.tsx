import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';

// StrictMode double-invokes effects and renders in development on purpose, to surface effects
// that are not idempotent. If something breaks only under StrictMode, the effect is the bug.
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
