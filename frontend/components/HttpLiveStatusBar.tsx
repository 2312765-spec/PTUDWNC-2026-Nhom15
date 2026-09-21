import React, { useState, useEffect } from 'react';
import { 
  Activity, 
  CheckCircle2, 
  AlertCircle, 
  Clock, 
  ChevronUp, 
  ChevronDown, 
  X, 
  Copy, 
  Check, 
  Zap 
} from 'lucide-react';
import { apiLogs, subscribeApiLogs, ApiRequestLog } from '../services/api';

export default function HttpLiveStatusBar() {
  const [latestLog, setLatestLog] = useState<ApiRequestLog | null>(apiLogs[0] || null);
  const [expanded, setExpanded] = useState<boolean>(false);
  const [copied, setCopied] = useState<boolean>(false);
  const [minimized, setMinimized] = useState<boolean>(false);

  useEffect(() => {
    const unsubscribe = subscribeApiLogs((newLog) => {
      setLatestLog(newLog);
    });
    return unsubscribe;
  }, []);

  if (!latestLog || minimized) {
    return (
      <button
        onClick={() => setMinimized(false)}
        className="fixed bottom-4 right-4 z-50 flex items-center gap-2 px-3 py-2 bg-slate-900/90 text-white rounded-full shadow-lg border border-slate-700 text-xs font-mono backdrop-blur-md hover:bg-slate-800 transition cursor-pointer"
        title="Mở thanh theo dõi mã phản hồi HTTP"
      >
        <span className="w-2 h-2 rounded-full bg-emerald-400 animate-ping" />
        <Activity className="w-3.5 h-3.5 text-blue-400" />
        <span>HTTP Inspector</span>
      </button>
    );
  }

  const getStatusColor = (status: number) => {
    if (status >= 200 && status < 300) {
      return {
        badge: 'bg-emerald-500 text-white font-bold',
        text: 'text-emerald-400',
        icon: CheckCircle2,
      };
    }
    if (status >= 400 && status < 500) {
      return {
        badge: 'bg-amber-500 text-white font-bold',
        text: 'text-amber-400',
        icon: AlertCircle,
      };
    }
    return {
      badge: 'bg-rose-500 text-white font-bold',
      text: 'text-rose-400',
      icon: AlertCircle,
    };
  };

  const getStatusText = (status: number) => {
    switch (status) {
      case 200: return '200 OK';
      case 201: return '201 Created';
      case 204: return '204 No Content';
      case 304: return '304 Not Modified';
      case 400: return '400 Bad Request';
      case 401: return '401 Unauthorized';
      case 403: return '403 Forbidden';
      case 404: return '404 Not Found';
      case 409: return '409 Conflict';
      case 422: return '422 Unprocessable Entity';
      case 500: return '500 Internal Server Error';
      default: return `${status}`;
    }
  };

  const color = getStatusColor(latestLog.status);

  const copyJson = () => {
    navigator.clipboard.writeText(JSON.stringify(latestLog, null, 2));
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };

  return (
    <div className="fixed bottom-4 right-4 z-50 max-w-md w-[calc(100vw-2rem)] sm:w-[460px] transition-all duration-300">
      <div className="bg-slate-950/95 backdrop-blur-xl border border-slate-800 rounded-2xl shadow-2xl text-white overflow-hidden font-sans">
        {/* Top Header Bar */}
        <div className="px-3.5 py-2.5 bg-slate-900/90 border-b border-slate-800/80 flex items-center justify-between gap-2">
          <div className="flex items-center gap-2 min-w-0">
            <span className="flex h-2 w-2 relative">
              <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
              <span className="relative inline-flex rounded-full h-2 w-2 bg-emerald-500"></span>
            </span>
            <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400 flex items-center gap-1">
              <Zap className="w-3 h-3 text-amber-400" />
              Mã HTTP Phản Hồi:
            </span>
            <span className={`px-2 py-0.5 rounded-md text-xs font-mono font-black ${color.badge} shadow-xs`}>
              {getStatusText(latestLog.status)}
            </span>
          </div>

          <div className="flex items-center gap-1 text-slate-400">
            <button
              onClick={() => setExpanded(!expanded)}
              className="p-1 rounded-md hover:bg-slate-800 hover:text-white transition cursor-pointer"
              title={expanded ? "Thu gọn chi tiết" : "Mở rộng chi tiết JSON response"}
            >
              {expanded ? <ChevronDown className="w-3.5 h-3.5" /> : <ChevronUp className="w-3.5 h-3.5" />}
            </button>
            <button
              onClick={() => setMinimized(true)}
              className="p-1 rounded-md hover:bg-slate-800 hover:text-white transition cursor-pointer"
              title="Ẩn thanh trạng thái"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          </div>
        </div>

        {/* Quick Summary Line */}
        <div className="px-3.5 py-2 flex items-center justify-between text-xs gap-3">
          <div className="flex items-center gap-2 truncate font-mono">
            <span className={`px-1.5 py-0.5 rounded text-[10px] font-extrabold uppercase ${
              latestLog.method === 'POST' ? 'bg-emerald-950 text-emerald-300 border border-emerald-800' :
              latestLog.method === 'GET' ? 'bg-blue-950 text-blue-300 border border-blue-800' :
              latestLog.method === 'PUT' ? 'bg-amber-950 text-amber-300 border border-amber-800' :
              'bg-rose-950 text-rose-300 border border-rose-800'
            }`}>
              {latestLog.method}
            </span>
            <span className="text-slate-300 truncate text-[11px]" title={latestLog.endpoint}>
              {latestLog.endpoint}
            </span>
          </div>

          <div className="flex items-center gap-2 text-[11px] text-slate-400 shrink-0 font-mono">
            {latestLog.cacheHeader && (
              <span className="px-1.5 py-0.5 rounded bg-blue-900/60 text-blue-300 text-[10px] border border-blue-700/50">
                {latestLog.cacheHeader}
              </span>
            )}
            <span className="flex items-center gap-1 text-slate-400">
              <Clock className="w-3 h-3 text-slate-500" />
              {latestLog.durationMs}ms
            </span>
          </div>
        </div>

        {/* Expanded JSON Inspector */}
        {expanded && (
          <div className="border-t border-slate-800/90 p-3.5 space-y-3 bg-slate-950/80 text-xs animate-in fade-in slide-in-from-bottom-2 duration-200">
            <div className="space-y-1.5">
              <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400 flex items-center justify-between">
                <span>Headers phản hồi</span>
                <span className="font-mono text-slate-500">{latestLog.timestamp}</span>
              </div>
              <div className="bg-slate-900/90 rounded-xl p-2.5 font-mono text-[11px] text-slate-300 space-y-1 border border-slate-800">
                <div><span className="text-blue-400">Status:</span> {latestLog.status} {getStatusText(latestLog.status).split(' ').slice(1).join(' ')}</div>
                <div><span className="text-blue-400">Server:</span> Kestrel (ASP.NET Core .NET 10 Clean Architecture)</div>
                <div><span className="text-blue-400">Content-Type:</span> application/json; charset=utf-8</div>
                {latestLog.status === 201 && (
                  <div className="text-emerald-300 font-semibold">
                    <span className="text-blue-400">Location:</span> {
                      latestLog.responseBody?.slug ? `/api/v1/recipes/${latestLog.responseBody.slug}` :
                      latestLog.endpoint
                    }
                  </div>
                )}
                {latestLog.cacheHeader && (
                  <div><span className="text-blue-400">X-Cache:</span> {latestLog.cacheHeader}</div>
                )}
              </div>
            </div>

            <div className="space-y-1.5">
              <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400 flex items-center justify-between">
                <span>Dữ liệu trả về (Response Body)</span>
                <button
                  onClick={copyJson}
                  className="flex items-center gap-1 text-[10px] text-blue-400 hover:text-blue-300 transition cursor-pointer"
                >
                  {copied ? <Check className="w-3 h-3 text-emerald-400" /> : <Copy className="w-3 h-3" />}
                  <span>{copied ? 'Đã chép' : 'Sao chép JSON'}</span>
                </button>
              </div>

              <pre className="bg-slate-900/95 border border-slate-800 rounded-xl p-3 max-h-48 overflow-y-auto text-[10px] font-mono text-emerald-300 leading-relaxed scrollbar-thin scrollbar-thumb-slate-700">
                {JSON.stringify(latestLog.responseBody, null, 2)}
              </pre>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}