import React, { useState } from 'react';
import { useApp } from '../context/AppContext';
import { 
  ChefHat, 
  Search, 
  PlusCircle, 
  Clock, 
  Pause, 
  Play, 
  X, 
  ShieldCheck, 
  User, 
  Menu, 
  Sparkles,
  Layers,
  BookOpen,
  LogOut,
  ChevronDown,
  FileCode2,
  Activity,
  RefreshCw
} from 'lucide-react';
import { Role } from '../types';
import DotnetArchitectureModal from './DotnetArchitectureModal';
import ApiActivityModal from './ApiActivityModal';

export default function Navbar() {
  const { 
    pathname, 
    navigate, 
    currentUser, 
    switchRole, 
    logout, 
    setSearchModalOpen,
    activeTimer,
    togglePauseTimer,
    resetTimer,
    setHealthModalOpen,
    bookmarks,
    apiConnected,
    apiLatencyMs,
    isApiSyncing,
    syncWithBackend,
    apiActivityModalOpen,
    setApiActivityModalOpen,
  } = useApp();

  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [roleDropdownOpen, setRoleDropdownOpen] = useState(false);
  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const [showDotnetModal, setShowDotnetModal] = useState(false);

  const navLinks = [
    { name: 'Trang chủ', href: '/', icon: Sparkles },
    { name: 'Khám phá công thức', href: '/recipes', icon: BookOpen },
    { name: 'Danh mục', href: '/categories', icon: Layers },
    ...(currentUser ? [{ name: 'Bảng điều khiển', href: '/dashboard', icon: ChefHat }] : []),
  ];

  const formatTimer = (seconds: number) => {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  };

  return (
    <header className="sticky top-0 z-40 w-full border-b border-blue-100/80 bg-white/95 backdrop-blur-md shadow-xs">
      {/* Top Banner: Subtitle & Quick status */}
      <div className="bg-gradient-to-r from-blue-900 via-blue-800 to-indigo-900 text-white text-xs px-4 py-1.5 hidden sm:flex items-center justify-between">
        <div className="flex items-center gap-2">
          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold bg-blue-500/30 text-blue-200 border border-blue-400/30">
            SRS v1.0.0 Approved
          </span>
          <span className="text-blue-100 font-medium">
            Next.js App Router + .NET 10 Minimal APIs Architecture
          </span>
        </div>
        <div className="flex items-center gap-3 text-blue-200">
          <button
            onClick={() => setApiActivityModalOpen(true)}
            className="flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-blue-500/30 hover:bg-blue-500/50 text-blue-100 hover:text-white text-[11px] font-bold border border-blue-400/40 transition cursor-pointer"
            title="Xem nhật ký trao đổi HTTP API giữa .tsx và backend"
          >
            <Activity className="w-3 h-3 text-blue-300 animate-pulse" />
            <span>Nhật ký API ({apiLatencyMs}ms)</span>
          </button>
          <span className="text-blue-400">|</span>
          <button
            onClick={() => setShowDotnetModal(true)}
            className="flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-purple-600/50 hover:bg-purple-500 text-purple-200 hover:text-white text-[11px] font-bold border border-purple-400/40 transition cursor-pointer"
            title="Xem mã nguồn kiến trúc ASP.NET Core .NET 10"
          >
            <FileCode2 className="w-3 h-3 text-purple-300" />
            <span>Mã nguồn .NET 10 (C#)</span>
          </button>
          <span className="text-blue-400">|</span>
          <button 
            onClick={() => syncWithBackend()}
            disabled={isApiSyncing}
            className="hover:text-white transition flex items-center gap-1 text-[11px] cursor-pointer"
            title="Nhấn để đồng bộ dữ liệu mới nhất từ Backend API"
          >
            <RefreshCw className={`w-3 h-3 ${isApiSyncing ? 'animate-spin text-amber-400' : 'text-emerald-400'}`} />
            <span>{apiConnected ? 'API: Connected' : 'API: Reconnecting'}</span>
          </button>
        </div>
      </div>

      {/* Main Navbar */}
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-18">
          {/* Brand Logo */}
          <div className="flex items-center gap-3">
            <button 
              onClick={() => navigate('/')} 
              className="flex items-center gap-2.5 text-left group cursor-pointer focus:outline-none"
            >
              <div className="w-10 h-10 rounded-xl bg-gradient-to-tr from-blue-700 to-blue-500 flex items-center justify-center text-white shadow-md shadow-blue-500/25 group-hover:scale-105 transition-transform duration-200">
                <ChefHat className="w-6 h-6" />
              </div>
              <div>
                <div className="flex items-center gap-1.5">
                  <span className="text-xl font-bold tracking-tight text-slate-900 group-hover:text-blue-600 transition-colors">
                    Culinary<span className="text-blue-600">Blog</span>
                  </span>
                  <span className="text-[10px] uppercase font-bold tracking-wider px-1.5 py-0.5 rounded bg-blue-100 text-blue-800">
                    V4
                  </span>
                </div>
                <p className="text-[11px] text-slate-700 hidden sm:block font-medium">
                  Ẩm Thực & Nấu Ăn Chuyên Nghiệp
                </p>
              </div>
            </button>
          </div>

          {/* Desktop Navigation Links */}
          <nav className="hidden md:flex items-center gap-1">
            {navLinks.map((link) => {
              const Icon = link.icon;
              const isActive = pathname === link.href || (link.href !== '/' && pathname.startsWith(link.href));
              return (
                <button
                  key={link.href}
                  onClick={() => navigate(link.href)}
                  className={`flex items-center gap-2 px-3.5 py-2 rounded-lg text-sm font-medium transition-all cursor-pointer ${
                    isActive
                      ? 'bg-blue-50 text-blue-700 font-semibold shadow-xs'
                      : 'text-slate-600 hover:text-blue-600 hover:bg-slate-50'
                  }`}
                >
                  <Icon className={`w-4 h-4 ${isActive ? 'text-blue-600' : 'text-slate-400'}`} />
                  {link.name}
                </button>
              );
            })}
          </nav>

          {/* Right Action Tools */}
          <div className="flex items-center gap-2.5">
            {/* Search Trigger */}
            <button
              onClick={() => setSearchModalOpen(true)}
              className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-slate-100 hover:bg-blue-50 hover:text-blue-600 text-slate-500 text-xs font-medium transition border border-slate-200/80 cursor-pointer"
              title="Tìm kiếm công thức (FR-SRCH-001)"
            >
              <Search className="w-4 h-4 text-slate-600" />
              <span className="hidden lg:inline text-slate-700 font-medium">Tìm công thức...</span>
              <kbd className="hidden lg:inline-block px-1.5 py-0.5 text-[10px] bg-white border border-slate-300 rounded text-slate-500 shadow-2xs">
                ⌘K
              </kbd>
            </button>

            {/* Active Kitchen Timer Widget (FR-RCP-010 Kitchen Assistant) */}
            {activeTimer && (
              <div className="flex items-center gap-2 px-3 py-1 bg-amber-50 border border-amber-300/80 rounded-lg text-xs animate-in fade-in">
                <Clock className="w-3.5 h-3.5 text-amber-600 animate-spin" style={{ animationDuration: '4s' }} />
                <div className="flex flex-col">
                  <span className="text-[10px] text-amber-900 font-semibold truncate max-w-[80px]">
                    B.{activeTimer.stepNumber}
                  </span>
                  <span className="font-mono font-bold text-amber-900">
                    {formatTimer(activeTimer.remainingSeconds)}
                  </span>
                </div>
                <button
                  onClick={togglePauseTimer}
                  className="p-1 hover:bg-amber-100 rounded text-amber-800 transition cursor-pointer"
                  title={activeTimer.isRunning ? 'Tạm dừng' : 'Tiếp tục'}
                >
                  {activeTimer.isRunning ? <Pause className="w-3 h-3" /> : <Play className="w-3 h-3" />}
                </button>
                <button
                  onClick={resetTimer}
                  className="p-1 hover:bg-amber-100 rounded text-amber-800 transition cursor-pointer"
                  title="Hủy hẹn giờ"
                >
                  <X className="w-3 h-3" />
                </button>
              </div>
            )}

            {/* Role Switcher Pill - Demonstration of SRS Actors (Guest, Author, Admin) */}
            <div className="relative">
              <button
                onClick={() => {
                  setRoleDropdownOpen(!roleDropdownOpen);
                  setUserMenuOpen(false);
                }}
                className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg bg-blue-50 hover:bg-blue-100 text-blue-800 text-xs font-semibold border border-blue-200 transition cursor-pointer"
                title="Chuyển đổi vai trò người dùng (SRS Chapter 2.3)"
              >
                <ShieldCheck className="w-3.5 h-3.5 text-blue-600" />
                <span>
                  {currentUser ? (currentUser.role === 'Admin' ? 'Admin' : 'Tác giả') : 'Khách'}
                </span>
                <ChevronDown className="w-3 h-3 text-blue-600" />
              </button>

              {roleDropdownOpen && (
                <div className="absolute right-0 mt-2 w-56 bg-white rounded-xl shadow-xl border border-slate-100 py-1.5 z-50 animate-in fade-in zoom-in-95">
                  <div className="px-3 py-1.5 border-b border-slate-100">
                    <p className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">
                      Mô phỏng vai trò (SRS 2.3)
                    </p>
                  </div>
                  <button
                    onClick={() => {
                      switchRole('Author');
                      setRoleDropdownOpen(false);
                    }}
                    className="w-full text-left px-3 py-2 text-xs hover:bg-blue-50 flex items-center justify-between text-slate-700 cursor-pointer"
                  >
                    <div>
                      <p className="font-semibold text-slate-900">Tác giả (Author)</p>
                      <p className="text-[11px] text-slate-500">Tạo & quản lý công thức của mình</p>
                    </div>
                    {currentUser?.role === 'Author' && (
                      <span className="w-2 h-2 rounded-full bg-blue-600"></span>
                    )}
                  </button>
                  <button
                    onClick={() => {
                      switchRole('Admin');
                      setRoleDropdownOpen(false);
                    }}
                    className="w-full text-left px-3 py-2 text-xs hover:bg-blue-50 flex items-center justify-between text-slate-700 cursor-pointer"
                  >
                    <div>
                      <p className="font-semibold text-slate-900">Quản trị viên (Admin)</p>
                      <p className="text-[11px] text-slate-500">Quản lý toàn bộ bài & danh mục</p>
                    </div>
                    {currentUser?.role === 'Admin' && (
                      <span className="w-2 h-2 rounded-full bg-indigo-600"></span>
                    )}
                  </button>
                  <button
                    onClick={() => {
                      switchRole('Guest');
                      setRoleDropdownOpen(false);
                    }}
                    className="w-full text-left px-3 py-2 text-xs hover:bg-slate-50 flex items-center justify-between text-slate-700 cursor-pointer"
                  >
                    <div>
                      <p className="font-semibold text-slate-900">Khách (Guest)</p>
                      <p className="text-[11px] text-slate-500">Chỉ xem công thức đã xuất bản</p>
                    </div>
                    {!currentUser && (
                      <span className="w-2 h-2 rounded-full bg-emerald-600"></span>
                    )}
                  </button>
                </div>
              )}
            </div>

            {/* User Account / Auth Actions */}
            {currentUser ? (
              <div className="relative">
                <button
                  onClick={() => {
                    setUserMenuOpen(!userMenuOpen);
                    setRoleDropdownOpen(false);
                  }}
                  className="flex items-center gap-2 p-1 rounded-full hover:ring-2 hover:ring-blue-400 transition cursor-pointer"
                >
                  <img
                    src={currentUser.avatarUrl}
                    alt={currentUser.displayName}
                    className="w-8 h-8 rounded-full object-cover border border-slate-200"
                  />
                </button>

                {userMenuOpen && (
                  <div className="absolute right-0 mt-2 w-60 bg-white rounded-xl shadow-xl border border-slate-100 py-2 z-50 animate-in fade-in">
                    <div className="px-4 py-2 border-b border-slate-100">
                      <p className="text-xs font-semibold text-slate-900 truncate">
                        {currentUser.displayName}
                      </p>
                      <p className="text-[11px] text-slate-500 truncate">{currentUser.email}</p>
                      <span className="inline-block mt-1 text-[10px] font-medium px-2 py-0.5 bg-blue-100 text-blue-800 rounded">
                        Vai trò: {currentUser.role}
                      </span>
                    </div>

                    <button
                      onClick={() => {
                        navigate('/dashboard');
                        setUserMenuOpen(false);
                      }}
                      className="w-full text-left px-4 py-2 text-xs text-slate-700 hover:bg-slate-50 flex items-center gap-2 cursor-pointer"
                    >
                      <ChefHat className="w-4 h-4 text-blue-600" />
                      <span>Bảng điều khiển bài viết</span>
                    </button>

                    <button
                      onClick={() => {
                        navigate('/profile');
                        setUserMenuOpen(false);
                      }}
                      className="w-full text-left px-4 py-2 text-xs text-slate-700 hover:bg-slate-50 flex items-center gap-2 cursor-pointer"
                    >
                      <User className="w-4 h-4 text-slate-400" />
                      <span>Hồ sơ cá nhân</span>
                    </button>

                    <button
                      onClick={() => {
                        navigate('/recipes?saved=true');
                        setUserMenuOpen(false);
                      }}
                      className="w-full text-left px-4 py-2 text-xs text-slate-700 hover:bg-slate-50 flex items-center justify-between cursor-pointer"
                    >
                      <span className="flex items-center gap-2">
                        <BookOpen className="w-4 h-4 text-slate-400" />
                        <span>Sổ tay đã lưu</span>
                      </span>
                      {bookmarks.length > 0 && (
                        <span className="px-1.5 py-0.5 text-[10px] bg-blue-100 text-blue-700 rounded-full font-bold">
                          {bookmarks.length}
                        </span>
                      )}
                    </button>

                    <div className="border-t border-slate-100 mt-1 pt-1">
                      <button
                        onClick={() => {
                          logout();
                          setUserMenuOpen(false);
                        }}
                        className="w-full text-left px-4 py-2 text-xs text-red-600 hover:bg-red-50 flex items-center gap-2 cursor-pointer"
                      >
                        <LogOut className="w-4 h-4 text-red-500" />
                        <span>Đăng xuất</span>
                      </button>
                    </div>
                  </div>
                )}
              </div>
            ) : (
              <div className="flex items-center gap-2">
                <button
                  onClick={() => navigate('/auth/login')}
                  className="text-xs font-semibold text-slate-700 hover:text-blue-600 px-2.5 py-1.5 cursor-pointer"
                >
                  Đăng nhập
                </button>
                <button
                  onClick={() => navigate('/auth/register')}
                  className="text-xs font-semibold bg-blue-600 hover:bg-blue-700 text-white px-3 py-1.5 rounded-lg shadow-sm shadow-blue-500/20 transition cursor-pointer"
                >
                  Đăng ký
                </button>
              </div>
            )}

            {/* Create Recipe CTA Button */}
            {currentUser && (
              <button
                onClick={() => navigate('/dashboard/recipes/new')}
                className="hidden sm:flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-blue-600 hover:bg-blue-700 text-white text-xs font-semibold shadow-sm shadow-blue-600/25 transition cursor-pointer"
              >
                <PlusCircle className="w-4 h-4" />
                <span>Đăng công thức</span>
              </button>
            )}

            {/* Mobile menu hamburger */}
            <button
              onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
              className="md:hidden p-2 text-slate-600 hover:text-blue-600 hover:bg-slate-100 rounded-lg cursor-pointer"
              aria-label="Toggle menu"
            >
              <Menu className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Mobile menu dropdown */}
        {mobileMenuOpen && (
          <div className="md:hidden border-t border-slate-100 py-3 space-y-1 animate-in slide-in-from-top-2">
            {navLinks.map((link) => {
              const Icon = link.icon;
              const isActive = pathname === link.href;
              return (
                <button
                  key={link.href}
                  onClick={() => {
                    navigate(link.href);
                    setMobileMenuOpen(false);
                  }}
                  className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium ${
                    isActive ? 'bg-blue-50 text-blue-700 font-bold' : 'text-slate-700 hover:bg-slate-50'
                  }`}
                >
                  <Icon className="w-4 h-4 text-blue-600" />
                  <span>{link.name}</span>
                </button>
              );
            })}
            {currentUser && (
              <button
                onClick={() => {
                  navigate('/dashboard/recipes/new');
                  setMobileMenuOpen(false);
                }}
                className="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-blue-700 bg-blue-50/80"
              >
                <PlusCircle className="w-4 h-4 text-blue-600" />
                <span>Đăng công thức mới</span>
              </button>
            )}

            <button
              onClick={() => {
                setShowDotnetModal(true);
                setMobileMenuOpen(false);
              }}
              className="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-semibold text-purple-700 bg-purple-50"
            >
              <FileCode2 className="w-4 h-4 text-purple-600" />
              <span>Mã nguồn .NET 10 (C#)</span>
            </button>
          </div>
        )}
      </div>

      {/* .NET 10 Clean Architecture Explorer Modal */}
      <DotnetArchitectureModal 
        isOpen={showDotnetModal} 
        onClose={() => setShowDotnetModal(false)} 
      />

      {/* Live API Traffic Inspector Modal */}
      <ApiActivityModal
        isOpen={apiActivityModalOpen}
        onClose={() => setApiActivityModalOpen(false)}
      />
    </header>
  );
}
