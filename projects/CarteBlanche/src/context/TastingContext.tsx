import React, {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
  useMemo,
  ReactNode,
} from 'react';
import { Platform } from 'react-native';
import {
  MenuItem,
  CourseCategory,
  DietaryTag,
  DiningArea,
  DishFeedback,
} from '../types/menu';
import { SEED_MENU } from '../data/seedMenu';

const STORAGE_KEYS = {
  TASTING_SET: 'carte_blanche_tasting_set_v1',
  FEEDBACKS: 'carte_blanche_dish_feedbacks_v1',
  AREA: 'carte_blanche_selected_area_v1',
};

export interface TastingContextType {
  // Tasting Flight State & Actions
  tastingSet: MenuItem[];
  addToTastingSet: (dish: MenuItem) => void;
  removeFromTastingSet: (dishId: string) => void;
  isInTastingSet: (dishId: string) => boolean;
  clearTastingSet: () => void;

  // Guest Ratings & Tasting Notes
  feedbacks: Record<string, DishFeedback>;
  setDishFeedback: (dishId: string, rating: number, notes: string) => void;

  // Menu Filters & Search
  selectedCourse: CourseCategory | 'All';
  setSelectedCourse: (course: CourseCategory | 'All') => void;
  searchQuery: string;
  setSearchQuery: (query: string) => void;
  selectedDietaryTags: Set<DietaryTag>;
  toggleDietaryTag: (tag: DietaryTag) => void;
  clearDietaryTags: () => void;
  selectedArea: DiningArea;
  setSelectedArea: (area: DiningArea) => void;

  // Computed Properties
  filteredDishes: MenuItem[];
  tastingSubtotal: number;
  tastingCount: number;
}

const TastingContext = createContext<TastingContextType | undefined>(undefined);

// Safe storage access for Web & fallback
const getStorageItem = (key: string): string | null => {
  if (Platform.OS === 'web' && typeof window !== 'undefined' && window.localStorage) {
    try {
      return window.localStorage.getItem(key);
    } catch {
      return null;
    }
  }
  return null;
};

const setStorageItem = (key: string, value: string): void => {
  if (Platform.OS === 'web' && typeof window !== 'undefined' && window.localStorage) {
    try {
      window.localStorage.setItem(key, value);
    } catch {
      // Storage quota or disabled
    }
  }
};

export interface TastingProviderProps {
  children: ReactNode;
}

