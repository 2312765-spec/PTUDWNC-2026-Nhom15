import {
  createRecipeSchema,
  emptyRecipeBasics,
  ingredientSchema,
  recipeBasicsSchema,
  stepSchema,
  toCreateRecipeRequest,
  toFormPath,
  toRecipeBasicsRequest,
} from '@/lib/recipe-schema';

const validBasics = {
  ...emptyRecipeBasics,
  title: 'Gỏi cuốn tôm thịt',
  description: 'Món cuốn tươi mát',
  categoryId: 'c1',
  prepTime: '20',
  cookTime: '0',
  servings: '4',
  difficulty: '2' as const,
};

describe('recipe-schema — khớp validator backend (FR-RCP-003/004/010)', () => {
  it('FR-RCP-003/D19: thông tin hợp lệ với cookTime = 0 → pass', () => {
    expect(recipeBasicsSchema.safeParse(validBasics).success).toBe(true);
  });

  it.each([
    ['title', { title: 'Gỏi' }],
    ['title', { title: '   Gỏi    ' }],
    ['description', { description: '  ' }],
    ['categoryId', { categoryId: '' }],
    ['prepTime', { prepTime: '0' }],
    ['prepTime', { prepTime: '' }],
    ['cookTime', { cookTime: '-1' }],
    ['servings', { servings: '1.5' }],
  ])('FR-RCP-003: %s sai → lỗi đúng trường', (field, override) => {
    const result = recipeBasicsSchema.safeParse({ ...validBasics, ...override });
    expect(result.success).toBe(false);
    expect(result.error?.issues.map((i) => i.path[0])).toContain(field);
  });

  it('D7: nguyên liệu không số lượng/đơn vị ("vừa đủ") hợp lệ; số lượng 0 bị chặn; nhận dấu phẩy thập phân', () => {
    expect(ingredientSchema.safeParse({ name: 'Muối', quantity: '', unit: '', notes: 'vừa đủ' }).success).toBe(true);
    expect(ingredientSchema.safeParse({ name: 'Muối', quantity: '0', unit: 'g', notes: '' }).success).toBe(false);
    expect(ingredientSchema.safeParse({ name: 'Bột', quantity: '0,5', unit: 'kg', notes: '' }).success).toBe(true);
  });

  it('D6: bước cần tiêu đề + mô tả; hẹn giờ để trống hoặc số nguyên không âm', () => {
    expect(stepSchema.safeParse({ title: 'Sơ chế', description: 'Rửa', timerMinutes: '' }).success).toBe(true);
    expect(stepSchema.safeParse({ title: '', description: 'Rửa', timerMinutes: '' }).success).toBe(false);
    expect(stepSchema.safeParse({ title: 'A', description: 'B', timerMinutes: '-3' }).success).toBe(false);
  });

  it('FR-RCP-003/D69: body POST — số đổi sang number, chuỗi trống → null, giữ thứ tự steps/ingredients', () => {
    const values = createRecipeSchema.parse({
      ...validBasics,
      title: '  Gỏi cuốn tôm thịt  ',
      instructions: '   ',
      ingredients: [
        { name: 'Tôm', quantity: '0,5', unit: 'kg', notes: '' },
        { name: 'Muối', quantity: '', unit: '', notes: 'vừa đủ' },
      ],
      steps: [
        { title: 'Sơ chế', description: 'Luộc tôm', timerMinutes: '10' },
        { title: 'Cuốn', description: 'Cuốn bánh', timerMinutes: '' },
      ],
    });

    expect(toCreateRecipeRequest(values)).toEqual({
      title: 'Gỏi cuốn tôm thịt',
      description: 'Món cuốn tươi mát',
      categoryId: 'c1',
      prepTime: 20,
      cookTime: 0,
      servings: 4,
      difficulty: 2,
      instructions: null,
      ingredients: [
        { name: 'Tôm', quantity: 0.5, unit: 'kg', notes: null },
        { name: 'Muối', quantity: null, unit: null, notes: 'vừa đủ' },
      ],
      steps: [
        { title: 'Sơ chế', description: 'Luộc tôm', timerMinutes: 10 },
        { title: 'Cuốn', description: 'Cuốn bánh', timerMinutes: null },
      ],
    });
  });

  it('FR-RCP-004/D69: body PUT không có steps/ingredients/nutrition (nutrition vắng = giữ nguyên)', () => {
    const body = toRecipeBasicsRequest(validBasics);
    expect(body).not.toHaveProperty('steps');
    expect(body).not.toHaveProperty('ingredients');
    expect(body).not.toHaveProperty('nutrition');
  });

  it.each([
    ['Title', 'title'],
    ['CategoryId', 'categoryId'],
    ['Steps[0].Title', 'steps.0.title'],
    ['Ingredients[12].Quantity', 'ingredients.12.quantity'],
    ['Nutrition.Calories', 'nutrition.calories'],
  ])('NFR-USE-003: key lỗi backend %s → field form %s', (key, path) => {
    expect(toFormPath(key)).toBe(path);
  });
});
