import { useMemo, useState } from 'react';
import { BellRing, ArrowUpRight, CheckCircle2, AlertTriangle, Send } from 'lucide-react';
import { postEvent } from '../api';
import type { LogEventCommand } from '../types';

const SAMPLE_EVENTS: Omit<LogEventCommand, 'timestamp'>[] = [
  {
    eventType: 'user_login',
    payload: '{"userId":"user-123","ipAddress":"192.168.1.100"}',
    source: 'web-client',
  },
  {
    eventType: 'payment_success',
    payload: '{"transactionId":"txn-456","amount":150.75}',
    source: 'payment-gateway',
  },
  {
    eventType: 'cache_hit',
    payload: '{"cacheKey":"dashboard:43200","hit":true}',
    source: 'redis-cache',
  },
  {
    eventType: 'error_occurred',
    payload: '{"code":500,"message":"database timeout"}',
    source: 'api-service',
  },
];

export function EventLogger() {
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError]   = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const onSelectEvent = async (event: Omit<LogEventCommand, 'timestamp'>) => {
    setStatus(null);
    setError(null);
    setIsSaving(true);

    try {
      const result = await postEvent({ ...event, timestamp: new Date().toISOString() });
      setStatus(result.message ?? 'Event sent successfully');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to send event');
    } finally {
      setIsSaving(false);
    }
  };

  const buttonLabel = useMemo(() => (isSaving ? 'Sending...' : 'Ready to send'), [isSaving]);

  return (
    <section className="rounded-3xl border border-slate-800/80 bg-slate-950/80 p-6 shadow-xl shadow-slate-950/20">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-sm uppercase tracking-[0.24em] text-sky-300">Event generator</p>
          <h2 className="mt-2 text-2xl font-semibold text-white">Send sample backend events</h2>
          <p className="mt-2 text-sm leading-6 text-slate-400">
            Click any event card below to send it through the analytics pipeline in real time.
          </p>
        </div>
        <div className="flex items-center gap-2 rounded-2xl bg-slate-900/80 px-4 py-3 text-sm text-slate-300">
          <BellRing className="h-4 w-4" /> Auto refresh on backend sync.
        </div>
      </div>

      {/* ✅ Event cards — واضح إنهم clickable */}
      <div className="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {SAMPLE_EVENTS.map((event) => (
          <button
            key={event.eventType}
            type="button"
            disabled={isSaving}
            onClick={() => onSelectEvent(event)}
            className="group rounded-3xl border border-slate-800/80 bg-slate-900/70 px-4 py-5 text-left text-sm text-slate-100 transition hover:border-sky-500/50 hover:bg-slate-900 disabled:cursor-not-allowed disabled:opacity-50"
          >
            <div className="flex items-center justify-between gap-3">
              <span className="font-semibold text-white">{event.eventType}</span>
              <ArrowUpRight className="h-4 w-4 text-slate-400 transition group-hover:text-sky-400" />
            </div>
            <p className="mt-3 text-xs text-slate-500">{event.source}</p>
            <p className="mt-3 text-[10px] font-semibold uppercase tracking-widest text-sky-600 group-hover:text-sky-400 transition">
              Click to send →
            </p>
          </button>
        ))}
      </div>

      {/* Status messages */}
      <div className="mt-6 space-y-3">
        {status ? (
          <div className="rounded-3xl bg-emerald-500/10 px-4 py-3 text-sm text-emerald-200">
            <CheckCircle2 className="inline-block h-4 w-4 mr-2 align-middle" /> {status}
          </div>
        ) : null}
        {error ? (
          <div className="rounded-3xl bg-rose-500/10 px-4 py-3 text-sm text-rose-200">
            <AlertTriangle className="inline-block h-4 w-4 mr-2 align-middle" /> {error}
          </div>
        ) : null}
      </div>

      <div className="mt-6 flex flex-wrap items-center justify-between gap-3 rounded-3xl bg-slate-900/80 p-4 text-sm text-slate-400">
        <div className="flex items-center gap-2">
          <Send className="h-4 w-4" />
          <span>{buttonLabel}</span>
        </div>
        <span className={`rounded-full px-3 py-1 text-xs font-semibold ${isSaving ? 'bg-sky-500/20 text-sky-300' : 'bg-slate-800/80 text-slate-400'}`}>
          {isSaving ? 'Working...' : 'Idle'}
        </span>
      </div>
    </section>
  );
}
