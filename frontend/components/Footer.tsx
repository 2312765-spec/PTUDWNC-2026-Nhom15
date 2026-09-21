import React from 'react';
import { ChefHat, Heart, Activity, Code2, Database, ShieldCheck, ArrowUpRight } from 'lucide-react';
import { useApp } from '../context/AppContext';

export default function Footer() {
  const { navigate, setHealthModalOpen } = useApp();

  return (
    <footer className="bg-slate-900 text-slate-300 pt-14 pb-8 border-t border-slate-800">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-8 mb-12">
          {/* Brand Col */}
          <div className="space-y-4 md:col-span-1">
            <div className="flex items-center gap-2.5">
              <div className="w-9 h-9 rounded-xl bg-blue-600 flex items-center justify-center text-white shadow-md shadow-blue-500/20">
                <ChefHat className="w-5 h-5" />
              </div>
              <span className="text-xl font-bold tracking-tight text-white">
                Culinary<span className="text-blue-400">Blog</span>
              </span>
            </div>
            <p className="text-xs text-slate-400 leading-relaxed">
              Dự án tham chiếu chuẩn IEEE 830 / ISO/IEC/IEEE 29148:2018. Hệ sinh thái chia sẻ công thức nấu ăn chuyên nghiệp xây dựng trên Next.js App Router và .NET 10 Minimal APIs.
            </p>
            <div className="flex items-center gap-2 pt-2">
              <span className="inline-flex items-center gap-1 px-2 py-1 rounded bg-slate-800 text-[11px] text-blue-300 border border-slate-700">
                <Database className="w-3 h-3 text-blue-400" />
                PostgreSQL 16 FTS
              </span>
              <span className="inline-flex items-center gap-1 px-2 py-1 rounded bg-slate-800 text-[11px] text-sky-300 border border-slate-700">
                <Code2 className="w-3 h-3 text-sky-400" />
                Clean Arch CQRS
              </span>
            </div>
          </div>

          {/* Quick Nav Col */}
          <div>
            <h4 className="text-sm font-semibold text-white uppercase tracking-wider mb-4 border-l-2 border-blue-500 pl-2">
              Điều hướng
            </h4>
            <ul className="space-y-2.5 text-xs text-slate-400">
              <li>
                <button onClick={() => navigate('/')} className="hover:text-blue-400 transition cursor-pointer">
                  Trang chủ
                </button>
              </li>
              <li>
                <button onClick={() => navigate('/recipes')} className="hover:text-blue-400 transition cursor-pointer">
                  Tất cả công thức ẩm thực
                </button>
              </li>
              <li>
                <button onClick={() => navigate('/categories')} className="hover:text-blue-400 transition cursor-pointer">
                  Danh mục món ăn
                </button>
              </li>
              <li>
                <button onClick={() => navigate('/dashboard')} className="hover:text-blue-400 transition cursor-pointer">
                  Bảng quản lý (Dashboard)
                </button>
              </li>
            </ul>
          </div>

          {/* Architecture specs */}
          <div>
            <h4 className="text-sm font-semibold text-white uppercase tracking-wider mb-4 border-l-2 border-blue-500 pl-2">
              Đặc tả Kỹ thuật (SRS)
            </h4>
            <ul className="space-y-2 text-xs text-slate-400">
              <li className="flex items-center justify-between">
                <span>Frontend:</span>
                <span className="font-mono text-blue-300">Next.js App Router + TS</span>
              </li>
              <li className="flex items-center justify-between">
                <span>Styling:</span>
                <span className="font-mono text-blue-300">Tailwind CSS (Blue Theme)</span>
              </li>
              <li className="flex items-center justify-between">
                <span>Full-Text Search:</span>
                <span className="font-mono text-blue-300">tsvector / unaccent</span>
              </li>
              <li className="flex items-center justify-between">
                <span>Caching & Jobs:</span>
                <span className="font-mono text-blue-300">Redis 7 + Hangfire</span>
              </li>
              <li className="flex items-center justify-between">
                <span>Object Storage:</span>
                <span className="font-mono text-blue-300">MinIO (S3-compatible)</span>
              </li>
            </ul>
          </div>

          {/* System status & newsletter */}
          <div>
            <h4 className="text-sm font-semibold text-white uppercase tracking-wider mb-4 border-l-2 border-blue-500 pl-2">
              Trạng thái & Giám sát
            </h4>
            <div className="bg-slate-800/80 rounded-xl p-3 border border-slate-700/80 space-y-3">
              <div className="flex items-center justify-between">
                <span className="text-xs text-slate-300 font-medium flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-full bg-emerald-400 animate-ping"></span>
                  Hệ thống hoạt động
                </span>
                <span className="text-[10px] text-emerald-400 font-mono bg-emerald-950/60 px-2 py-0.5 rounded border border-emerald-800/50">
                  99.98% Uptime
                </span>
              </div>
              <p className="text-[11px] text-slate-400 leading-normal">
                Quan sát thời gian thực với OpenTelemetry & Serilog Structured Logging.
              </p>
              <button
                onClick={() => setHealthModalOpen(true)}
                className="w-full py-1.5 px-3 bg-blue-600/20 hover:bg-blue-600/30 text-blue-400 hover:text-blue-300 rounded-lg text-xs font-medium border border-blue-500/30 flex items-center justify-center gap-1.5 transition cursor-pointer"
              >
                <Activity className="w-3.5 h-3.5" />
                <span>Xem Health Checks (/health)</span>
                <ArrowUpRight className="w-3 h-3" />
              </button>
            </div>
          </div>
        </div>

        {/* Bottom copyright */}
        <div className="border-t border-slate-800/90 pt-6 flex flex-col sm:flex-row items-center justify-between text-xs text-slate-500 gap-4">
          <p>© 2026 Culinary Blog. Giáo trình Phát triển Ứng dụng Web Nâng cao V4. Chuẩn IEEE 830.</p>
          <div className="flex items-center gap-1 text-slate-400">
            <span>Thiết kế với phong cách xanh dương</span>
            <span className="w-2 h-2 rounded-full bg-blue-500 inline-block mx-1"></span>
            <span>Tailwind CSS & TypeScript</span>
          </div>
        </div>
      </div>
    </footer>
  );
}
