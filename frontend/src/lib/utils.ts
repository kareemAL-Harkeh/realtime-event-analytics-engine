export function formatNumber(value: number): string {
  return new Intl.NumberFormat('en-US').format(value);
}

export function formatDateTime(value: string | Date): string {
  const date = typeof value === 'string' ? new Date(value) : value;
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(date);
}

export function statusColor(eventType: string): string {
  const normalized = eventType.toLowerCase();
  if (normalized.includes('error') || normalized.includes('fail')) return 'bg-rose-500/10 text-rose-200';
  if (normalized.includes('success') || normalized.includes('ok')) return 'bg-emerald-500/10 text-emerald-200';
  if (normalized.includes('warn') || normalized.includes('warning')) return 'bg-amber-500/10 text-amber-200';
  return 'bg-sky-500/10 text-sky-200';
}

export function eventColor(eventType: string): string {
  const normalized = eventType.toLowerCase();
  if (normalized.includes('error') || normalized.includes('fail')) return '#fb7185';
  if (normalized.includes('success') || normalized.includes('ok')) return '#34d399';
  if (normalized.includes('warn') || normalized.includes('warning')) return '#f59e0b';
  return '#60a5fa';
}
