import { useEffect, useRef, useState } from 'react';
import { HubConnectionBuilder } from '@microsoft/signalr';
import type { EventFeedItem } from '../types';

const SIGNALR_URL = import.meta.env.VITE_SIGNALR_URL || '/eventHub';

export function useRealtimeFeed() {
  const [events, setEvents] = useState<EventFeedItem[]>([]);
  const [connected, setConnected] = useState(false);
  const [hasError, setHasError] = useState(false);
  const connectionRef = useRef<any>(null);

  useEffect(() => {
    const connection = new HubConnectionBuilder().withUrl(SIGNALR_URL).withAutomaticReconnect().build();

    const pushEvent = (event: EventFeedItem) => {
      setEvents((current) => {
        const next = [event, ...current];
        return next.slice(0, 12);
      });
    };

    connection.on('ReceiveEvent', pushEvent);
    connection.on('NewEvent', pushEvent);
    connection.on('EventReceived', pushEvent);

    connection.onreconnected(() => {
      setConnected(true);
      setHasError(false);
    });

    connection.onreconnecting(() => {
      setConnected(false);
    });

    connection.onclose(() => {
      setConnected(false);
    });

    connection
      .start()
      .then(() => {
        setConnected(true);
        setHasError(false);
      })
      .catch(() => {
        setConnected(false);
        setHasError(true);
      });

    connectionRef.current = connection;

    return () => {
      connection.stop();
    };
  }, []);

  return { events, connected, hasError };
}
