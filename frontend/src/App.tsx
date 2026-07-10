import { Dashboard } from './components/Dashboard';
import { EventLogger } from './components/EventLogger';
import { RealtimePanel } from './components/RealtimePanel';
import './index.css';

function App() {
  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        <div className="space-y-8">
          <Dashboard />
          <div className="grid gap-6 xl:grid-cols-[1.4fr_0.9fr]">
            <EventLogger />
            <RealtimePanel />
          </div>
        </div>
      </main>
    </div>
  );
}

export default App;
