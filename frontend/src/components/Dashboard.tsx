import { motion } from 'framer-motion';
import { Bar, BarChart, CartesianGrid, Cell, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { ArrowRightCircle, Sparkles, ShieldCheck } from 'lucide-react';
import { formatNumber, eventColor } from '../lib/utils';
import { useDashboardData } from '../hooks/useDashboardData';

export function Dashboard() {
  const { dashboard, eventTypes, isLoading, error, refresh } = useDashboardData(43200);
  const chartData = eventTypes.map((item) => ({ name: item.eventType, count: item.value }));

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <p className="text-sm uppercase tracking-[0.24em] text-sky-300">Real-time analytics</p>
          <h1 className="mt-2 text-3xl font-semibold tracking-tight text-slate-50">Event engine dashboard</h1>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-400">
            Monitor event throughput, success ratio, and live event stream with backend-driven metrics.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <button
            type="button"
            onClick={refresh}
            className="inline-flex items-center gap-2 rounded-full border border-slate-700 bg-slate-950/80 px-4 py-2 text-sm text-slate-200 transition hover:border-slate-500 hover:bg-slate-900"
          >
            <ArrowRightCircle className="h-4 w-4" />
            Refresh now
          </button>
          {/* ✅ غيرنا من 15 min لـ 30 days */}
          <div className="rounded-full bg-slate-900/80 px-4 py-2 text-sm text-slate-300">
            Window: 30 days
          </div>
        </div>
      </div>

      {error ? (
        <div className="rounded-3xl border border-rose-500/30 bg-rose-500/10 p-6 text-rose-100">
          <p className="text-sm font-medium">Unable to load dashboard</p>
          <p className="mt-2 text-sm text-rose-200">{error}</p>
        </div>
      ) : null}

      <div className="grid gap-4 xl:grid-cols-[1.5fr_1fr]">
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-2">
          <motion.div
            layout
            initial={{ opacity: 0, y: 12 }}
            animate={{ opacity: 1, y: 0 }}
            className="rounded-3xl border border-slate-800/80 bg-slate-950/80 p-6 shadow-xl shadow-slate-950/20"
          >
            <div className="flex items-center justify-between gap-4">
              <div>
                <p className="text-sm text-slate-400">Total events</p>
                <p className="mt-3 text-4xl font-semibold text-white">
                  {isLoading ? '—' : formatNumber(dashboard?.totalEvents ?? 0)}
                </p>
              </div>
              <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-sky-500/10 text-sky-300">
                <Sparkles className="h-6 w-6" />
              </div>
            </div>
            <p className="mt-4 text-sm text-slate-500">Events processed in the selected time window.</p>
          </motion.div>

          <motion.div
            layout
            initial={{ opacity: 0, y: 12 }}
            animate={{ opacity: 1, y: 0 }}
            className="rounded-3xl border border-slate-800/80 bg-slate-950/80 p-6 shadow-xl shadow-slate-950/20"
          >
            <div className="flex items-center justify-between gap-4">
              <div>
                <p className="text-sm text-slate-400">Success rate</p>
                <p className="mt-3 text-4xl font-semibold text-white">
                  {isLoading ? '—' : `${dashboard?.recentSuccessRate ?? 0}%`}
                </p>
              </div>
              <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-emerald-500/10 text-emerald-300">
                <ShieldCheck className="h-6 w-6" />
              </div>
            </div>
            <p className="mt-4 text-sm text-slate-500">Success score for the active window.</p>
          </motion.div>
        </div>

        <motion.div
          layout
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          className="rounded-3xl border border-slate-800/80 bg-slate-950/80 p-6 shadow-xl shadow-slate-950/20"
        >
          <div className="flex items-center justify-between gap-4">
            <div>
              <p className="text-sm text-slate-400">Event types</p>
              <p className="mt-3 text-4xl font-semibold text-white">
                {isLoading ? '—' : Object.keys(dashboard?.eventsByType ?? {}).length}
              </p>
            </div>
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-violet-500/10 text-violet-300">
              <Sparkles className="h-6 w-6" />
            </div>
          </div>
          <p className="mt-4 text-sm text-slate-500">Different event categories seen in the current range.</p>
        </motion.div>
      </div>

      <div className="grid gap-4 xl:grid-cols-[1.4fr_0.9fr]">
        <motion.div
          layout
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          className="rounded-3xl border border-slate-800/80 bg-slate-950/80 p-6 shadow-xl shadow-slate-950/20"
        >
          <div className="mb-4 flex items-center justify-between gap-4">
            <div>
              <p className="text-sm text-slate-400">Event distribution</p>
              <h2 className="text-xl font-semibold text-white">Top event types</h2>
            </div>
            <span className="rounded-full bg-slate-900/80 px-3 py-1 text-xs uppercase tracking-[0.24em] text-slate-400">
              Live
            </span>
          </div>

          <div className="h-[320px]">
            {isLoading ? (
              <div className="flex h-full items-center justify-center text-slate-500">Loading chart…</div>
            ) : chartData.length > 0 ? (
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={chartData} margin={{ top: 8, right: 8, bottom: 8, left: -12 }}>
                  <CartesianGrid stroke="#334155" strokeDasharray="3 3" vertical={false} />
                  <XAxis dataKey="name" tick={{ fill: '#94a3b8', fontSize: 12 }} axisLine={false} tickLine={false} interval={0} angle={-20} textAnchor="end" height={80} />
                  <YAxis tick={{ fill: '#94a3b8', fontSize: 12 }} axisLine={false} tickLine={false} />
                  <Tooltip contentStyle={{ backgroundColor: '#020617', borderColor: '#334155' }} labelStyle={{ color: '#fff' }} itemStyle={{ color: '#fff' }} />
                  <Bar dataKey="count" radius={[12, 12, 0, 0]}>
                    {chartData.map((entry) => (
                      <Cell key={entry.name} fill={eventColor(entry.name)} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            ) : (
              <div className="flex h-full items-center justify-center text-slate-500">No chart data available.</div>
            )}
          </div>
        </motion.div>

        <motion.div
          layout
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          className="rounded-3xl border border-slate-800/80 bg-slate-950/80 p-6 shadow-xl shadow-slate-950/20"
        >
          <div className="mb-4 flex items-center justify-between gap-4">
            <div>
              <p className="text-sm text-slate-400">Event breakdown</p>
              <h2 className="text-xl font-semibold text-white">Type composition</h2>
            </div>
          </div>

          <div className="space-y-3">
            {isLoading ? (
              <div className="space-y-3">
                {[1, 2, 3].map((item) => (
                  <div key={item} className="h-16 rounded-3xl bg-slate-900/70" />
                ))}
              </div>
            ) : eventTypes.length > 0 ? (
              eventTypes.map((item) => {
                const ratio = dashboard?.totalEvents ? (item.value / dashboard.totalEvents) * 100 : 0;
                return (
                  <div key={item.eventType} className="rounded-3xl border border-slate-800/80 bg-slate-900/70 p-4">
                    <div className="flex items-center justify-between gap-4">
                      <div>
                        <p className="text-sm font-medium text-slate-100">{item.eventType}</p>
                        <p className="text-xs text-slate-500">{formatNumber(item.value)} events</p>
                      </div>
                      <span className="text-sm font-semibold text-slate-200">{ratio.toFixed(1)}%</span>
                    </div>
                    <div className="mt-4 h-2 overflow-hidden rounded-full bg-slate-800">
                      <div className="h-2 rounded-full bg-sky-400" style={{ width: `${Math.max(2, ratio)}%` }} />
                    </div>
                  </div>
                );
              })
            ) : (
              <p className="text-sm text-slate-500">No event type data available.</p>
            )}
          </div>
        </motion.div>
      </div>

      <motion.div
        layout
        initial={{ opacity: 0, y: 12 }}
        animate={{ opacity: 1, y: 0 }}
        className="rounded-3xl border border-slate-800/80 bg-slate-950/80 p-6 shadow-xl shadow-slate-950/20"
      >
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="text-sm text-slate-400">Live status</p>
            <p className="mt-1 text-sm text-slate-300">Automatic refresh every 5 seconds for backend metrics.</p>
          </div>
          <span className="rounded-full bg-slate-900/80 px-3 py-1 text-xs uppercase tracking-[0.24em] text-slate-400">
            {isLoading ? 'Updating' : 'Synced'}
          </span>
        </div>
        <div className="mt-4 flex flex-wrap gap-3 text-sm text-slate-400">
          <span className="rounded-2xl bg-slate-900/80 px-3 py-2">Total events: {isLoading ? '—' : formatNumber(dashboard?.totalEvents ?? 0)}</span>
          <span className="rounded-2xl bg-slate-900/80 px-3 py-2">Success rate: {isLoading ? '—' : `${dashboard?.recentSuccessRate ?? 0}%`}</span>
          <span className="rounded-2xl bg-slate-900/80 px-3 py-2">Types: {isLoading ? '—' : Object.keys(dashboard?.eventsByType ?? {}).length}</span>
        </div>
      </motion.div>
    </section>
  );
}
