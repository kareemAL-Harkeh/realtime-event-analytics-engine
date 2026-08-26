import { act, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { EventFeedItem } from '../types';

const signalRMock = vi.hoisted(() => {
  const connection = {
    on: vi.fn(),
    onclose: vi.fn(),
    onreconnected: vi.fn(),
    onreconnecting: vi.fn(),
    start: vi.fn(() => Promise.resolve()),
    stop: vi.fn(() => Promise.resolve()),
  };

  const builder = {
    withUrl: vi.fn(() => builder),
    withAutomaticReconnect: vi.fn(() => builder),
    build: vi.fn(() => connection),
  };

  return { connection, builder };
});

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: vi.fn(function () {
    return signalRMock.builder;
  }),
}));

import { useRealtimeFeed } from './useRealtime';

const event = (eventType: string): EventFeedItem => ({
  eventType,
  source: 'test-client',
  timestamp: new Date().toISOString(),
  payload: '{}',
});

function registeredHandler(eventName: string) {
  const registration = signalRMock.connection.on.mock.calls.find(
    ([name]) => name === eventName,
  );

  if (!registration) {
    throw new Error(`No handler registered for ${eventName}`);
  }

  return registration[1] as (value: EventFeedItem) => void;
}

afterEach(() => {
  vi.clearAllMocks();
});

describe('useRealtimeFeed', () => {
  it('connects to the hub and receives events newest first', async () => {
    const { result } = renderHook(() => useRealtimeFeed());

    await act(async () => {
      await Promise.resolve();
    });

    expect(signalRMock.builder.withUrl).toHaveBeenCalledWith('/eventHub');
    expect(signalRMock.builder.withAutomaticReconnect).toHaveBeenCalled();
    expect(result.current.connected).toBe(true);
    expect(result.current.hasError).toBe(false);

    act(() => {
      registeredHandler('ReceiveEvent')(event('first'));
      registeredHandler('ReceiveEvent')(event('second'));
    });

    expect(result.current.events.map((item) => item.eventType)).toEqual(['second', 'first']);
  });

  it('keeps only the latest twelve events', () => {
    const { result } = renderHook(() => useRealtimeFeed());
    const receiveEvent = registeredHandler('ReceiveEvent');

    act(() => {
      for (let index = 1; index <= 13; index += 1) {
        receiveEvent(event(`event-${index}`));
      }
    });

    expect(result.current.events).toHaveLength(12);
    expect(result.current.events[0].eventType).toBe('event-13');
    expect(result.current.events[11].eventType).toBe('event-2');
  });

  it('updates connection state and stops the hub on unmount', async () => {
    const { result, unmount } = renderHook(() => useRealtimeFeed());
    const reconnect = signalRMock.connection.onreconnected.mock.calls[0][0] as () => void;
    const reconnecting = signalRMock.connection.onreconnecting.mock.calls[0][0] as () => void;
    const close = signalRMock.connection.onclose.mock.calls[0][0] as () => void;

    await act(async () => {
      await Promise.resolve();
    });

    act(() => reconnecting());
    expect(result.current.connected).toBe(false);

    act(() => reconnect());
    expect(result.current.connected).toBe(true);
    expect(result.current.hasError).toBe(false);

    act(() => close());
    expect(result.current.connected).toBe(false);

    unmount();
    expect(signalRMock.connection.stop).toHaveBeenCalled();
  });
});
