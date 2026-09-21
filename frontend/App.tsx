import React from 'react';
import { AppProvider, useApp } from './context/AppContext';
import Navbar from './components/Navbar';
import Footer from './components/Footer';
import SearchModal from './components/SearchModal';
import JsonLdModal from './components/JsonLdModal';
import HealthCheckModal from './components/HealthCheckModal';
import ToastContainer from './components/ToastContainer';
import FloatingKitchenTimer from './components/FloatingKitchenTimer';

import HomeView from './views/HomeView';
import RecipesView from './views/RecipesView';
import RecipeDetailView from './views/RecipeDetailView';
import CategoriesView from './views/CategoriesView';
import CategoryDetailView from './views/CategoryDetailView';
import DashboardView from './views/DashboardView';
import RecipeEditorView from './views/RecipeEditorView';
import BookmarksView from './views/BookmarksView';
import ProfileView from './views/ProfileView';
import AuthView from './views/AuthView';
import { AlertCircle } from 'lucide-react';

/**
 * Main Router simulating Next.js App Router route hierarchy:
 * - / (page.tsx)
 * - /recipes (recipes/page.tsx)
 * - /recipes/[slug] (recipes/[slug]/page.tsx)
 * - /categories (categories/page.tsx)
 * - /categories/[slug] (categories/[slug]/page.tsx)
 * - /dashboard (dashboard/page.tsx)
 * - /dashboard/recipes/new (dashboard/recipes/new/page.tsx)
 * - /dashboard/recipes/[id]/edit (dashboard/recipes/[id]/edit/page.tsx)
 * - /bookmarks (bookmarks/page.tsx)
 * - /profile (profile/page.tsx)
 * - /auth/login (auth/login/page.tsx)
 * - /auth/register (auth/register/page.tsx)
 */
function AppContent() {
  const { pathname, navigate } = useApp();

  const renderCurrentView = () => {
    // 1. Home
    if (pathname === '/' || pathname === '') {
      return <HomeView />;
    }

    // 2. Recipes List
    if (pathname === '/recipes') {
      return <RecipesView />;
    }

    // 3. Recipe Detail: /recipes/[slug]
    if (pathname.startsWith('/recipes/')) {
      const slug = pathname.replace('/recipes/', '').split('?')[0];
      return <RecipeDetailView slug={slug} />;
    }

    // 4. Categories List
    if (pathname === '/categories') {
      return <CategoriesView />;
    }

    // 5. Category Detail: /categories/[slug]
    if (pathname.startsWith('/categories/')) {
      const slug = pathname.replace('/categories/', '').split('?')[0];
      return <CategoryDetailView slug={slug} />;
    }

    // 6. Dashboard Create Recipe: /dashboard/recipes/new
    if (pathname === '/dashboard/recipes/new') {
      return <RecipeEditorView />;
    }

    // 7. Dashboard Edit Recipe: /dashboard/recipes/[id]/edit
    if (pathname.startsWith('/dashboard/recipes/') && pathname.endsWith('/edit')) {
      const parts = pathname.split('/');
      // /dashboard/recipes/:id/edit
      const id = parts[3];
      return <RecipeEditorView recipeId={id} />;
    }

    // 8. Dashboard Main: /dashboard or /dashboard/recipes
    if (pathname === '/dashboard' || pathname === '/dashboard/recipes') {
      return <DashboardView />;
    }

    // 9. Bookmarks
    if (pathname === '/bookmarks') {
      return <BookmarksView />;
    }

    // 10. Profile
    if (pathname === '/profile') {
      return <ProfileView />;
    }

    // 11. Auth Login / Register
    if (pathname === '/auth/login') {
      return <AuthView mode="login" />;
    }
    if (pathname === '/auth/register') {
      return <AuthView mode="register" />;
    }

    // Fallback 404
    return (
      <div className="max-w-md mx-auto px-4 py-24 text-center space-y-4">
        <div className="w-16 h-16 rounded-2xl bg-blue-50 text-blue-600 flex items-center justify-center mx-auto">
          <AlertCircle className="w-8 h-8" />
        </div>
        <h2 className="text-2xl font-extrabold text-slate-800">404 - Không Tìm Thấy Trang</h2>
        <p className="text-xs text-slate-500">
          Đường dẫn <code className="text-blue-600 font-mono">{pathname}</code> không tồn tại trong hệ thống.
        </p>
        <button
          onClick={() => navigate('/')}
          className="px-5 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold rounded-xl shadow-xs transition cursor-pointer"
        >
          Trở về Trang chủ
        </button>
      </div>
    );
  };

  return (
    <div className="min-h-screen flex flex-col bg-slate-50 text-slate-900 font-sans selection:bg-blue-500 selection:text-white">
      {/* Top Navigation */}
      <Navbar />

      {/* Main Dynamic View Content */}
      <main className="flex-1 pb-16">
        {renderCurrentView()}
      </main>

      {/* Footer with System Observability Status */}
      <Footer />

      {/* Modals & Overlays */}
      <SearchModal />
      <JsonLdModal />
      <HealthCheckModal />
      <ToastContainer />
      <FloatingKitchenTimer />
    </div>
  );
}

export default function App() {
  return (
    <AppProvider>
      <AppContent />
    </AppProvider>
  );
}
