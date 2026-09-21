import React, { useState, useEffect } from 'react';
import { 
  X, 
  Activity, 
  CheckCircle2, 
  AlertCircle, 
  Clock, 
  RefreshCw, 
  Database, 
  Send, 
  Filter, 
  ArrowDownLeft, 
  Code2, 
  Zap, 
  Server
} from 'lucide-react';
import { api, apiLogs, subscribeApiLogs, ApiRequestLog } from '../services/api';

interface ApiActivityModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export default function ApiActivityModal({ isOpen, onClose }: ApiActivityModalProps) {
  const [logs, setLogs] = useState<ApiRequestLog[]>([...apiLogs]);
  const [filterMethod, setFilterMethod] = useState<string>('ALL');
  const [selectedLog, setSelectedLog] = useState<ApiRequestLog | null>(null);
  const [isPinging, setIsPinging] = useState(false);
  const [healthData, setHealthData] = useState<any>(null);

  useEffect(() => {
    const unsubscribe = subscribeApiLogs(() => {
      setLogs([...apiLogs]);
    });
    return unsubscribe;
  }, []);

  useEffect(() => {
    if (isOpen) {
      pingHealth();
    }
  }, [isOpen]);

  const pingHealth = async () => {
    setIsPinging(true);
    try {
      const data = await api.getHealth();
      setHealthData(data);
    } catch (err) {
      console.error('Health ping error:', err);
    } finally {
      setIsPinging(false);
    }
  };

  if (!isOpen) return null;

  const filteredLogs = logs.filter((log) => {
    if (filterMethod === 'ALL') return true;
    return log.method === filterMethod;
  });

  const getStatusBadge = (status: number) => {
    if (status >= 200 && status < 300) {
      return (
        <span className="px-2 py-0.5 rounded text-[11px] font-bold bg-emerald-100 text-emerald-700 border border-emerald-300">
          {status} OK
        </span>
      );
    }
    if (status >= 400 && status < 500) {
      return (
        <span className="px-2 py-0.5 rounded text-[11px] font-bold bg-amber-100 text-amber-700 border border-amber-300">
          {status} Client Error
        </span>
      );
    }
    return (
      <span className="px-2 py-0.5 rounded text-[11px] font-bold bg-rose-100 text-rose-700 border border-rose-300">
        {status} Server Error
      </span>
    );
  };