export const TastingProvider: React.FC<TastingProviderProps> = ({ children }) => {
  // Initialize state from offline storage if available
  const [tastingSet, setTastingSet] = useState<MenuItem[]>(() => {
    const raw = getStorageItem(STORAGE_KEYS.TASTING_SET);
    if (raw) {
      try {
        const parsedIds = JSON.parse(raw) as string[];
        if (Array.isArray(parsedIds)) {
          const restored = parsedIds
            .map((id) => SEED_MENU.find((dish) => dish.id === id))
            .filter((dish): dish is MenuItem => Boolean(dish));
          if (restored.length > 0) return restored;
        }
      } catch {
        // Parse error fallback
      }
    }
    // Initial default: 2 highlight chef selection dishes for immediate elegance
    return [SEED_MENU[0], SEED_MENU[1]].filter(Boolean);
  });

  const [feedbacks, setFeedbacks] = useState<Record<string, DishFeedback>>(() => {
    const raw = getStorageItem(STORAGE_KEYS.FEEDBACKS);
    if (raw) {
      try {
        const parsed = JSON.parse(raw);
        if (parsed && typeof parsed === 'object') {
          return parsed as Record<string, DishFeedback>;
        }
      } catch {
        // Parse error fallback
      }
    }
    return {};
  });

  const [selectedCourse, setSelectedCourse] = useState<CourseCategory | 'All'>('All');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [selectedDietaryTags, setSelectedDietaryTags] = useState<Set<DietaryTag>>(new Set());
  const [selectedArea, setSelectedArea] = useState<DiningArea>(() => {
    const raw = getStorageItem(STORAGE_KEYS.AREA);
    if (raw && (raw === 'Main Salon' || raw === "Chef's Bar" || raw === 'Veranda Terrace')) {
      return raw as DiningArea;
    }
    return 'Main Salon';
  });

  // Sync state changes with storage
  useEffect(() => {
    const ids = tastingSet.map((d) => d.id);
    setStorageItem(STORAGE_KEYS.TASTING_SET, JSON.stringify(ids));
  }, [tastingSet]);

  useEffect(() => {
    setStorageItem(STORAGE_KEYS.FEEDBACKS, JSON.stringify(feedbacks));
  }, [feedbacks]);

  useEffect(() => {
    setStorageItem(STORAGE_KEYS.AREA, selectedArea);
  }, [selectedArea]);

  // Tasting Set Operations
  const addToTastingSet = useCallback((dish: MenuItem) => {
    setTastingSet((prev) => {
      if (prev.some((item) => item.id === dish.id)) {
        return prev;
      }
      return [...prev, dish];
    });
  }, []);

  const removeFromTastingSet = useCallback((dishId: string) => {
    setTastingSet((prev) => prev.filter((item) => item.id !== dishId));
  }, []);

  const isInTastingSet = useCallback(
    (dishId: string): boolean => {
      return tastingSet.some((item) => item.id === dishId);
    },
    [tastingSet]
  );

  const clearTastingSet = useCallback(() => {
    setTastingSet([]);
  }, []);

  // Guest Feedback Operations
  const setDishFeedback = useCallback(
    (dishId: string, rating: number, notes: string) => {
      setFeedbacks((prev) => ({
        ...prev,
        [dishId]: {
          rating,
          notes,
          updatedAt: new Date().toISOString(),
        },
      }));
    },
    []
  );

  // Dietary Tag Toggle & Clear
  const toggleDietaryTag = useCallback((tag: DietaryTag) => {
    setSelectedDietaryTags((prev) => {
      const next = new Set(prev);
      if (next.has(tag)) {
        next.delete(tag);
      } else {
        next.add(tag);
      }
      return next;
    });
  }, []);

  const clearDietaryTags = useCallback(() => {
    setSelectedDietaryTags(new Set());
  }, []);

  // Computed Filtered Dishes
  const filteredDishes = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();

    return SEED_MENU.filter((dish) => {
      // 1. Course category filter
      if (selectedCourse !== 'All' && dish.course !== selectedCourse) {
        return false;
      }

      // 2. Search query across name, description, origin
      if (query.length > 0) {
        const matchesName = dish.name.toLowerCase().includes(query);
        const matchesDescription = dish.description.toLowerCase().includes(query);
        const matchesOrigin = dish.origin.toLowerCase().includes(query);
        if (!matchesName && !matchesDescription && !matchesOrigin) {
          return false;
        }
      }

      // 3. Dietary tags / allergen filter (must satisfy ALL selected tags)
      if (selectedDietaryTags.size > 0) {
        for (const tag of selectedDietaryTags) {
          if (!dish.dietaryTags.includes(tag)) {
            return false;
          }
        }
      }

      return true;
    });
  }, [selectedCourse, searchQuery, selectedDietaryTags]);

  // Computed Subtotal & Count
  const tastingSubtotal = useMemo(() => {
    return tastingSet.reduce((acc, dish) => acc + dish.price, 0);
  }, [tastingSet]);

  const tastingCount = tastingSet.length;

  const value = useMemo<TastingContextType>(
    () => ({
      tastingSet,
      addToTastingSet,
      removeFromTastingSet,
      isInTastingSet,
      clearTastingSet,
      feedbacks,
      setDishFeedback,
      selectedCourse,
      setSelectedCourse,
      searchQuery,
      setSearchQuery,
      selectedDietaryTags,
      toggleDietaryTag,
      clearDietaryTags,
      selectedArea,
      setSelectedArea,
      filteredDishes,
      tastingSubtotal,
      tastingCount,
    }),
    [
      tastingSet,
      addToTastingSet,
      removeFromTastingSet,
      isInTastingSet,
      clearTastingSet,
      feedbacks,
      setDishFeedback,
      selectedCourse,
      searchQuery,
      selectedDietaryTags,
      toggleDietaryTag,
      clearDietaryTags,
      selectedArea,
      filteredDishes,
      tastingSubtotal,
      tastingCount,
    ]
  );

  return <TastingContext.Provider value={value}>{children}</TastingContext.Provider>;
};

export const useTasting = (): TastingContextType => {
  const context = useContext(TastingContext);
  if (!context) {
    throw new Error('useTasting must be used within a TastingProvider');
  }
  return context;
};

export default TastingContext;
