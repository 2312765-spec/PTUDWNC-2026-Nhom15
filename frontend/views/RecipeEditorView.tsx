import React, { useState, useEffect } from 'react';
import { useApp } from '../context/AppContext';
import { 
  ArrowLeft, 
  Save, 
  Plus, 
  Trash2, 
  Clock, 
  ChefHat, 
  Flame, 
  Users, 
  Sparkles, 
  Layers, 
  Info,
  CheckCircle2,
  AlertCircle
} from 'lucide-react';
import { Recipe, RecipeDifficulty, RecipeIngredient, RecipeStep } from '../types';

interface RecipeEditorViewProps {
  recipeId?: string; // If provided, edit mode; else, create mode
}

export default function RecipeEditorView({ recipeId }: RecipeEditorViewProps) {
  const { recipes, categories, currentUser, createRecipe, updateRecipe, navigate, showToast } = useApp();

  const isEditMode = !!recipeId;
  const existingRecipe = recipes.find((r) => r.id === recipeId);

  // Form states
  const [activeSection, setActiveSection] = useState<'basic' | 'ingredients' | 'steps' | 'nutrition'>('basic');
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [instructions, setInstructions] = useState('');
  const [categoryId, setCategoryId] = useState('');
  const [difficulty, setDifficulty] = useState<RecipeDifficulty>('Medium');
  const [prepTimeMinutes, setPrepTimeMinutes] = useState(20);
  const [cookTimeMinutes, setCookTimeMinutes] = useState(30);
  const [servings, setServings] = useState(4);
  const [coverImageUrl, setCoverImageUrl] = useState('');

  // Ingredients state (FR-RCP-009)
  const [ingredients, setIngredients] = useState<Array<{ name: string; quantity?: number; unit?: string; notes?: string }>>([
    { name: '', quantity: 200, unit: 'gram', notes: '' },
    { name: '', quantity: 1, unit: 'thìa canh', notes: '' },
  ]);

  // Steps state (FR-RCP-010)
  const [steps, setSteps] = useState<Array<{ title: string; description: string; timerMinutes?: number; imageUrl?: string }>>([
    { title: 'Sơ chế nguyên liệu', description: '', timerMinutes: 10 },
    { title: 'Chế biến và nấu chín', description: '', timerMinutes: 20 },
  ]);

  // Nutrition state (Chapter 7.2.1)
  const [calories, setCalories] = useState<number>(450);
  const [protein, setProtein] = useState<number>(28);
  const [carbs, setCarbs] = useState<number>(45);
  const [fat, setFat] = useState<number>(14);
  const [fiber, setFiber] = useState<number>(3);
  const [sodium, setSodium] = useState<number>(650);

  // Populate form if in edit mode
  useEffect(() => {
    if (isEditMode && existingRecipe) {
      setTitle(existingRecipe.title);
      setDescription(existingRecipe.description);
      setInstructions(existingRecipe.instructions || '');
      setCategoryId(existingRecipe.categoryId);
      setDifficulty(existingRecipe.difficulty);
      setPrepTimeMinutes(existingRecipe.prepTimeMinutes);
      setCookTimeMinutes(existingRecipe.cookTimeMinutes);
      setServings(existingRecipe.servings);
      setCoverImageUrl(existingRecipe.images[0]?.originalUrl || '');
      
      if (existingRecipe.ingredients.length > 0) {
        setIngredients(existingRecipe.ingredients.map((ing) => ({
          name: ing.name,
          quantity: ing.quantity,
          unit: ing.unit,
          notes: ing.notes,
        })));
      }

      if (existingRecipe.steps.length > 0) {
        setSteps(existingRecipe.steps.map((st) => ({
          title: st.title,
          description: st.description,
          timerMinutes: st.timerMinutes,
          imageUrl: st.imageUrl,
        })));
      }

      if (existingRecipe.nutrition) {
        setCalories(existingRecipe.nutrition.calories || 0);
        setProtein(existingRecipe.nutrition.protein || 0);
        setCarbs(existingRecipe.nutrition.carbohydrates || 0);
        setFat(existingRecipe.nutrition.fat || 0);
        setFiber(existingRecipe.nutrition.fiber || 0);
        setSodium(existingRecipe.nutrition.sodium || 0);
      }
    } else if (categories.length > 0 && !categoryId) {
      setCategoryId(categories[0].id);
      setCoverImageUrl('https://images.unsplash.com/photo-1546069901-ba9599a7e63c?auto=format&fit=crop&w=1200&q=80');
    }
  }, [isEditMode, existingRecipe, categories]);

  if (!currentUser) {
    return (
      <div className="max-w-md mx-auto px-4 py-20 text-center space-y-4">
        <h2 className="text-xl font-bold text-slate-800">Yêu cầu đăng nhập</h2>
        <p className="text-xs text-slate-500">Bạn cần đăng nhập để tạo hoặc sửa công thức.</p>
        <button
          onClick={() => navigate('/auth/login')}
          className="px-4 py-2 bg-blue-600 text-white rounded-xl text-xs font-semibold cursor-pointer"
        >
          Đăng nhập
        </button>
      </div>
    );
  }

  // Ingredients handlers
  const handleAddIngredient = () => {
    setIngredients((prev) => [...prev, { name: '', quantity: 100, unit: 'gram', notes: '' }]);
  };

  const handleRemoveIngredient = (index: number) => {
    setIngredients((prev) => prev.filter((_, i) => i !== index));
  };

  const handleIngredientChange = (index: number, field: string, value: string | number) => {
    setIngredients((prev) =>
      prev.map((item, i) => (i === index ? { ...item, [field]: value } : item))
    );
  };

  // Steps handlers
  const handleAddStep = () => {
    setSteps((prev) => [
      ...prev,
      { title: `Bước ${prev.length + 1}`, description: '', timerMinutes: 5 },
    ]);
  };

  const handleRemoveStep = (index: number) => {
    setSteps((prev) => prev.filter((_, i) => i !== index));
  };

  const handleStepChange = (index: number, field: string, value: string | number) => {
    setSteps((prev) =>
      prev.map((item, i) => (i === index ? { ...item, [field]: value } : item))
    );
  };

  // Save handler
  const handleSave = async (publishImmediately: boolean = false) => {
    if (!title.trim() || title.trim().length < 5) {
      showToast('Lỗi nhập liệu', 'Tiêu đề công thức phải từ 5 ký tự trở lên (FR-RCP-003).', 'error');
      setActiveSection('basic');
      return;
    }

    const validIngredients: RecipeIngredient[] = ingredients
      .filter((ing) => ing.name.trim().length > 0)
      .map((ing, i) => ({
        id: `ing-${Date.now()}-${i}`,
        recipeId: isEditMode && existingRecipe ? existingRecipe.id : '',
        name: ing.name.trim(),
        quantity: Number(ing.quantity) || undefined,
        unit: ing.unit || '',
        notes: ing.notes || '',
        orderIndex: i + 1,
      }));

    const validSteps: RecipeStep[] = steps
      .filter((st) => st.description.trim().length > 0 || st.title.trim().length > 0)
      .map((st, i) => ({
        id: `step-${Date.now()}-${i}`,
        recipeId: isEditMode && existingRecipe ? existingRecipe.id : '',
        stepNumber: i + 1,
        title: st.title.trim() || `Bước ${i + 1}`,
        description: st.description.trim() || 'Thực hiện theo kinh nghiệm gia đình.',
        timerMinutes: Number(st.timerMinutes) || undefined,
        imageUrl: st.imageUrl?.trim() || undefined,
      }));

    if (publishImmediately && validSteps.length === 0) {
      showToast('Chưa thể xuất bản', 'Quy tắc FR-RCP-005: Công thức phải có ít nhất 1 bước thực hiện.', 'error');
      setActiveSection('steps');
      return;
    }

    if (publishImmediately && validIngredients.length === 0) {
      showToast('Chưa thể xuất bản', 'Quy tắc FR-RCP-005: Công thức phải có ít nhất 1 nguyên liệu.', 'error');
      setActiveSection('ingredients');
      return;
    }

    const payload: Partial<Recipe> = {
      title: title.trim(),
      description: description.trim() || title.trim(),
      instructions: instructions.trim() || description.trim(),
      categoryId: categoryId || categories[0]?.id,
      difficulty,
      prepTimeMinutes: Number(prepTimeMinutes) || 15,
      cookTimeMinutes: Number(cookTimeMinutes) || 30,
      servings: Number(servings) || 4,
      status: publishImmediately ? 'Published' : 'Draft',
      ingredients: validIngredients,
      steps: validSteps,
      nutrition: {
        calories: Number(calories) || 0,
        protein: Number(protein) || 0,
        carbohydrates: Number(carbs) || 0,
        fat: Number(fat) || 0,
        fiber: Number(fiber) || 0,
        sodium: Number(sodium) || 0,
      },
      images: [
        {
          id: `img-cov-${Date.now()}`,
          recipeId: isEditMode && existingRecipe ? existingRecipe.id : '',
          originalUrl: coverImageUrl.trim() || 'https://images.unsplash.com/photo-1546069901-ba9599a7e63c?auto=format&fit=crop&w=1200&q=80',
          isPrimary: true,
          orderIndex: 1,
        },
      ],
    };

    if (isEditMode && existingRecipe) {
      const res = await updateRecipe(existingRecipe.id, payload);
      if (res.success) {
        navigate(`/recipes/${existingRecipe.slug}`);
      }
    } else {
      const res = await createRecipe(payload);
      if (res.success && res.slug) {
        navigate(`/recipes/${res.slug}`);
      }
    }
  };

  return (
    <div className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-8">
      {/* Top action header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-slate-200">
        <div className="flex items-center gap-3">
          <button
            onClick={() => navigate('/dashboard')}
            className="p-2 rounded-xl bg-white hover:bg-slate-100 border border-slate-200 text-slate-600 transition cursor-pointer"
          >
            <ArrowLeft className="w-4 h-4" />
          </button>
          <div>
            <h1 className="text-xl sm:text-2xl font-extrabold text-slate-900">
              {isEditMode ? 'Chỉnh Sửa Công Thức' : 'Đăng Tải Công Thức Mới'}
            </h1>
            <p className="text-xs text-slate-500">
              {isEditMode ? `Cập nhật thông tin ID: ${recipeId}` : 'Mẫu biên soạn theo chuẩn FR-RCP-003 / FR-RCP-009 / FR-RCP-010'}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={() => handleSave(false)}
            className="flex items-center gap-1.5 px-3.5 py-2 rounded-xl bg-white hover:bg-slate-50 border border-slate-300 text-slate-700 text-xs font-semibold shadow-2xs transition cursor-pointer"
          >
            <Save className="w-3.5 h-3.5 text-slate-500" />
            <span>Lưu Bản Nháp (Draft)</span>
          </button>

          <button
            onClick={() => handleSave(true)}
            className="flex items-center gap-1.5 px-4 py-2 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold shadow-md shadow-blue-500/20 transition cursor-pointer"
          >
            <CheckCircle2 className="w-4 h-4" />
            <span>Xuất Bản Ngay (Publish)</span>
          </button>
        </div>
      </div>

      {/* Wizard Steps Tabs */}
      <div className="flex items-center gap-2 p-1.5 bg-slate-100/80 rounded-2xl border border-slate-200 text-xs font-bold overflow-x-auto">
        <button
          onClick={() => setActiveSection('basic')}
          className={`flex-1 min-w-[130px] py-2 px-3 rounded-xl transition cursor-pointer flex items-center justify-center gap-2 ${
            activeSection === 'basic'
              ? 'bg-white text-blue-600 shadow-xs'
              : 'text-slate-600 hover:text-slate-900'
          }`}
        >
          <Info className="w-3.5 h-3.5" />
          <span>1. Thông tin chung</span>
        </button>

        <button
          onClick={() => setActiveSection('ingredients')}
          className={`flex-1 min-w-[130px] py-2 px-3 rounded-xl transition cursor-pointer flex items-center justify-center gap-2 ${
            activeSection === 'ingredients'
              ? 'bg-white text-blue-600 shadow-xs'
              : 'text-slate-600 hover:text-slate-900'
          }`}
        >
          <Layers className="w-3.5 h-3.5" />
          <span>2. Nguyên liệu ({ingredients.length})</span>
        </button>

        <button
          onClick={() => setActiveSection('steps')}
          className={`flex-1 min-w-[130px] py-2 px-3 rounded-xl transition cursor-pointer flex items-center justify-center gap-2 ${
            activeSection === 'steps'
              ? 'bg-white text-blue-600 shadow-xs'
              : 'text-slate-600 hover:text-slate-900'
          }`}
        >
          <ChefHat className="w-3.5 h-3.5" />
          <span>3. Các bước nấu ({steps.length})</span>
        </button>

        <button
          onClick={() => setActiveSection('nutrition')}
          className={`flex-1 min-w-[130px] py-2 px-3 rounded-xl transition cursor-pointer flex items-center justify-center gap-2 ${
            activeSection === 'nutrition'
              ? 'bg-white text-blue-600 shadow-xs'
              : 'text-slate-600 hover:text-slate-900'
          }`}
        >
          <Sparkles className="w-3.5 h-3.5" />
          <span>4. Dinh dưỡng</span>
        </button>
      </div>

      {/* Section 1: Basic info */}
      {activeSection === 'basic' && (
        <div className="bg-white rounded-3xl border border-slate-200/90 p-6 sm:p-8 space-y-6 shadow-xs animate-in fade-in">
          <div className="space-y-4">
            <div>
              <label className="block text-xs font-bold text-slate-800 uppercase tracking-wider mb-1.5">
                Tiêu đề công thức * (Title)
              </label>
              <input
                type="text"
                required
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="Ví dụ: Phở Bò Tái Lăn Hà Nội Chuẩn Vị Gia Truyền"
                className="w-full text-sm font-semibold p-3 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500 focus:outline-none"
              />
              <span className="text-[11px] text-slate-400 mt-1 block">
                Slug URL sẽ được tự động sinh theo chuẩn SEO không dấu (ví dụ: pho-bo-tai-lan...)
              </span>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-bold text-slate-800 uppercase tracking-wider mb-1.5">
                  Danh mục ẩm thực * (Category)
                </label>
                <select
                  value={categoryId}
                  onChange={(e) => setCategoryId(e.target.value)}
                  className="w-full text-xs font-semibold p-3 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500 cursor-pointer"
                >
                  {categories.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-800 uppercase tracking-wider mb-1.5">
                  Độ khó thực hiện (Difficulty)
                </label>
                <select
                  value={difficulty}
                  onChange={(e) => setDifficulty(e.target.value as RecipeDifficulty)}
                  className="w-full text-xs font-semibold p-3 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500 cursor-pointer"
                >
                  <option value="Easy">Dễ (Easy)</option>
                  <option value="Medium">Trung bình (Medium)</option>
                  <option value="Hard">Khó (Hard)</option>
                  <option value="Expert">Chuyên gia (Expert)</option>
                </select>
              </div>
            </div>

            <div className="grid grid-cols-3 gap-4">
              <div>
                <label className="block text-xs font-bold text-slate-800 uppercase tracking-wider mb-1.5">
                  Chuẩn bị (phút)
                </label>
                <input
                  type="number"
                  min="1"
                  value={prepTimeMinutes}
                  onChange={(e) => setPrepTimeMinutes(Number(e.target.value))}
                  className="w-full text-xs font-semibold p-3 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-800 uppercase tracking-wider mb-1.5">
                  Nấu chín (phút)
                </label>
                <input
                  type="number"
                  min="0"
                  value={cookTimeMinutes}
                  onChange={(e) => setCookTimeMinutes(Number(e.target.value))}
                  className="w-full text-xs font-semibold p-3 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-800 uppercase tracking-wider mb-1.5">
                  Khẩu phần (người)
                </label>
                <input
                  type="number"
                  min="1"
                  value={servings}
                  onChange={(e) => setServings(Number(e.target.value))}
                  className="w-full text-xs font-semibold p-3 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500"
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-800 uppercase tracking-wider mb-1.5">
                Mô tả ngắn hấp dẫn (Description - ≤ 2000 ký tự)
              </label>
              <textarea
                rows={3}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Giới thiệu hương vị, nét đặc trưng của món ăn và điểm nhấn ẩm thực..."
                className="w-full text-xs p-3 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500 focus:outline-none"
              ></textarea>
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-800 uppercase tracking-wider mb-1.5">
                Đường dẫn ảnh bìa chính (Cover Image URL - MinIO S3)
              </label>
              <input
                type="url"
                value={coverImageUrl}
                onChange={(e) => setCoverImageUrl(e.target.value)}
                placeholder="https://images.unsplash.com/..."
                className="w-full text-xs p-3 bg-slate-50 border border-slate-200 rounded-xl focus:bg-white focus:ring-2 focus:ring-blue-500"
              />
              {coverImageUrl && (
                <div className="mt-2 h-40 w-full sm:w-64 rounded-xl overflow-hidden border border-slate-200">
                  <img src={coverImageUrl} alt="Preview" className="w-full h-full object-cover" />
                </div>
              )}
            </div>
          </div>

          <div className="flex justify-end pt-4 border-t border-slate-100">
            <button
              type="button"
              onClick={() => setActiveSection('ingredients')}
              className="px-5 py-2 bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold rounded-xl cursor-pointer"
            >
              Tiếp tục: Thêm nguyên liệu →
            </button>
          </div>
        </div>
      )}

      {/* Section 2: Ingredients Builder (FR-RCP-009) */}
      {activeSection === 'ingredients' && (
        <div className="bg-white rounded-3xl border border-slate-200/90 p-6 sm:p-8 space-y-6 shadow-xs animate-in fade-in">
          <div className="flex items-center justify-between pb-3 border-b border-slate-100">
            <div>
              <h3 className="text-sm font-bold text-slate-900">
                Danh sách nguyên liệu (CRUD RecipeIngredient)
              </h3>
              <p className="text-xs text-slate-500">
                Nhập từng loại nguyên liệu kèm định lượng và đơn vị đo lường chính xác
              </p>
            </div>
            <button
              type="button"
              onClick={handleAddIngredient}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-50 hover:bg-blue-100 text-blue-700 text-xs font-bold rounded-lg border border-blue-200 cursor-pointer"
            >
              <Plus className="w-3.5 h-3.5" />
              <span>Thêm dòng</span>
            </button>
          </div>

          <div className="space-y-3">
            {ingredients.map((ing, idx) => (
              <div
                key={idx}
                className="flex items-center gap-2.5 p-2 bg-slate-50 rounded-xl border border-slate-200"
              >
                <span className="w-6 text-center text-xs font-bold text-slate-400">
                  {idx + 1}
                </span>

                <input
                  type="text"
                  placeholder="Tên nguyên liệu (ví dụ: Thịt thăn bò)"
                  value={ing.name}
                  onChange={(e) => handleIngredientChange(idx, 'name', e.target.value)}
                  className="flex-2 p-2 text-xs bg-white border border-slate-200 rounded-lg focus:ring-1 focus:ring-blue-500"
                />

                <input
                  type="number"
                  placeholder="Số lượng"
                  value={ing.quantity || ''}
                  onChange={(e) => handleIngredientChange(idx, 'quantity', Number(e.target.value))}
                  className="w-20 p-2 text-xs bg-white border border-slate-200 rounded-lg focus:ring-1 focus:ring-blue-500 text-center"
                />

                <input
                  type="text"
                  placeholder="Đơn vị (gram, ml, muỗng...)"
                  value={ing.unit || ''}
                  onChange={(e) => handleIngredientChange(idx, 'unit', e.target.value)}
                  className="w-24 p-2 text-xs bg-white border border-slate-200 rounded-lg focus:ring-1 focus:ring-blue-500"
                />

                <input
                  type="text"
                  placeholder="Ghi chú (thái mỏng, băm nhuyễn...)"
                  value={ing.notes || ''}
                  onChange={(e) => handleIngredientChange(idx, 'notes', e.target.value)}
                  className="flex-1 hidden sm:block p-2 text-xs bg-white border border-slate-200 rounded-lg focus:ring-1 focus:ring-blue-500"
                />

                <button
                  type="button"
                  onClick={() => handleRemoveIngredient(idx)}
                  className="p-2 text-slate-400 hover:text-rose-600 rounded-lg transition cursor-pointer"
                  title="Xóa nguyên liệu"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            ))}
          </div>

          <div className="flex justify-between pt-4 border-t border-slate-100">
            <button
              type="button"
              onClick={() => setActiveSection('basic')}
              className="px-4 py-2 bg-slate-100 text-slate-700 text-xs font-semibold rounded-xl cursor-pointer"
            >
              ← Quay lại
            </button>
            <button
              type="button"
              onClick={() => setActiveSection('steps')}
              className="px-5 py-2 bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold rounded-xl cursor-pointer"
            >
              Tiếp tục: Các bước thực hiện →
            </button>
          </div>
        </div>
      )}

      {/* Section 3: Steps Builder (FR-RCP-010) */}
      {activeSection === 'steps' && (
        <div className="bg-white rounded-3xl border border-slate-200/90 p-6 sm:p-8 space-y-6 shadow-xs animate-in fade-in">
          <div className="flex items-center justify-between pb-3 border-b border-slate-100">
            <div>
              <h3 className="text-sm font-bold text-slate-900">
                Các bước thực hiện (CRUD RecipeStep)
              </h3>
              <p className="text-xs text-slate-500">
                Tự động đánh số liên tục (1, 2, 3...) và tích hợp đồng hồ hẹn giờ phút
              </p>
            </div>
            <button
              type="button"
              onClick={handleAddStep}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-50 hover:bg-blue-100 text-blue-700 text-xs font-bold rounded-lg border border-blue-200 cursor-pointer"
            >
              <Plus className="w-3.5 h-3.5" />
              <span>Thêm bước</span>
            </button>
          </div>

          <div className="space-y-4">
            {steps.map((st, idx) => (
              <div
                key={idx}
                className="p-4 bg-slate-50 rounded-2xl border border-slate-200 space-y-3"
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <span className="w-6 h-6 rounded-lg bg-blue-600 text-white font-bold text-xs flex items-center justify-center">
                      {idx + 1}
                    </span>
                    <input
                      type="text"
                      placeholder={`Tiêu đề bước (ví dụ: Sơ chế và ướp thịt)`}
                      value={st.title}
                      onChange={(e) => handleStepChange(idx, 'title', e.target.value)}
                      className="text-xs font-bold p-1.5 bg-white border border-slate-200 rounded-lg w-64 focus:ring-1 focus:ring-blue-500"
                    />
                  </div>

                  <div className="flex items-center gap-2">
                    <div className="flex items-center gap-1 text-xs">
                      <Clock className="w-3.5 h-3.5 text-amber-600" />
                      <input
                        type="number"
                        min="1"
                        placeholder="Phút"
                        value={st.timerMinutes || ''}
                        onChange={(e) => handleStepChange(idx, 'timerMinutes', Number(e.target.value))}
                        className="w-16 p-1 text-xs bg-white border border-slate-200 rounded text-center"
                      />
                      <span className="text-[11px] text-slate-500">phút</span>
                    </div>

                    <button
                      type="button"
                      onClick={() => handleRemoveStep(idx)}
                      className="p-1.5 text-slate-400 hover:text-rose-600 transition cursor-pointer"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </div>
                </div>

                <textarea
                  rows={2}
                  placeholder="Mô tả chi tiết cách làm trong bước này..."
                  value={st.description}
                  onChange={(e) => handleStepChange(idx, 'description', e.target.value)}
                  className="w-full text-xs p-2.5 bg-white border border-slate-200 rounded-xl focus:ring-1 focus:ring-blue-500 focus:outline-none"
                ></textarea>
              </div>
            ))}
          </div>

          <div className="flex justify-between pt-4 border-t border-slate-100">
            <button
              type="button"
              onClick={() => setActiveSection('ingredients')}
              className="px-4 py-2 bg-slate-100 text-slate-700 text-xs font-semibold rounded-xl cursor-pointer"
            >
              ← Quay lại
            </button>
            <button
              type="button"
              onClick={() => setActiveSection('nutrition')}
              className="px-5 py-2 bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold rounded-xl cursor-pointer"
            >
              Tiếp tục: Thông tin dinh dưỡng →
            </button>
          </div>
        </div>
      )}

      {/* Section 4: Nutrition & Summary */}
      {activeSection === 'nutrition' && (
        <div className="bg-white rounded-3xl border border-slate-200/90 p-6 sm:p-8 space-y-6 shadow-xs animate-in fade-in">
          <div className="pb-3 border-b border-slate-100">
            <h3 className="text-sm font-bold text-slate-900">
              Thông tin dinh dưỡng (RecipeNutrition - Cột nhúng bảng Recipes)
            </h3>
            <p className="text-xs text-slate-500">
              Phục vụ hiển thị nhãn năng lượng và thẻ dữ liệu Schema.org Recipe cho SEO
            </p>
          </div>

          <div className="grid grid-cols-2 sm:grid-cols-3 gap-4 text-xs">
            <div>
              <label className="font-bold text-slate-700 block mb-1">Calories (kcal)</label>
              <input
                type="number"
                value={calories}
                onChange={(e) => setCalories(Number(e.target.value))}
                className="w-full p-2.5 border border-slate-200 rounded-xl bg-slate-50 focus:bg-white"
              />
            </div>

            <div>
              <label className="font-bold text-slate-700 block mb-1">Đạm - Protein (gram)</label>
              <input
                type="number"
                value={protein}
                onChange={(e) => setProtein(Number(e.target.value))}
                className="w-full p-2.5 border border-slate-200 rounded-xl bg-slate-50 focus:bg-white"
              />
            </div>

            <div>
              <label className="font-bold text-slate-700 block mb-1">Tinh bột - Carbs (gram)</label>
              <input
                type="number"
                value={carbs}
                onChange={(e) => setCarbs(Number(e.target.value))}
                className="w-full p-2.5 border border-slate-200 rounded-xl bg-slate-50 focus:bg-white"
              />
            </div>

            <div>
              <label className="font-bold text-slate-700 block mb-1">Chất béo - Fat (gram)</label>
              <input
                type="number"
                value={fat}
                onChange={(e) => setFat(Number(e.target.value))}
                className="w-full p-2.5 border border-slate-200 rounded-xl bg-slate-50 focus:bg-white"
              />
            </div>

            <div>
              <label className="font-bold text-slate-700 block mb-1">Chất xơ - Fiber (gram)</label>
              <input
                type="number"
                value={fiber}
                onChange={(e) => setFiber(Number(e.target.value))}
                className="w-full p-2.5 border border-slate-200 rounded-xl bg-slate-50 focus:bg-white"
              />
            </div>

            <div>
              <label className="font-bold text-slate-700 block mb-1">Natri - Sodium (mg)</label>
              <input
                type="number"
                value={sodium}
                onChange={(e) => setSodium(Number(e.target.value))}
                className="w-full p-2.5 border border-slate-200 rounded-xl bg-slate-50 focus:bg-white"
              />
            </div>
          </div>

          <div className="flex justify-between pt-6 border-t border-slate-100">
            <button
              type="button"
              onClick={() => setActiveSection('steps')}
              className="px-4 py-2 bg-slate-100 text-slate-700 text-xs font-semibold rounded-xl cursor-pointer"
            >
              ← Quay lại
            </button>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => handleSave(false)}
                className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-bold rounded-xl cursor-pointer"
              >
                Lưu Bản Nháp
              </button>
              <button
                type="button"
                onClick={() => handleSave(true)}
                className="px-5 py-2 bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold rounded-xl shadow-md cursor-pointer"
              >
                Xuất Bản Hoàn Tất
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
