export type Role = 'Guest' | 'Author' | 'Admin';

export type RecipeDifficulty = 'Easy' | 'Medium' | 'Hard' | 'Expert';

export type RecipeStatus = 'Draft' | 'Published' | 'Archived';

export interface ApplicationUser {
  id: string;
  email: string;
  displayName: string;
  avatarUrl: string;
  bio?: string;
  role: Role;
  isActive: boolean;
  createdAt: string;
}

export interface RecipeNutrition {
  calories?: number;
  protein?: number;
  carbohydrates?: number;
  fat?: number;
  fiber?: number;
  sodium?: number;
}

export interface RecipeIngredient {
  id: string;
  recipeId: string;
  name: string;
  quantity?: number;
  unit?: string;
  notes?: string;
  orderIndex: number;
}

export interface RecipeStep {
  id: string;
  recipeId: string;
  stepNumber: number;
  title: string;
  description: string;
  timerMinutes?: number;
  imageUrl?: string;
}

export interface RecipeImage {
  id: string;
  recipeId: string;
  originalUrl: string;
  mediumUrl?: string;
  thumbnailUrl?: string;
  altText?: string;
  isPrimary: boolean;
  orderIndex: number;
}

export interface Category {
  id: string;
  name: string;
  slug: string;
  description: string;
  imageUrl?: string;
  recipeCount: number;
  orderIndex: number;
}

export interface Recipe {
  id: string;
  title: string;
  slug: string;
  description: string;
  instructions: string;
  prepTimeMinutes: number;
  cookTimeMinutes: number;
  servings: number;
  difficulty: RecipeDifficulty;
  status: RecipeStatus;
  categoryId: string;
  authorId: string;
  author?: ApplicationUser;
  category?: Category;
  publishedAt?: string;
  createdAt: string;
  updatedAt?: string;
  nutrition: RecipeNutrition;
  steps: RecipeStep[];
  ingredients: RecipeIngredient[];
  images: RecipeImage[];
  viewCount: number;
  likeCount: number;
}

export interface RecipeFilterParams {
  searchTerm?: string;
  categoryId?: string;
  difficulty?: RecipeDifficulty | 'All';
  maxCookTime?: number;
  sort?: '-createdAt' | 'createdAt' | 'title' | 'cookTime';
  page?: number;
  pageSize?: number;
}

export interface SystemHealth {
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  timestamp: string;
  database: { status: 'Healthy' | 'Unhealthy'; latencyMs: number };
  redis: { status: 'Healthy' | 'Unhealthy'; latencyMs: number };
  minio: { status: 'Healthy' | 'Unhealthy'; latencyMs: number };
  hangfire: { status: 'Healthy' | 'Unhealthy'; activeWorkers: number; queuedJobs: number };
}
