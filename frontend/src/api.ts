import type { DashboardOverview, EventAcceptedResponse, LogEventCommand } from './types';

const API_BASE = import.meta.env.VITE_API_BASE || '/api';

function unwrapApiData<T>(payload: unknown): T {
  if (typeof payload !== 'object' || payload === null) {
    throw new Error('Invalid API response payload');
  }

  const candidate = payload as Record<string, unknown>;
  const data = candidate.data ?? candidate.Data;

  if (data === undefined) {
    throw new Error('API response did not contain data');
  }

  return data as T;
}

export async function fetchDashboard(windowMinutes = 43200): Promise<DashboardOverview> {
  const response = await fetch(`${API_BASE}/dashboard?windowMinutes=${windowMinutes}`);
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Dashboard request failed: ${response.status} ${response.statusText} ${text}`);
  }

  const payload = await response.json();
  return unwrapApiData<DashboardOverview>(payload);
}

export async function postEvent(command: LogEventCommand): Promise<EventAcceptedResponse> {
  const response = await fetch(`${API_BASE}/events`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(command),
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Event request failed: ${response.status} ${response.statusText} ${text}`);
  }

  const payload = await response.json();
  return unwrapApiData<EventAcceptedResponse>(payload);
}
