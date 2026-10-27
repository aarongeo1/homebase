import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';
import { resetMockData } from '@/shared/api/mock/mock-fetch';

afterEach(() => {
  cleanup();
  resetMockData();
});
