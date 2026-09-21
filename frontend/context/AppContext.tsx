import React, { createContext, useContext, useState, useEffect, useMemo, ReactNode } from 'react';
import { Recipe, Category, ApplicationUser, Role, RecipeFilterParams, SystemHealth } from '../types';
import { MOCK_RECIPES, MOCK_CATEGORIES, MOCK_USERS, MOCK_SYSTEM_HEALTH, unaccent, generateSlug } from '../data/mockData';
import { api, ProblemDetails } from '../services/api';

interface ToastInfo {
  id: string;
  title: string;
  description?: string;
  type: 'success' | 'error' | 'info';
}

interface ActiveTimer {
  recipeTitle: string;
  stepNumber: number;
  totalSeconds: number;
  remainingSeconds: number;
  isRunning: boolean;
}

interface AppContextType {
  pathname: string;
  searchParams: URLSearchParams;
  navigate: (url: string) => void;
  currentUser: ApplicationUser | null;
  setCurrentUser: (user: ApplicationUser | null) => void;
  switchRole: (role: Role) => void;
  logout: () => void;
  loginWithGoogle: () => void;
  
  // Data
  recipes: Recipe[];
  categories: Category[];
  systemHealth: SystemHealth;
  
  // Backend API Sync Status
  apiConnected: boolean;
  apiLatencyMs: number;
  lastApiSync: string;
  isApiSyncing: boolean;
  cacheHeaderStatus: string;
  syncWithBackend: () => Promise<void>;
  
  // Operations connected to Backend REST API
  createRecipe: (data: Partial<Recipe>) => Promise<{ success: boolean; slug?: string; error?: string }>;
  updateRecipe: (id: string, data: Partial<Recipe>) => Promise<{ success: boolean; error?: string }>;
  deleteRecipe: (id: string) => Promise<{ success: boolean; error?: string }>;
  publishRecipe: (id: string) => Promise<{ success: boolean; error?: string }>;
  unpublishRecipe: (id: string) => Promise<{ success: boolean; error?: string }>;
  archiveRecipe: (id: string) => Promise<{ success: boolean; error?: string }>;
  createCategory: (data: { name: string; description?: string; imageUrl?: string }) => Promise<{ success: boolean; category?: Category; error?: string }>;
  toggleLike: (id: string) => void;
  likedRecipeIds: string[];
  bookmarks: string[];
  toggleBookmark: (id: string) => void;
  
  // Search & Filtering
  filterRecipes: (params: RecipeFilterParams) => Recipe[];
  
  // Modals & Overlays
  searchModalOpen: boolean;
  setSearchModalOpen: (open: boolean) => void;
  healthModalOpen: boolean;
  setHealthModalOpen: (open: boolean) => void;
  apiActivityModalOpen: boolean;
  setApiActivityModalOpen: (open: boolean) => void;
  jsonLdRecipe: Recipe | null;
  setJsonLdRecipe: (recipe: Recipe | null) => void;
  
  // Toasts
  toasts: ToastInfo[];
  showToast: (title: string, description?: string, type?: 'success' | 'error' | 'info') => void;
  removeToast: (id: string) => void;
  
  // Kitchen Timer
  activeTimer: ActiveTimer | null;
  startTimer: (recipeTitle: string, stepNumber: number, minutes: number) => void;
  togglePauseTimer: () => void;
  resetTimer: () => void;
}

const AppContext = createContext<AppContextType | undefined>(undefined);

