import { DEFAULT_API_BASE_URL, readRuntimeConfig } from './runtime-config';

describe('readRuntimeConfig', () => {
  it('takes what the host set', () => {
    const config = readRuntimeConfig({ apiBaseUrl: 'http://127.0.0.1:9000', token: 'abc' });

    expect(config.apiBaseUrl).toBe('http://127.0.0.1:9000');
    expect(config.token).toBe('abc');
  });

  it('drops a trailing slash, so joining a path cannot double it', () => {
    expect(readRuntimeConfig({ apiBaseUrl: 'http://127.0.0.1:9000/' }).apiBaseUrl).toBe(
      'http://127.0.0.1:9000',
    );
  });

  it('falls back to the port the service listens on when there is no host', () => {
    expect(readRuntimeConfig(undefined).apiBaseUrl).toBe(DEFAULT_API_BASE_URL);
  });

  it('has no token without a host, so the service refuses and says so', () => {
    // Better than inventing one: a developer running `ng serve` should see the refusal rather than
    // wonder why nothing arrives.
    expect(readRuntimeConfig(undefined).token).toBe('');
  });
});
