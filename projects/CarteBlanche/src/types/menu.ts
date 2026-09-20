/**
 * Type definitions for Carte Blanche Luxe Menu & Tasting Guide.
 * Includes V2 models (KZT, Unsplash photography, revised categorization)
 * as well as backwards compatibility for V1 components.
 */

// --- V2 Haute Cuisine Categories ---
export type MenuCategory = 'starters' | 'mains' | 'desserts' | 'drinks';

export interface MenuItemPairing {
  title: string;
  type: string;
  description: string;
  // Optional legacy sommelier fields
  id?: string;
  name?: string;
  notes?: string;
  temperature?: string;
  producer?: string;
  vintage?: string;
}

export interface MenuItemFlavor {
  umami: number;      // 0.0 - 1.0
  acidity: number;    // 0.0 - 1.0
  sweetness: number;  // 0.0 - 1.0
  spiciness: number;  // 0.0 - 1.0
  crispness: number;  // 0.0 - 1.0
  // Optional legacy axis
  texture?: number;   // 0.0 - 1.0
}

/**
 * Primary domain model for V2 Menu Dish.
 */
export interface MenuItem {
  id: string;
  name: string;
  category: 'starters' | 'mains' | 'desserts' | 'drinks';
  categoryTitle: string;
  priceKZT: number;              // в тенге, например 12500
  weight: string;                // "280 г"
  image: string;                 // Прямой URL качественного фото еды с Unsplash
  shortDescription: string;
  fullStory: string;
  isChefChoice?: boolean;
  dietary: string[];             // ["Без глютена", "Острое"]
  pairing: MenuItemPairing;
  flavor: MenuItemFlavor;

  // --- V1 Backwards Compatibility Fields ---
  course: CourseCategory;
  price: number;
  description: string;
  culinaryStory: string;
  dietaryTags: DietaryTag[];
  flavorProfile: FlavorProfile;
  heroImageSymbol: string;
  rating: number;
  isChefSelection: boolean;
  origin: string;
}

// --- Legacy V1 Course Categories & Domain Models ---
export type CourseCategory =
  | 'Prelude'
  | 'Main Courses'
  | 'Garden'
  | 'Desserts & Fromage'
  | 'Cellar';

export type DietaryTag =
  | 'Gluten-Free'
  | 'Vegan'
  | 'Vegetarian'
  | 'Halal'
  | 'Nut-Free'
  | 'Dairy-Free'
  | 'Pescatarian';

export interface FlavorProfile {
  umami: number;      // 0.0 - 1.0
  acidity: number;    // 0.0 - 1.0
  sweetness: number;  // 0.0 - 1.0
  spiciness: number;  // 0.0 - 1.0
  texture: number;    // 0.0 - 1.0
}

export interface Pairing {
  id: string;
  name: string;
  type: string;
  notes: string;
  temperature: string;
  producer: string;
  vintage: string;
}

export type DiningArea = 'Main Salon' | "Chef's Bar" | 'Veranda Terrace';

export interface DishFeedback {
  rating: number;     // 1 - 5
  notes: string;
  updatedAt: string;
}

export const ALL_COURSE_CATEGORIES: CourseCategory[] = [
  'Prelude',
  'Main Courses',
  'Garden',
  'Desserts & Fromage',
  'Cellar',
];

export const ALL_DIETARY_TAGS: DietaryTag[] = [
  'Gluten-Free',
  'Vegan',
  'Vegetarian',
  'Halal',
  'Nut-Free',
  'Dairy-Free',
  'Pescatarian',
];

export const DINING_AREAS: DiningArea[] = [
  'Main Salon',
  "Chef's Bar",
  'Veranda Terrace',
];

export const CATEGORY_LABELS_V2: Record<MenuCategory, string> = {
  starters: 'Закуски',
  mains: 'Основные блюда',
  desserts: 'Десерты',
  drinks: 'Винный погреб & Бар',
};

export const COURSE_CATEGORY_LABELS_RU: Record<CourseCategory, string> = {
  'Prelude': 'Прелюдия • Закуски',
  'Main Courses': 'Основные подачи',
  'Garden': 'Ботаника и сад',
  'Desserts & Fromage': 'Десерты и сыры',
  'Cellar': 'Винный погреб и бар',
};

export const COURSE_CATEGORY_SHORT_RU: Record<CourseCategory, string> = {
  'Prelude': 'Закуски',
  'Main Courses': 'Основные',
  'Garden': 'Ботаника',
  'Desserts & Fromage': 'Десерты',
  'Cellar': 'Винный погреб',
};

export const DIETARY_TAG_LABELS_RU: Record<DietaryTag, string> = {
  'Gluten-Free': 'Без глютена',
  'Vegan': 'Веган',
  'Vegetarian': 'Вегетарианское',
  'Halal': 'Халяль',
  'Nut-Free': 'Без орехов',
  'Dairy-Free': 'Без лактозы',
  'Pescatarian': 'Пескетарианство',
};

export const DINING_AREA_LABELS_RU: Record<DiningArea, string> = {
  'Main Salon': 'Главный зал',
  "Chef's Bar": 'Бар шефа',
  'Veranda Terrace': 'Панорамная веранда',
};

export interface CartItem {
  menuItem: MenuItem;
  quantity: number;
}
