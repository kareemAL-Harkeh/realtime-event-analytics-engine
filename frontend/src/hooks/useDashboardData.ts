import { useEffect, useMemo, useState } from 'react';
import type { DashboardOverview } from '../types';
import { fetchDashboard } from '../api';

export function useDashboardData(windowMinutes = 43200) {
  const [dashboard, setDashboard] = useState<DashboardOverview | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadDashboard = async () => {
    setIsLoading(true);
    setError(null);

    try {
      const result = await fetchDashboard(windowMinutes);
      setDashboard(result);
    } catch (err) {
      setDashboard(null);
      setError(err instanceof Error ? err.message : 'Unable to load dashboard');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    let active = true;

    const refresh = async () => {
      if (!active) return;
      await loadDashboard();
    };

    refresh();
    const interval = window.setInterval(refresh, 5000);
    return () => {
      active = false;
      window.clearInterval(interval);
    };
  }, [windowMinutes]);

  const eventTypes = useMemo(() => {
    if (!dashboard) return [];
    return Object.entries(dashboard.eventsByType).map(([eventType, value]) => ({
      eventType,
      value,
    }));
  }, [dashboard]);

  return { dashboard, eventTypes, isLoading, error, refresh: loadDashboard };
}
