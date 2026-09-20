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

export interface MenuItem {
  id: string;
  name: string;
  course: CourseCategory;
  price: number;
  description: string;
  culinaryStory: string;
  dietaryTags: DietaryTag[];
  flavorProfile: FlavorProfile;
  pairing: Pairing;
  heroImageSymbol: string;
  rating: number;
  isChefSelection: boolean;
  origin: string;
}

export type DiningArea = 'Main Salon' | "Chef's Bar" | 'Veranda Terrace';

export interface DishFeedback {
  rating: number;
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
