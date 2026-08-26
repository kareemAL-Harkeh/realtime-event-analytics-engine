import { afterEach, describe, expect, it, vi } from 'vitest';
import { fetchDashboard, postEvent } from './api';
import type { LogEventCommand } from './types';

const validCommand: LogEventCommand = {
  eventType: 'user_login',
  payload: '{"userId":"user-1"}',
  source: 'test-client',
};

afterEach(() => {
  vi.restoreAllMocks();
});

describe('API client', () => {
  it('fetches dashboard data with the dashboard API key and unwraps the response', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          status: 'Success',
          data: {
            totalEvents: 7,
            eventsByType: { user_login: 7 },
            recentSuccessRate: 100,
          },
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      ),
    );

    await expect(fetchDashboard(30)).resolves.toEqual({
      totalEvents: 7,
      eventsByType: { user_login: 7 },
      recentSuccessRate: 100,
    });

    expect(fetchMock).toHaveBeenCalledWith('/api/dashboard?windowMinutes=30', {
      headers: { 'X-Api-Key': 'dev-dashboard-key' },
    });
  });

  it('posts events with the ingestion API key and unwraps the acknowledgement', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          status: 'Success',
          data: { message: 'Event queued for asynchronous processing.' },
        }),
        { status: 202, headers: { 'Content-Type': 'application/json' } },
      ),
    );

    await expect(postEvent(validCommand)).resolves.toEqual({
      message: 'Event queued for asynchronous processing.',
    });

    expect(fetchMock).toHaveBeenCalledWith('/api/events', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Api-Key': 'dev-ingestion-key',
      },
      body: JSON.stringify(validCommand),
    });
  });

  it('includes the backend response when an API request fails', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response('{"status":"Unauthorized"}', {
        status: 401,
        statusText: 'Unauthorized',
      }),
    );

    await expect(fetchDashboard()).rejects.toThrow(
      'Dashboard request failed: 401 Unauthorized {"status":"Unauthorized"}',
    );
  });
});
