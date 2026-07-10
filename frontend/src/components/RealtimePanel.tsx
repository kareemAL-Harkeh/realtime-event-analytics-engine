import { motion } from 'framer-motion';
import { CircleDot, Wifi, WifiOff } from 'lucide-react';
import { useRealtimeFeed } from '../hooks/useRealtime';
import { formatDateTime, statusColor } from '../lib/utils';

export function RealtimePanel() {
  const { events, connected, hasError } = useRealtimeFeed();

  return (
    <section className="rounded-3xl border border-slate-800/80 bg-slate-950/80 p-6 shadow-xl shadow-slate-950/20">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-sm uppercase tracking-[0.24em] text-sky-300">Live event feed</p>
          <h2 className="mt-2 text-2xl font-semibold text-white">SignalR stream</h2>
        </div>
        <div className="flex items-center gap-2 rounded-full bg-slate-900/80 px-4 py-2 text-sm text-slate-300">
          {connected && !hasError ? (
            <Wifi className="h-4 w-4 text-emerald-400" />
          ) : (
            <WifiOff className="h-4 w-4 text-rose-400" />
          )}
          <span>{connected ? 'Connected' : hasError ? 'Offline' : 'Connecting'}</span>
        </div>
      </div>

      <motion.div layout className="mt-6 space-y-4">
        {events.length === 0 ? (
          <div className="rounded-3xl border border-slate-800/70 bg-slate-900/70 p-8 text-center text-sm text-slate-500">
            No live events have arrived yet. Events will appear here when the backend hub publishes a message.
          </div>
        ) : (
          <div className="space-y-3">
            {events.map((event, index) => (
              // ✅ key أكثر فرادة — يضمن عدم التعارض بين events متشابهة
              <div
                key={`${event.eventType}-${event.timestamp}-${index}`}
                className="rounded-3xl border border-slate-800/70 bg-slate-900/70 p-4"
              >
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div className="flex items-center gap-2">
                    <span className={`inline-flex items-center rounded-full px-3 py-1 text-xs font-semibold ${statusColor(event.eventType)}`}>
                      {event.eventType}
                    </span>
                    <span className="text-sm text-slate-400">{event.source}</span>
                  </div>
                  <div className="flex items-center gap-2 text-xs text-slate-500">
                    <CircleDot className="h-3.5 w-3.5" />
                    {formatDateTime(event.timestamp)}
                  </div>
                </div>
                {event.payload ? (
                  <pre className="mt-3 overflow-x-auto rounded-2xl bg-slate-950/90 p-3 text-xs text-slate-300">
                    {event.payload}
                  </pre>
                ) : null}
              </div>
            ))}
          </div>
        )}
      </motion.div>
    </section>
  );
}
