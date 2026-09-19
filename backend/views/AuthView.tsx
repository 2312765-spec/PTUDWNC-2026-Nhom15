import React, { useState } from 'react';
import { useApp } from '../context/AppContext';
import { ChefHat, Mail, Lock, User, ArrowRight, ShieldCheck, CheckCircle2 } from 'lucide-react';
import { MOCK_USERS } from '../data/mockData';

interface AuthViewProps {
  mode?: 'login' | 'register';
}

export default function AuthView({ mode = 'login' }: AuthViewProps) {
  const { setCurrentUser, navigate, showToast } = useApp();
  const [isLogin, setIsLogin] = useState(mode === 'login');
  
  // Form fields
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fullName, setFullName] = useState('');
  const [role, setRole] = useState<'Author' | 'Admin'>('Author');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim() || !password.trim()) {
      showToast('Lỗi nhập liệu', 'Vui lòng điền đầy đủ email và mật khẩu.', 'error');
      return;
    }

    if (!isLogin && !fullName.trim()) {
      showToast('Lỗi nhập liệu', 'Vui lòng nhập họ và tên của bạn.', 'error');
      return;
    }

    // Register or login simulation
    const newUser = {
      id: `user-${Date.now()}`,
      email: email.trim(),
      displayName: isLogin ? (email.split('@')[0] || 'Thành viên') : fullName.trim(),
      avatarUrl: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=400&q=80',
      role,
      isActive: true,
      createdAt: new Date().toISOString(),
    };

    setCurrentUser(newUser);
    showToast(
      isLogin ? 'Đăng nhập thành công' : 'Đăng ký thành công',
      `Chào mừng ${newUser.displayName} đến với Culinary Blog!`,
      'success'
    );
    navigate('/dashboard');
  };

  const handleGoogleLogin = () => {
    // Google OAuth 2.0 simulation (FR-AUTH-003)
    setCurrentUser(MOCK_USERS[0]);
    showToast('Đăng nhập Google thành công', `Chào mừng ${MOCK_USERS[0].displayName}!`, 'success');
    navigate('/dashboard');
  };

  const handleQuickLoginAs = (userIndex: number) => {
    setCurrentUser(MOCK_USERS[userIndex]);
    showToast('Đăng nhập nhanh thành công', `Đang đăng nhập dưới quyền: ${MOCK_USERS[userIndex].displayName}`, 'success');
    navigate('/dashboard');
  };

  return (
    <div className="min-h-[80vh] flex items-center justify-center px-4 py-12">
      <div className="w-full max-w-md bg-white rounded-3xl border border-slate-200 shadow-xl overflow-hidden animate-in zoom-in-95 duration-200">
        {/* Blue Header Accent */}
        <div className="bg-gradient-to-r from-blue-900 to-blue-700 text-white p-6 text-center space-y-2">
          <div className="w-12 h-12 rounded-2xl bg-white/10 backdrop-blur-md flex items-center justify-center mx-auto text-white shadow-inner">
            <ChefHat className="w-6 h-6" />
          </div>
          <h2 className="text-xl font-bold">
            {isLogin ? 'Chào mừng bạn quay lại' : 'Tạo tài khoản tác giả'}
          </h2>
          <p className="text-xs text-blue-200">
            Hệ sinh thái công thức nấu ăn Culinary Blog
          </p>
        </div>

        {/* Tab switch */}
        <div className="flex border-b border-slate-100 bg-slate-50 text-xs font-bold">
          <button
            onClick={() => setIsLogin(true)}
            className={`flex-1 py-3 text-center transition cursor-pointer ${
              isLogin ? 'bg-white text-blue-600 border-b-2 border-blue-600' : 'text-slate-500 hover:text-slate-900'
            }`}
          >
            Đăng nhập (FR-AUTH-002)
          </button>
          <button
            onClick={() => setIsLogin(false)}
            className={`flex-1 py-3 text-center transition cursor-pointer ${
              !isLogin ? 'bg-white text-blue-600 border-b-2 border-blue-600' : 'text-slate-500 hover:text-slate-900'
            }`}
          >
            Đăng ký mới (FR-AUTH-001)
          </button>
        </div>

        <div className="p-6 space-y-5 text-xs">
          {/* Google OAuth 2.0 Button (FR-AUTH-003) */}
          <button
            onClick={handleGoogleLogin}
            type="button"
            className="w-full py-2.5 px-4 bg-white hover:bg-slate-50 border border-slate-200 rounded-xl text-slate-700 font-semibold flex items-center justify-center gap-2.5 shadow-2xs transition cursor-pointer"
          >
            <svg className="w-4 h-4" viewBox="0 0 24 24">
              <path
                fill="#4285F4"
                d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"
              />
              <path
                fill="#34A853"
                d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"
              />
              <path
                fill="#FBBC05"
                d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.06H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.94l2.85-2.22.81-.63z"
              />
              <path
                fill="#EA4335"
                d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.06l3.66 2.84c.87-2.6 3.3-4.52 6.16-4.52z"
              />
            </svg>
            <span>Đăng nhập với Google OAuth 2.0</span>
          </button>

          <div className="flex items-center gap-2 text-slate-400">
            <div className="flex-1 h-px bg-slate-200"></div>
            <span className="text-[10px] uppercase font-bold tracking-wider text-slate-400">
              hoặc email & mật khẩu
            </span>
            <div className="flex-1 h-px bg-slate-200"></div>
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit} className="space-y-3.5">
            {!isLogin && (
              <div>
                <label className="font-bold text-slate-700 block mb-1">
                  Họ và tên tác giả *
                </label>
                <div className="relative">
                  <User className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
                  <input
                    type="text"
                    required
                    placeholder="Nguyễn Văn A"
                    value={fullName}
                    onChange={(e) => setFullName(e.target.value)}
                    className="w-full pl-9 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500"
                  />
                </div>
              </div>
            )}

            <div>
              <label className="font-bold text-slate-700 block mb-1">
                Địa chỉ Email *
              </label>
              <div className="relative">
                <Mail className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  type="email"
                  required
                  placeholder="chef@culinaryblog.vn"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="w-full pl-9 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500"
                />
              </div>
            </div>

            <div>
              <label className="font-bold text-slate-700 block mb-1">
                Mật khẩu *
              </label>
              <div className="relative">
                <Lock className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  type="password"
                  required
                  placeholder="••••••••"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="w-full pl-9 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500"
                />
              </div>
              <span className="text-[10px] text-slate-400 mt-1 block">
                Bảo mật mã hóa PBKDF2 theo tiêu chuẩn ASP.NET Core Identity
              </span>
            </div>

            <button
              type="submit"
              className="w-full py-2.5 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl shadow-md shadow-blue-500/20 transition flex items-center justify-center gap-2 cursor-pointer mt-2"
            >
              <span>{isLogin ? 'Đăng nhập vào hệ thống' : 'Đăng ký tài khoản tác giả'}</span>
              <ArrowRight className="w-4 h-4" />
            </button>
          </form>

          {/* Demo Quick Logins */}
          <div className="pt-3 border-t border-slate-100 space-y-2">
            <p className="text-[11px] font-semibold text-slate-500 text-center">
              Mô phỏng tài khoản thử nghiệm nhanh:
            </p>
            <div className="grid grid-cols-2 gap-2">
              <button
                type="button"
                onClick={() => handleQuickLoginAs(0)}
                className="p-2 rounded-lg bg-blue-50 hover:bg-blue-100 text-blue-700 text-[11px] font-bold border border-blue-200 transition cursor-pointer text-center"
              >
                Tác giả: Chef Minh Tuấn
              </button>
              <button
                type="button"
                onClick={() => handleQuickLoginAs(2)}
                className="p-2 rounded-lg bg-indigo-50 hover:bg-indigo-100 text-indigo-700 text-[11px] font-bold border border-indigo-200 transition cursor-pointer text-center"
              >
                Quản trị: Hoàng Nam
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
