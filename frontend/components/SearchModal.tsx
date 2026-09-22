import React, { useState, useEffect, useRef } from 'react';
import { useApp } from '../context/AppContext';
import { Search, X, Clock, ChefHat, ArrowRight, Sparkles, Filter } from 'lucide-react';
import { unaccent } from '../data/mockData';

export default function SearchModal() {
  const { searchModalOpen, setSearchModalOpen, recipes, categories, navigate } = useApp();
  const [query, setQuery] = useState('');
  const [selectedCat, setSelectedCat] = useState<string>('all');
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (searchModalOpen) {
      setTimeout(() => inputRef.current?.focus(), 50);
    } else {
      setQuery('');
      setSelectedCat('all');
    }
  }, [searchModalOpen]);

  // Global keydown listener for Cmd/Ctrl+K and Escape
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setSearchModalOpen(true);
      }
      if (e.key === 'Escape' && searchModalOpen) {
        setSearchModalOpen(false);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [searchModalOpen, setSearchModalOpen]);

  if (!searchModalOpen) return null;

  // Filter with unaccented matching (PostgreSQL tsvector + unaccent emulation - FR-SRCH-001)
  const normalizedQuery = unaccent(query);
  const searchResults = recipes.filter((recipe) => {
    if (recipe.status !== 'Published') return false;
    if (selectedCat !== 'all' && recipe.categoryId !== selectedCat) return false;
    if (!normalizedQuery) return true;

    const titleMatch = unaccent(recipe.title).includes(normalizedQuery);
    const descMatch = unaccent(recipe.description).includes(normalizedQuery);
    const ingredientMatch = recipe.ingredients.some((ing) => unaccent(ing.name).includes(normalizedQuery));
    return titleMatch || descMatch || ingredientMatch;
  }).slice(0, 6);

  const handleSelectRecipe = (slug: string) => {
    setSearchModalOpen(false);
    navigate(`/recipes/${slug}`);
  };

  const handleViewAll = () => {
    setSearchModalOpen(false);
    navigate(`/recipes?q=${encodeURIComponent(query)}&cat=${selectedCat}`);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center pt-16 sm:pt-24 px-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
      <div 
        className="w-full max-w-2xl bg-white rounded-2xl shadow-2xl border border-blue-100 overflow-hidden animate-in zoom-in-95 duration-200"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Input Bar */}
        <div className="relative flex items-center px-4 py-3.5 border-b border-slate-100 bg-blue-50/40">
          <Search className="w-5 h-5 text-blue-600 mr-3 shrink-0" />
          <input
            ref={inputRef}
            type="text"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && searchResults.length > 0) {
                handleSelectRecipe(searchResults[0].slug);
              }
            }}
            placeholder="Tìm món ăn, nguyên liệu (ví dụ: phở bò, thịt ba chỉ, canh chua...)"
            className="w-full text-slate-800 placeholder-slate-400 bg-transparent text-sm sm:text-base font-medium focus:outline-none"
          />
          {query && (
            <button
              onClick={() => setQuery('')}
              className="p-1 hover:bg-slate-200 rounded-full text-slate-400 hover:text-slate-600 mr-2 cursor-pointer"
            >
              <X className="w-4 h-4" />
            </button>
          )}
          <button
            onClick={() => setSearchModalOpen(false)}
            className="p-1.5 hover:bg-slate-200/80 rounded-lg text-slate-400 hover:text-slate-700 text-xs transition cursor-pointer"
          >
            <kbd className="px-1.5 py-0.5 bg-white border border-slate-300 rounded shadow-2xs font-mono text-[10px]">
              ESC
            </kbd>
          </button>
        </div>

        {/* Quick Category Filter Pills */}
        <div className="px-4 py-2 bg-slate-50/70 border-b border-slate-100 flex items-center gap-1.5 overflow-x-auto no-scrollbar text-xs">
          <span className="text-slate-500 font-medium shrink-0 flex items-center gap-1 mr-1">
            <Filter className="w-3 h-3 text-blue-600" />
            Lọc:
          </span>
          <button
            onClick={() => setSelectedCat('all')}
            className={`px-2.5 py-1 rounded-full text-xs font-medium transition shrink-0 cursor-pointer ${
              selectedCat === 'all'
                ? 'bg-blue-600 text-white'
                : 'bg-white text-slate-600 hover:bg-slate-100 border border-slate-200'
            }`}
          >
            Tất cả
          </button>
          {categories.map((c) => (
            <button
              key={c.id}
              onClick={() => setSelectedCat(c.id)}
              className={`px-2.5 py-1 rounded-full text-xs font-medium transition shrink-0 cursor-pointer ${
                selectedCat === c.id
                  ? 'bg-blue-600 text-white'
                  : 'bg-white text-slate-600 hover:bg-slate-100 border border-slate-200'
              }`}
            >
              {c.name}
            </button>
          ))}
        </div>

        {/* Results List */}
        <div className="max-h-[60vh] overflow-y-auto p-2">
          {searchResults.length > 0 ? (
            <div className="space-y-1">
              <div className="px-3 py-1.5 text-[11px] font-semibold text-slate-600 uppercase tracking-wider flex items-center justify-between">
                <span>Kết quả phù hợp ({searchResults.length})</span>
                <span className="text-blue-700 font-normal">Hỗ trợ tìm kiếm không dấu (unaccent)</span>
              </div>
              {searchResults.map((recipe) => (
                <div
                  key={recipe.id}
                  onClick={() => handleSelectRecipe(recipe.slug)}
                  className="flex items-center gap-3 p-2.5 rounded-xl hover:bg-blue-50/80 group transition cursor-pointer border border-transparent hover:border-blue-100"
                >
                  <img
                    src={recipe.images[0]?.thumbnailUrl || recipe.images[0]?.originalUrl}
                    alt={recipe.title}
                    className="w-14 h-14 rounded-lg object-cover shrink-0 border border-slate-200 group-hover:scale-105 transition duration-200"
                  />
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-0.5">
                      <span className="text-[10px] font-semibold px-2 py-0.5 rounded bg-blue-100 text-blue-700">
                        {recipe.difficulty}
                      </span>
                      <span className="text-[11px] text-slate-400 flex items-center gap-1">
                        <Clock className="w-3 h-3 text-slate-400" />
                        {recipe.cookTimeMinutes} phút
                      </span>
                    </div>
                    <h4 className="text-sm font-semibold text-slate-900 group-hover:text-blue-700 truncate transition-colors">
                      {recipe.title}
                    </h4>
                    <p className="text-xs text-slate-500 truncate">
                      {recipe.description}
                    </p>
                  </div>
                  <ArrowRight className="w-4 h-4 text-slate-400 group-hover:text-blue-600 group-hover:translate-x-0.5 transition shrink-0" />
                </div>
              ))}
            </div>
          ) : (
            <div className="py-12 text-center">
              <div className="w-12 h-12 rounded-full bg-blue-50 text-blue-600 flex items-center justify-center mx-auto mb-3">
                <ChefHat className="w-6 h-6" />
              </div>
              <p className="text-sm font-semibold text-slate-700">
                Không tìm thấy công thức nào phù hợp
              </p>
              <p className="text-xs text-slate-500 mt-1 max-w-sm mx-auto">
                Thử tìm với từ khóa chung hơn như "bò", "gà", "canh", "trứng", "bánh"...
              </p>
            </div>
          )}
        </div>

        {/* Footer Actions */}
        {searchResults.length > 0 && (
          <div className="p-3 bg-slate-50 border-t border-slate-100 flex items-center justify-between text-xs">
            <span className="text-slate-500">
              Nhấn <kbd className="px-1.5 py-0.5 bg-white border border-slate-200 rounded font-mono text-[10px]">Enter</kbd> để chọn món đầu tiên
            </span>
            <button
              onClick={handleViewAll}
              className="text-blue-600 hover:text-blue-700 font-semibold flex items-center gap-1 cursor-pointer"
            >
              <span>Xem tất cả kết quả</span>
              <ArrowRight className="w-3.5 h-3.5" />
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
