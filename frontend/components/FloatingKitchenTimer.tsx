import React from 'react';
import { useApp } from '../context/AppContext';
import { Clock, Pause, Play, Square, BellRing } from 'lucide-react';

export default function FloatingKitchenTimer() {
  const { activeTimer, toggleTimerPause, cancelTimer } = useApp();

  if (!activeTimer) return null;

  const minutes = Math.floor(activeTimer.remainingSeconds / 60);
  const seconds = activeTimer.remainingSeconds % 60;
  const formatted = `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
  const progress = ((activeTimer.totalSeconds - activeTimer.remainingSeconds) / activeTimer.totalSeconds) * 100;

  return (
    <div className="fixed bottom-6 right-6 z-40 bg-slate-900 text-white rounded-2xl shadow-2xl border border-slate-700 p-4 w-72 sm:w-80 animate-in slide-in-from-bottom-5 duration-300">
      <div className="flex items-start justify-between gap-2 mb-2">
        <div className="flex items-center gap-2 min-w-0">
          <div className="w-8 h-8 rounded-xl bg-amber-500/20 text-amber-400 flex items-center justify-center shrink-0">
            {activeTimer.remainingSeconds === 0 ? (
              <BellRing className="w-4 h-4 animate-bounce" />
            ) : (
              <Clock className="w-4 h-4" />
            )}
          </div>
          <div className="min-w-0">
            <p className="text-[10px] uppercase font-bold text-amber-400">
              Đồng hồ nhà bếp
            </p>
            <p className="text-xs font-bold text-slate-100 truncate">
              Bước {activeTimer.stepNumber}: {activeTimer.recipeTitle}
            </p>
          </div>
        </div>

        <button
          onClick={cancelTimer}
          className="p-1 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition cursor-pointer"
          title="Tắt đồng hồ"
        >
          <Square className="w-3.5 h-3.5" />
        </button>
      </div>

      <div className="flex items-center justify-between my-2">
        <span className="text-2xl sm:text-3xl font-mono font-extrabold tracking-wider text-amber-400">
          {formatted}
        </span>
        <div className="flex items-center gap-1.5">
          <button
            onClick={toggleTimerPause}
            className="p-2 rounded-xl bg-amber-500 hover:bg-amber-400 text-slate-950 font-bold transition shadow-xs cursor-pointer"
            title={activeTimer.isPaused ? 'Tiếp tục' : 'Tạm dừng'}
          >
            {activeTimer.isPaused ? (
              <Play className="w-4 h-4 fill-slate-950" />
            ) : (
              <Pause className="w-4 h-4 fill-slate-950" />
            )}
          </button>
        </div>
      </div>

      {/* Progress Bar */}
      <div className="w-full bg-slate-800 h-1.5 rounded-full overflow-hidden">
        <div
          className="bg-gradient-to-r from-amber-500 to-amber-400 h-full transition-all duration-1000 rounded-full"
          style={{ width: `${progress}%` }}
        ></div>
      </div>
    </div>
  );
}
