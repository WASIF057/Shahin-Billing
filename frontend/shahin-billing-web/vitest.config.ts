import { defineConfig } from 'vitest/config';

// Plain unit tests for the pure TypeScript business rules (no Angular, no browser needed).
export default defineConfig({
  test: { include: ['src/**/*.spec.ts'], environment: 'node' },
});