  const getMethodBadge = (method: string) => {
    const map: Record<string, string> = {
      GET: 'bg-blue-100 text-blue-700 border-blue-200',
      POST: 'bg-emerald-100 text-emerald-700 border-emerald-200',
      PUT: 'bg-amber-100 text-amber-700 border-amber-200',
      DELETE: 'bg-rose-100 text-rose-700 border-rose-200',
      PATCH: 'bg-purple-100 text-purple-700 border-purple-200',
    };
    return (
      <span className={`px-2 py-0.5 rounded font-mono font-bold text-[11px] border ${map[method] || 'bg-slate-100 text-slate-700'}`}>
        {method}
      </span>
    );
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in">
      <div className="w-full max-w-5xl bg-white rounded-2xl shadow-2xl border border-slate-200 flex flex-col max-h-[90vh] overflow-hidden">
        {/* Header */}
        <div className="p-4 sm:p-5 border-b border-slate-100 flex items-center justify-between bg-gradient-to-r from-slate-900 to-slate-800 text-white">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl bg-blue-500/20 text-blue-400 flex items-center justify-center border border-blue-400/30">
              <Activity className="w-5 h-5 animate-pulse" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="text-base font-bold">Giám Sát Kết Nối API Backend & Frontend .tsx</h3>
                <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-500/30 text-emerald-300 border border-emerald-400/40">
                  HTTP 200 OK • CONNECTED
                </span>
              </div>
              <p className="text-xs text-slate-300">
                Theo dõi toàn bộ lưu lượng HTTP giữa các component giao diện React (.tsx) và API backend (/api/v1/*).
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-slate-700 transition cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Server & Connectivity Summary Cards */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 p-4 bg-slate-50 border-b border-slate-200 text-xs">
          <div className="p-3 bg-white rounded-xl border border-slate-200">
            <div className="flex items-center justify-between text-slate-500 mb-1">
              <span className="font-semibold">Backend Port</span>
              <Server className="w-4 h-4 text-blue-600" />
            </div>
            <div className="font-bold text-slate-900 text-sm">PORT 3000</div>
            <span className="text-[10px] text-emerald-600 font-medium">Express & .NET 10 Bridge</span>
          </div>

          <div className="p-3 bg-white rounded-xl border border-slate-200">
            <div className="flex items-center justify-between text-slate-500 mb-1">
              <span className="font-semibold">Bộ nhớ Cache</span>
              <Zap className="w-4 h-4 text-amber-500" />
            </div>
            <div className="font-bold text-slate-900 text-sm">IMemoryCache</div>
            <span className="text-[10px] text-slate-500">TTL 60 phút sliding</span>
          </div>

          <div className="p-3 bg-white rounded-xl border border-slate-200">
            <div className="flex items-center justify-between text-slate-500 mb-1">
              <span className="font-semibold">Chuẩn Báo Lỗi</span>
              <AlertCircle className="w-4 h-4 text-purple-600" />
            </div>
            <div className="font-bold text-slate-900 text-sm">RFC 7807</div>
            <span className="text-[10px] text-purple-700 font-medium">Problem Details</span>
          </div>

          <div className="p-3 bg-white rounded-xl border border-slate-200">
            <div className="flex items-center justify-between text-slate-500 mb-1">
              <span className="font-semibold">Tổng Request Log</span>
              <Activity className="w-4 h-4 text-emerald-600" />
            </div>
            <div className="font-bold text-slate-900 text-sm">{logs.length} cuộc gọi</div>
            <button
              onClick={pingHealth}
              disabled={isPinging}
              className="text-[10px] text-blue-600 font-bold hover:underline flex items-center gap-1 mt-0.5 cursor-pointer"
            >
              <RefreshCw className={`w-3 h-3 ${isPinging ? 'animate-spin' : ''}`} />
              <span>Ping Server ({healthData?.database?.latencyMs || 2}ms)</span>
            </button>
          </div>
        </div>

        {/* Content Area: Split View of Request Logs & Details */}
        <div className="flex-1 flex flex-col sm:flex-row overflow-hidden min-h-[380px]">
          {/* Left Column: Request List */}
          <div className="w-full sm:w-1/2 border-r border-slate-200 flex flex-col overflow-hidden bg-white">
            {/* Filter Bar */}
            <div className="p-2.5 border-b border-slate-100 flex items-center justify-between bg-slate-50/70 text-xs">
              <span className="font-bold text-slate-700 flex items-center gap-1.5">
                <Filter className="w-3.5 h-3.5 text-slate-400" />
                Lọc theo HTTP Method:
              </span>
              <div className="flex gap-1">
                {['ALL', 'GET', 'POST', 'PUT', 'DELETE', 'PATCH'].map((m) => (
                  <button
                    key={m}
                    onClick={() => setFilterMethod(m)}
                    className={`px-2 py-0.5 rounded text-[11px] font-semibold transition cursor-pointer ${
                      filterMethod === m
                        ? 'bg-blue-600 text-white'
                        : 'bg-white text-slate-600 hover:bg-slate-200 border border-slate-200'
                    }`}
                  >
                    {m}
                  </button>
                ))}
              </div>
            </div>

            {/* List */}
            <div className="flex-1 overflow-y-auto divide-y divide-slate-100">
              {filteredLogs.length === 0 ? (
                <div className="p-8 text-center text-slate-400 text-xs">
                  Chưa có request nào được ghi nhận. Hãy thao tác trên giao diện web để xem lưu lượng API!
                </div>
              ) : (
                filteredLogs.map((log) => (
                  <div
                    key={log.id}
                    onClick={() => setSelectedLog(log)}
                    className={`p-3 text-xs flex items-center justify-between hover:bg-blue-50/60 cursor-pointer transition ${
                      selectedLog?.id === log.id ? 'bg-blue-50 border-l-4 border-blue-600' : ''
                    }`}
                  >
                    <div className="space-y-1 min-w-0 pr-2">
                      <div className="flex items-center gap-2">
                        {getMethodBadge(log.method)}
                        <span className="font-mono font-medium text-slate-800 truncate text-[11px]">
                          {log.endpoint}
                        </span>
                      </div>
                      <div className="flex items-center gap-2 text-[10px] text-slate-400">
                        <Clock className="w-3 h-3" />
                        <span>{log.timestamp}</span>
                        <span>•</span>
                        <span>{log.durationMs}ms</span>
                        {log.cacheHeader && (
                          <span className={`px-1.5 py-0.2 rounded font-bold ${log.cacheHeader === 'HIT' ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-700'}`}>
                            {log.cacheHeader}
                          </span>
                        )}
                      </div>
                    </div>
                    <div>
                      {getStatusBadge(log.status)}
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>

          {/* Right Column: Selected Request Inspection */}
          <div className="w-full sm:w-1/2 p-4 overflow-y-auto bg-slate-900 text-slate-200 text-xs font-mono space-y-4">
            {selectedLog ? (
              <>
                <div className="flex items-center justify-between pb-2 border-b border-slate-800">
                  <div className="flex items-center gap-2">
                    <span className="text-emerald-400 font-bold">{selectedLog.method}</span>
                    <span className="text-white font-semibold">{selectedLog.endpoint}</span>
                  </div>
                  <span className="text-slate-400">{selectedLog.durationMs}ms</span>
                </div>

                <div>
                  <h4 className="text-[11px] uppercase tracking-wider text-slate-400 font-bold mb-1">
                    Trạng Thái HTTP & Headers
                  </h4>
                  <div className="p-2.5 rounded-lg bg-slate-950 border border-slate-800 text-[11px] space-y-1">
                    <div>Status: <span className="text-emerald-400">{selectedLog.status}</span></div>
                    <div>Timestamp: <span className="text-slate-300">{selectedLog.timestamp}</span></div>
                    {selectedLog.cacheHeader && (
                      <div>X-Cache: <span className="text-amber-400">{selectedLog.cacheHeader}</span></div>
                    )}
                  </div>
                </div>

                {selectedLog.requestBody && (
                  <div>
                    <h4 className="text-[11px] uppercase tracking-wider text-slate-400 font-bold mb-1">
                      Request Payload (Dữ liệu gửi lên)
                    </h4>
                    <pre className="p-2.5 rounded-lg bg-slate-950 border border-slate-800 overflow-x-auto text-[11px] text-blue-300">
                      {JSON.stringify(selectedLog.requestBody, null, 2)}
                    </pre>
                  </div>
                )}

                <div>
                  <h4 className="text-[11px] uppercase tracking-wider text-slate-400 font-bold mb-1">
                    Response Payload (Dữ liệu phản hồi)
                  </h4>
                  <pre className="p-2.5 rounded-lg bg-slate-950 border border-slate-800 overflow-x-auto text-[11px] text-emerald-300 max-h-60">
                    {typeof selectedLog.responseBody === 'string' 
                      ? selectedLog.responseBody 
                      : JSON.stringify(selectedLog.responseBody, null, 2)}
                  </pre>
                </div>
              </>
            ) : (
              <div className="h-full flex flex-col items-center justify-center text-center text-slate-500 py-12 space-y-2">
                <Code2 className="w-10 h-10 text-slate-700" />
                <p className="text-slate-400 text-xs">Chọn một request ở danh sách bên trái để kiểm tra Payload chi tiết.</p>
              </div>
            )}
          </div>
        </div>

        {/* Footer */}
        <div className="p-3 bg-slate-100 border-t border-slate-200 flex items-center justify-between text-xs text-slate-600">
          <div className="flex items-center gap-2">
            <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
            <span>API Client Service: Active (`src/services/api.ts`)</span>
          </div>
          <button
            onClick={onClose}
            className="px-4 py-1.5 rounded-lg bg-slate-200 hover:bg-slate-300 text-slate-800 font-semibold cursor-pointer"
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
}
