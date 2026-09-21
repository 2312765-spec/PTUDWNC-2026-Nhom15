import React, { useState } from 'react';
import { useApp } from '../context/AppContext';
import { X, Activity, Database, Server, RefreshCw, CheckCircle2, Layers, Cpu } from 'lucide-react';

export default function HealthCheckModal() {
  const { healthModalOpen, setHealthModalOpen, systemHealth } = useApp();
  const [activeTab, setActiveTab] = useState<'summary' | 'live' | 'ready'>('summary');
  const [isRefreshing, setIsRefreshing] = useState(false);

  if (!healthModalOpen) return null;

  const handleRefresh = () => {
    setIsRefreshing(true);
    setTimeout(() => setIsRefreshing(false), 600);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in">
      <div 
        className="w-full max-w-xl bg-white rounded-2xl shadow-2xl border border-blue-100 overflow-hidden flex flex-col max-h-[85vh] animate-in zoom-in-95"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-slate-100 bg-blue-50/60">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-lg bg-blue-600 text-white shadow-xs">
              <Activity className="w-4 h-4" />
            </div>
            <div>
              <h3 className="text-sm font-bold text-slate-900">
                Hệ Thống Quan Sát & Health Checks (FR-OBS-001)
              </h3>
              <p className="text-xs text-blue-700">
                Mô phỏng ASP.NET Core Health Checks Probe (/health)
              </p>
            </div>
          </div>
          <div className="flex items-center gap-1.5">
            <button
              onClick={handleRefresh}
              className={`p-1.5 rounded-lg text-slate-500 hover:text-blue-600 hover:bg-slate-100 transition cursor-pointer ${
                isRefreshing ? 'animate-spin text-blue-600' : ''
              }`}
              title="Làm mới trạng thái"
            >
              <RefreshCw className="w-4 h-4" />
            </button>
            <button
              onClick={() => setHealthModalOpen(false)}
              className="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition cursor-pointer"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Tab Selection */}
        <div className="flex border-b border-slate-200 bg-slate-50 px-5 pt-2 gap-2 text-xs font-semibold">
          <button
            onClick={() => setActiveTab('summary')}
            className={`pb-2 px-2.5 border-b-2 transition cursor-pointer ${
              activeTab === 'summary'
                ? 'border-blue-600 text-blue-700'
                : 'border-transparent text-slate-500 hover:text-slate-800'
            }`}
          >
            GET /health (Tổng quan)
          </button>
          <button
            onClick={() => setActiveTab('ready')}
            className={`pb-2 px-2.5 border-b-2 transition cursor-pointer ${
              activeTab === 'ready'
                ? 'border-blue-600 text-blue-700'
                : 'border-transparent text-slate-500 hover:text-slate-800'
            }`}
          >
            GET /health/ready (Readiness)
          </button>
          <button
            onClick={() => setActiveTab('live')}
            className={`pb-2 px-2.5 border-b-2 transition cursor-pointer ${
              activeTab === 'live'
                ? 'border-blue-600 text-blue-700'
                : 'border-transparent text-slate-500 hover:text-slate-800'
            }`}
          >
            GET /health/live (Liveness)
          </button>
        </div>

        {/* Body */}
        <div className="p-5 space-y-4 overflow-y-auto">
          {/* Main Status Badge */}
          <div className="flex items-center justify-between p-3.5 bg-emerald-50 rounded-xl border border-emerald-200">
            <div className="flex items-center gap-3">
              <CheckCircle2 className="w-6 h-6 text-emerald-600" />
              <div>
                <p className="text-xs font-bold text-emerald-900 uppercase tracking-wider">
                  Trạng thái hệ thống: {systemHealth.status}
                </p>
                <p className="text-[11px] text-emerald-700">
                  Tất cả các dịch vụ nền tảng đang phản hồi tốt
                </p>
              </div>
            </div>
            <span className="font-mono text-[11px] text-emerald-800 bg-white px-2.5 py-1 rounded-md border border-emerald-300">
              HTTP 200 OK
            </span>
          </div>

          {/* Component breakdowns */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div className="p-3 bg-slate-50 rounded-xl border border-slate-200/80">
              <div className="flex items-center justify-between mb-1.5">
                <span className="text-xs font-bold text-slate-800 flex items-center gap-1.5">
                  <Database className="w-3.5 h-3.5 text-blue-600" />
                  PostgreSQL 16
                </span>
                <span className="text-[10px] font-bold text-emerald-600 bg-emerald-100 px-1.5 py-0.5 rounded">
                  {systemHealth.database.status}
                </span>
              </div>
              <p className="text-[11px] text-slate-500">
                EF Core Code First + Full-Text Search tsvector
              </p>
              <div className="mt-2 text-[11px] text-slate-700 font-mono">
                Độ trễ: <span className="font-bold text-blue-700">{systemHealth.database.latencyMs}ms</span>
              </div>
            </div>

            <div className="p-3 bg-slate-50 rounded-xl border border-slate-200/80">
              <div className="flex items-center justify-between mb-1.5">
                <span className="text-xs font-bold text-slate-800 flex items-center gap-1.5">
                  <Server className="w-3.5 h-3.5 text-red-500" />
                  Redis 7 Cache
                </span>
                <span className="text-[10px] font-bold text-emerald-600 bg-emerald-100 px-1.5 py-0.5 rounded">
                  {systemHealth.redis.status}
                </span>
              </div>
              <p className="text-[11px] text-slate-500">
                Distributed Cache & Rate Limit Counter
              </p>
              <div className="mt-2 text-[11px] text-slate-700 font-mono">
                Độ trễ: <span className="font-bold text-blue-700">{systemHealth.redis.latencyMs}ms</span>
              </div>
            </div>

            <div className="p-3 bg-slate-50 rounded-xl border border-slate-200/80">
              <div className="flex items-center justify-between mb-1.5">
                <span className="text-xs font-bold text-slate-800 flex items-center gap-1.5">
                  <Layers className="w-3.5 h-3.5 text-pink-600" />
                  MinIO (S3) Storage
                </span>
                <span className="text-[10px] font-bold text-emerald-600 bg-emerald-100 px-1.5 py-0.5 rounded">
                  {systemHealth.minio.status}
                </span>
              </div>
              <p className="text-[11px] text-slate-500">
                Bucket "culinary-blog" (Recipe Images)
              </p>
              <div className="mt-2 text-[11px] text-slate-700 font-mono">
                Độ trễ: <span className="font-bold text-blue-700">{systemHealth.minio.latencyMs}ms</span>
              </div>
            </div>

            <div className="p-3 bg-slate-50 rounded-xl border border-slate-200/80">
              <div className="flex items-center justify-between mb-1.5">
                <span className="text-xs font-bold text-slate-800 flex items-center gap-1.5">
                  <Cpu className="w-3.5 h-3.5 text-purple-600" />
                  Hangfire Background Jobs
                </span>
                <span className="text-[10px] font-bold text-emerald-600 bg-emerald-100 px-1.5 py-0.5 rounded">
                  {systemHealth.hangfire.status}
                </span>
              </div>
              <p className="text-[11px] text-slate-500">
                Email chào mừng, thumbnails, sitemap
              </p>
              <div className="mt-2 text-[11px] text-slate-700 font-mono">
                Workers: <span className="font-bold text-blue-700">{systemHealth.hangfire.activeWorkers}</span> • Hàng đợi: 0
              </div>
            </div>
          </div>

          {/* JSON raw output preview */}
          <div className="bg-slate-900 text-slate-300 p-3 rounded-xl font-mono text-[11px]">
            <div className="flex items-center justify-between text-slate-400 mb-1 pb-1 border-b border-slate-800 text-[10px]">
              <span>Phản hồi HTTP RFC 7807</span>
              <span>Content-Type: application/json</span>
            </div>
            <pre className="text-emerald-400 overflow-x-auto">
{JSON.stringify({
  status: systemHealth.status,
  totalDuration: "00:00:00.038",
  entries: {
    npgsql: { status: systemHealth.database.status, duration: `${systemHealth.database.latencyMs}ms` },
    redis: { status: systemHealth.redis.status, duration: `${systemHealth.redis.latencyMs}ms` },
    minio: { status: systemHealth.minio.status, duration: `${systemHealth.minio.latencyMs}ms` },
    hangfire: { status: systemHealth.hangfire.status, activeWorkers: systemHealth.hangfire.activeWorkers }
  }
}, null, 2)}
            </pre>
          </div>
        </div>

        {/* Footer */}
        <div className="p-4 border-t border-slate-100 bg-slate-50 flex items-center justify-between">
          <span className="text-xs text-slate-500">
            Chuẩn NFR-OBS-001 (Liveness / Readiness Probes cho K8s & Nginx)
          </span>
          <button
            onClick={() => setHealthModalOpen(false)}
            className="px-4 py-1.5 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-xs font-semibold cursor-pointer"
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
}