export function AppProvider({ children }: { children: ReactNode }) {
  // Sync with browser URL / hash
  const [currentUrl, setCurrentUrl] = useState<string>(() => {
    if (typeof window !== 'undefined') {
      const path = window.location.pathname;
      const search = window.location.search;
      return path ? `${path}${search}` : '/';
    }
    return '/';
  });

  const { pathname, searchParams } = useMemo(() => {
    try {
      const [path, query] = currentUrl.split('?');
      return {
        pathname: path || '/',
        searchParams: new URLSearchParams(query || ''),
      };
    } catch {
      return { pathname: '/', searchParams: new URLSearchParams() };
    }
  }, [currentUrl]);

  const navigate = (url: string) => {
    setCurrentUrl(url);
    if (typeof window !== 'undefined') {
      window.history.pushState(null, '', url);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  };

  useEffect(() => {
    const handlePopState = () => {
      setCurrentUrl(window.location.pathname + window.location.search);
    };
    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, []);

  // Current logged in user (Defaults to Chef Minh Tuấn - Author role)
  const [currentUser, setCurrentUser] = useState<ApplicationUser | null>(MOCK_USERS[0]);
  const [recipes, setRecipes] = useState<Recipe[]>(MOCK_RECIPES);
  const [categories, setCategories] = useState<Category[]>(MOCK_CATEGORIES);
  const [likedRecipeIds, setLikedRecipeIds] = useState<string[]>([]);
  const [bookmarks, setBookmarks] = useState<string[]>([]);
  
  // API connection states
  const [apiConnected, setApiConnected] = useState<boolean>(true);
  const [apiLatencyMs, setApiLatencyMs] = useState<number>(12);
  const [lastApiSync, setLastApiSync] = useState<string>('Khởi tạo ban đầu');
  const [isApiSyncing, setIsApiSyncing] = useState<boolean>(false);
  const [cacheHeaderStatus, setCacheHeaderStatus] = useState<string>('HIT');

  // Modals
  const [searchModalOpen, setSearchModalOpen] = useState(false);
  const [healthModalOpen, setHealthModalOpen] = useState(false);
  const [apiActivityModalOpen, setApiActivityModalOpen] = useState(false);
  const [jsonLdRecipe, setJsonLdRecipe] = useState<Recipe | null>(null);
  const [toasts, setToasts] = useState<ToastInfo[]>([]);

  // Kitchen Timer
  const [activeTimer, setActiveTimer] = useState<ActiveTimer | null>(null);

  // Synchronize data from backend API
  const syncWithBackend = async () => {
    setIsApiSyncing(true);
    const start = performance.now();
    try {
      // 1. Fetch Categories from GET /api/v1/categories
      const catRes = await api.getCategories();
      if (Array.isArray(catRes.data)) {
        setCategories(catRes.data);
        setCacheHeaderStatus(catRes.cacheStatus);
      }

      // 2. Fetch Recipes from GET /api/v1/recipes
      const recRes = await api.getRecipes(
        { pageSize: 100 },
        currentUser?.role || 'Guest',
        currentUser?.id || 'guest'
      );
      if (recRes && Array.isArray(recRes.items)) {
        setRecipes(recRes.items);
      }

      setApiConnected(true);
      setApiLatencyMs(Math.round(performance.now() - start));
      setLastApiSync(new Date().toLocaleTimeString());
    } catch (err) {
      console.error('Lỗi khi đồng bộ dữ liệu từ Backend API:', err);
      setApiConnected(false);
    } finally {
      setIsApiSyncing(false);
    }
  };

  // Run backend sync on boot & when user switches role
  useEffect(() => {
    syncWithBackend();
  }, [currentUser?.role, currentUser?.id]);

  useEffect(() => {
    let interval: NodeJS.Timeout | null = null;
    if (activeTimer && activeTimer.isRunning && activeTimer.remainingSeconds > 0) {
      interval = setInterval(() => {
        setActiveTimer((prev) => {
          if (!prev) return null;
          if (prev.remainingSeconds <= 1) {
            showToast('Hẹn giờ hoàn thành!', `Bước ${prev.stepNumber} của ${prev.recipeTitle} đã xong.`, 'info');
            try {
              const audioCtx = new (window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext)();
              const osc = audioCtx.createOscillator();
              osc.type = 'sine';
              osc.frequency.setValueAtTime(587.33, audioCtx.currentTime); // D5
              osc.connect(audioCtx.destination);
              osc.start();
              osc.stop(audioCtx.currentTime + 0.6);
            } catch {
              // fallback silent
            }
            return { ...prev, remainingSeconds: 0, isRunning: false };
          }
          return { ...prev, remainingSeconds: prev.remainingSeconds - 1 };
        });
      }, 1000);
    }
    return () => {
      if (interval) clearInterval(interval);
    };
  }, [activeTimer]);

  const startTimer = (recipeTitle: string, stepNumber: number, minutes: number) => {
    const totalSeconds = Math.max(1, Math.round(minutes * 60));
    setActiveTimer({
      recipeTitle,
      stepNumber,
      totalSeconds,
      remainingSeconds: totalSeconds,
      isRunning: true,
    });
    showToast('Bắt đầu hẹn giờ', `Bước ${stepNumber}: ${minutes} phút đang đếm ngược.`, 'info');
  };

  const togglePauseTimer = () => {
    setActiveTimer((prev) => (prev ? { ...prev, isRunning: !prev.isRunning } : null));
  };

  const resetTimer = () => {
    setActiveTimer(null);
  };

  const showToast = (title: string, description?: string, type: 'success' | 'error' | 'info' = 'success') => {
    const id = Math.random().toString(36).substring(2, 9);
    setToasts((prev) => [...prev, { id, title, description, type }]);
    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
    }, 4000);
  };

  const removeToast = (id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  };

  const switchRole = (role: Role) => {
    if (role === 'Guest') {
      setCurrentUser(null);
      showToast('Đã chuyển sang vai trò Khách', 'Chế độ xem công khai: Chỉ xem các công thức đã xuất bản.', 'info');
    } else if (role === 'Author') {
      setCurrentUser(MOCK_USERS[0]);
      showToast('Đã chuyển sang Tác giả', 'Đang đăng nhập dưới tư cách Chef Minh Tuấn.', 'success');
    } else if (role === 'Admin') {
      setCurrentUser(MOCK_USERS[2]);
      showToast('Đã chuyển sang Quản trị viên', 'Quyền Admin: Toàn quyền quản lý bài viết & danh mục.', 'success');
    }
  };

  const logout = () => {
    setCurrentUser(null);
    showToast('Đã đăng xuất', 'Bạn đang duyệt web dưới chế độ Khách.', 'info');
    navigate('/');
  };

  const loginWithGoogle = () => {
    setCurrentUser(MOCK_USERS[0]);
    showToast('Đăng nhập Google thành công', `Chào mừng ${MOCK_USERS[0].displayName} quay trở lại!`, 'success');
  };

  const toggleLike = async (id: string) => {
    const isLiked = likedRecipeIds.includes(id);
    setLikedRecipeIds((prev) =>
      isLiked ? prev.filter((item) => item !== id) : [...prev, id]
    );

    // Optimistic UI update
    setRecipes((all) =>
      all.map((r) => (r.id === id ? { ...r, likeCount: Math.max(0, r.likeCount + (isLiked ? -1 : 1)) } : r))
    );

    try {
      await api.toggleLikeRecipe(id);
    } catch (err) {
      console.error('Lỗi like recipe:', err);
    }
  };

  const toggleBookmark = (id: string) => {
    setBookmarks((prev) => {
      const isSaved = prev.includes(id);
      const updated = isSaved ? prev.filter((item) => item !== id) : [...prev, id];
      showToast(isSaved ? 'Đã bỏ lưu công thức' : 'Đã lưu công thức vào sổ tay');
      return updated;
    });
  };

  // Recipe Operations connected to Backend REST API
  const createRecipe = async (data: Partial<Recipe>) => {
    if (!currentUser) {
      return { success: false, error: 'Bạn phải đăng nhập để tạo công thức.' };
    }

    try {
      const newRecipe = await api.createRecipe(data, currentUser.role, currentUser.id);
      setRecipes((prev) => [newRecipe, ...prev]);

      // Re-fetch categories to sync recipe counts
      const catRes = await api.getCategories();
      if (Array.isArray(catRes.data)) setCategories(catRes.data);

      showToast('Tạo công thức thành công (201 Created)', `Đã lưu công thức "${newRecipe.title}"`, 'success');
      return { success: true, slug: newRecipe.slug };
    } catch (err: any) {
      const pErr = err as ProblemDetails;
      const msg = pErr.detail || pErr.title || 'Không thể tạo công thức.';
      showToast('Lỗi tạo công thức', msg, 'error');
      return { success: false, error: msg };
    }
  };

  const updateRecipe = async (id: string, data: Partial<Recipe>) => {
    if (!currentUser) return { success: false, error: 'Vui lòng đăng nhập.' };

    try {
      const updated = await api.updateRecipe(id, data, currentUser.role, currentUser.id);
      setRecipes((prev) => prev.map((r) => (r.id === id ? updated : r)));
      showToast('Cập nhật thành công (200 OK)', `Công thức "${updated.title}" đã được lưu trên backend.`, 'success');
      return { success: true };
    } catch (err: any) {
      const pErr = err as ProblemDetails;
      const msg = pErr.detail || 'Không thể cập nhật công thức.';
      showToast('Lỗi cập nhật', msg, 'error');
      return { success: false, error: msg };
    }
  };

  const deleteRecipe = async (id: string) => {
    if (!currentUser) return { success: false, error: 'Vui lòng đăng nhập.' };

    const target = recipes.find((r) => r.id === id);
    try {
      await api.deleteRecipe(id, currentUser.role, currentUser.id);
      setRecipes((prev) => prev.filter((r) => r.id !== id));

      // Update category count
      if (target) {
        setCategories((prev) =>
          prev.map((c) => (c.id === target.categoryId ? { ...c, recipeCount: Math.max(0, c.recipeCount - 1) } : c))
        );
      }
      showToast('Đã xóa công thức', `Công thức "${target?.title || id}" đã được xóa khỏi hệ thống.`, 'info');
      return { success: true };
    } catch (err: any) {
      const pErr = err as ProblemDetails;
      const msg = pErr.detail || 'Không thể xóa công thức.';
      showToast('Lỗi xóa công thức', msg, 'error');
      return { success: false, error: msg };
    }
  };

  const publishRecipe = async (id: string) => {
    if (!currentUser) return { success: false, error: 'Vui lòng đăng nhập.' };
    try {
      const updated = await api.updateRecipeStatus(id, 'Published', currentUser.role, currentUser.id);
      setRecipes((prev) => prev.map((r) => (r.id === id ? updated : r)));
      showToast('Xuất bản thành công (200 OK)', `Công thức "${updated.title}" đã được công khai trên toàn hệ thống!`, 'success');
      
      // Update categories count
      const catRes = await api.getCategories();
      if (Array.isArray(catRes.data)) setCategories(catRes.data);

      return { success: true };
    } catch (err: any) {
      const pErr = err as ProblemDetails;
      const msg = pErr.detail || 'Không thể xuất bản công thức.';
      showToast('Quy tắc xuất bản (422)', msg, 'error');
      return { success: false, error: msg };
    }
  };

  const unpublishRecipe = async (id: string) => {
    if (!currentUser) return { success: false, error: 'Vui lòng đăng nhập.' };
    try {
      const updated = await api.updateRecipeStatus(id, 'Draft', currentUser.role, currentUser.id);
      setRecipes((prev) => prev.map((r) => (r.id === id ? updated : r)));
      showToast('Đã chuyển thành bản nháp (200 OK)', `Công thức "${updated.title}" chuyển sang chế độ riêng tư.`, 'info');
      
      const catRes = await api.getCategories();
      if (Array.isArray(catRes.data)) setCategories(catRes.data);

      return { success: true };
    } catch (err: any) {
      const pErr = err as ProblemDetails;
      const msg = pErr.detail || 'Không thể chuyển bản nháp.';
      showToast('Lỗi cập nhật', msg, 'error');
      return { success: false, error: msg };
    }
  };

  const archiveRecipe = async (id: string) => {
    if (!currentUser) return { success: false, error: 'Vui lòng đăng nhập.' };
    try {
      const updated = await api.updateRecipeStatus(id, 'Archived', currentUser.role, currentUser.id);
      setRecipes((prev) => prev.map((r) => (r.id === id ? updated : r)));
      showToast('Đã lưu trữ công thức (200 OK)', `Công thức "${updated.title}" đã được chuyển vào kho lưu trữ.`, 'info');
      
      const catRes = await api.getCategories();
      if (Array.isArray(catRes.data)) setCategories(catRes.data);

      return { success: true };
    } catch (err: any) {
      const pErr = err as ProblemDetails;
      const msg = pErr.detail || 'Không thể lưu trữ công thức.';
      showToast('Lỗi lưu trữ', msg, 'error');
      return { success: false, error: msg };
    }
  };

  const createCategory = async (data: { name: string; description?: string; imageUrl?: string }) => {
    if (currentUser?.role !== 'Admin') {
      showToast('Từ chối quyền truy cập (403)', 'Chỉ Quản trị viên (Admin) mới có quyền tạo danh mục.', 'error');
      return { success: false, error: 'Chỉ Admin mới có quyền tạo danh mục.' };
    }

    try {
      const res = await api.createCategory(data, 'Admin', currentUser.id);
      setCategories((prev) => [...prev, res.category]);
      showToast(
        'Tạo danh mục thành công (201 Created)',
        `Đã tạo "${res.category.name}" (Slug: /${res.category.slug}). Header Location: ${res.location}`,
        'success'
      );
      return { success: true, category: res.category };
    } catch (err: any) {
      const pErr = err as ProblemDetails;
      const msg = pErr.detail || 'Không thể tạo danh mục.';
      showToast(`Lỗi tạo danh mục (${pErr.status || 400})`, msg, 'error');
      return { success: false, error: msg };
    }
  };

  const filterRecipes = (params: RecipeFilterParams): Recipe[] => {
    return recipes.filter((r) => {
      // Visibility rule based on FR-RCP-001 & FR-RCP-002
      if (currentUser?.role === 'Admin') {
        // Admin sees all
      } else if (currentUser?.role === 'Author') {
        // Author sees published + own drafts/archived
        if (r.status !== 'Published' && r.authorId !== currentUser.id) return false;
      } else {
        // Guest only sees published
        if (r.status !== 'Published') return false;
      }

      // Search term
      if (params.searchTerm && params.searchTerm.trim()) {
        const query = unaccent(params.searchTerm);
        const titleMatch = unaccent(r.title).includes(query);
        const descMatch = unaccent(r.description).includes(query);
        const ingredientMatch = r.ingredients?.some((ing) => unaccent(ing.name).includes(query));
        if (!titleMatch && !descMatch && !ingredientMatch) return false;
      }

      // Category
      if (params.categoryId && params.categoryId !== 'all') {
        if (r.categoryId !== params.categoryId) return false;
      }

      // Difficulty
      if (params.difficulty && params.difficulty !== 'All') {
        if (r.difficulty !== params.difficulty) return false;
      }

      // Max Cook Time
      if (params.maxCookTime && params.maxCookTime > 0) {
        if (r.cookTimeMinutes > params.maxCookTime) return false;
      }

      return true;
    }).sort((a, b) => {
      if (params.sort === 'createdAt') {
        return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
      }
      if (params.sort === 'title') {
        return a.title.localeCompare(b.title, 'vi');
      }
      if (params.sort === 'cookTime') {
        return a.cookTimeMinutes - b.cookTimeMinutes;
      }
      // default: -createdAt
      return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
    });
  };

  return (
    <AppContext.Provider
      value={{
        pathname,
        searchParams,
        navigate,
        currentUser,
        setCurrentUser,
        switchRole,
        logout,
        loginWithGoogle,
        recipes,
        categories,
        systemHealth: MOCK_SYSTEM_HEALTH,
        apiConnected,
        apiLatencyMs,
        lastApiSync,
        isApiSyncing,
        cacheHeaderStatus,
        syncWithBackend,
        createRecipe,
        updateRecipe,
        deleteRecipe,
        publishRecipe,
        unpublishRecipe,
        archiveRecipe,
        createCategory,
        toggleLike,
        likedRecipeIds,
        bookmarks,
        toggleBookmark,
        filterRecipes,
        searchModalOpen,
        setSearchModalOpen,
        healthModalOpen,
        setHealthModalOpen,
        apiActivityModalOpen,
        setApiActivityModalOpen,
        jsonLdRecipe,
        setJsonLdRecipe,
        toasts,
        showToast,
        removeToast,
        activeTimer,
        startTimer,
        togglePauseTimer,
        resetTimer,
      }}
    >
      {children}
    </AppContext.Provider>
  );
}

export function useApp() {
  const context = useContext(AppContext);
  if (!context) {
    throw new Error('useApp must be used within an AppProvider');
  }
  return context;
}
