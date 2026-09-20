import React, { useState, useMemo } from 'react';
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  StyleSheet,
  Platform,
} from 'react-native';
import { FilterX, Sparkles, SlidersHorizontal, RotateCcw } from 'lucide-react-native';
import { Header } from '../components/common/Header';
import {
  CategorySelector,
  CategoryFilterType,
} from '../components/menu/CategorySelector';
import { DishCardV2 } from '../components/menu/DishCardV2';
import { DishDetailModal } from '../components/menu/DishDetailModal';
import { MENU_ITEMS } from '../data/menuDataKZT';
import {
  MenuItem,
  ALL_DIETARY_TAGS,
  DietaryTag,
  DIETARY_TAG_LABELS_RU,
} from '../types/menu';
import { COLORS, FONTS, RADIUS, SPACING } from '../constants/theme';
import { useCart } from '../context/CartContext';

export interface MenuScreenProps {
  onNavigateToCart?: () => void;
}

/**
 * MenuScreen (Stage 5)
 * Haute Cuisine Menu Showcase for Carte Blanche V2.
 * Features:
 * - Dynamic Island SafeArea Header with table pill & search
 * - CategorySelector with 5 Dark Luxe tabs
 * - Expandable Dietary Filter chips
 * - Filtered list of DishCardV2 with Unsplash photography & cart stepper
 * - Full-featured DishDetailModal with sensory radar chart & sommelier notes
 */
export const MenuScreen: React.FC<MenuScreenProps> = ({ onNavigateToCart }) => {
  const { tableNumber } = useCart();

  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] =
    useState<CategoryFilterType>('all');
  const [isFilterExpanded, setIsFilterExpanded] = useState(false);
  const [selectedDietaryTags, setSelectedDietaryTags] = useState<Set<DietaryTag>>(
    new Set()
  );

  const [selectedDish, setSelectedDish] = useState<MenuItem | null>(null);

  const toggleDietaryTag = (tag: DietaryTag) => {
    setSelectedDietaryTags((prev) => {
      const next = new Set(prev);
      if (next.has(tag)) {
        next.delete(tag);
      } else {
        next.add(tag);
      }
      return next;
    });
  };

  const clearAllFilters = () => {
    setSearchQuery('');
    setSelectedCategory('all');
    setSelectedDietaryTags(new Set());
  };

  // Filter dishes based on Category, Search Query, and Dietary Tags
  const filteredDishes = useMemo(() => {
    return MENU_ITEMS.filter((dish) => {
      // 1. Category Filter
      if (selectedCategory !== 'all') {
        if (dish.category !== selectedCategory) {
          return false;
        }
      }

      // 2. Search Query
      if (searchQuery.trim().length > 0) {
        const query = searchQuery.toLowerCase().trim();
        const matchesName = dish.name.toLowerCase().includes(query);
        const matchesShortDesc = (dish.shortDescription || dish.description || '')
          .toLowerCase()
          .includes(query);
        const matchesStory = (dish.fullStory || dish.culinaryStory || '')
          .toLowerCase()
          .includes(query);
        const matchesOrigin = (dish.origin || '').toLowerCase().includes(query);
        const matchesDietary = (dish.dietary || [])
          .join(' ')
          .toLowerCase()
          .includes(query);

        if (
          !matchesName &&
          !matchesShortDesc &&
          !matchesStory &&
          !matchesOrigin &&
          !matchesDietary
        ) {
          return false;
        }
      }

      // 3. Dietary Tags Filter
      if (selectedDietaryTags.size > 0) {
        for (const tag of selectedDietaryTags) {
          const ruLabel = DIETARY_TAG_LABELS_RU[tag];
          const hasTag =
            dish.dietaryTags?.includes(tag) ||
            (dish.dietary && dish.dietary.includes(ruLabel)) ||
            (dish.dietary && dish.dietary.includes(tag));

          if (!hasTag) {
            return false;
          }
        }
      }

      return true;
    });
  }, [selectedCategory, searchQuery, selectedDietaryTags]);

  const activeFilterCount = selectedDietaryTags.size;

  return (
    <View style={styles.container}>
      {/* 1. Header with Search and Table info */}
      <Header
        tableNumber={tableNumber || 'Стол №4'}
        searchQuery={searchQuery}
        onSearchChange={setSearchQuery}
        onFilterPress={() => setIsFilterExpanded((prev) => !prev)}
        activeFilterCount={activeFilterCount}
      />

      {/* 2. Scrollable Content */}
      <ScrollView
        showsVerticalScrollIndicator={false}
        contentContainerStyle={styles.scrollContent}
        keyboardShouldPersistTaps="handled"
      >
        {/* Category Horizontal Selector */}
        <CategorySelector
          selectedCategory={selectedCategory}
          onSelectCategory={setSelectedCategory}
        />

        {/* Expandable Dietary Filter Bar */}
        {isFilterExpanded && (
          <View style={styles.dietarySection}>
            <View style={styles.dietaryHeader}>
              <View style={styles.dietaryTitleRow}>
                <SlidersHorizontal size={13} color="#D4A373" />
                <Text style={styles.dietaryTitle}>ДИЕТИЧЕСКИЕ ПРЕДПОЧТЕНИЯ</Text>
              </View>

              {selectedDietaryTags.size > 0 && (
                <TouchableOpacity
                  onPress={() => setSelectedDietaryTags(new Set())}
                  hitSlop={{ top: 6, bottom: 6, left: 6, right: 6 }}
                >
                  <Text style={styles.clearTagsText}>Сбросить</Text>
                </TouchableOpacity>
              )}
            </View>

            <ScrollView
              horizontal
              showsHorizontalScrollIndicator={false}
              contentContainerStyle={styles.dietaryScroll}
            >
              {ALL_DIETARY_TAGS.map((tag) => {
                const isSelected = selectedDietaryTags.has(tag);
                const label = DIETARY_TAG_LABELS_RU[tag] || tag;

                return (
                  <TouchableOpacity
                    key={tag}
                    onPress={() => toggleDietaryTag(tag)}
                    style={[
                      styles.dietaryChip,
                      isSelected && styles.dietaryChipActive,
                    ]}
                    accessibilityRole="checkbox"
                    accessibilityState={{ checked: isSelected }}
                    accessibilityLabel={label}
                  >
                    <Text
                      style={[
                        styles.dietaryChipText,
                        isSelected && styles.dietaryChipTextActive,
                      ]}
                    >
                      {label}
                    </Text>
                  </TouchableOpacity>
                );
              })}
            </ScrollView>
          </View>
        )}

        {/* Dish Count Micro-label */}
        <View style={styles.resultsInfoRow}>
          <Text style={styles.resultsCountText}>
            ПОЗИЦИИ МЕНЮ • НАЙДЕНО {filteredDishes.length}{' '}
            {filteredDishes.length === 1
              ? 'БЛЮДО'
              : filteredDishes.length < 5
              ? 'БЛЮДА'
              : 'БЛЮД'}
          </Text>
        </View>

        {/* Dishes List or Empty State */}
        {filteredDishes.length === 0 ? (
          <View style={styles.emptyContainer}>
            <View style={styles.emptyIconBox}>
              <FilterX size={40} color="#8E95A5" strokeWidth={1.6} />
            </View>
            <Text style={styles.emptyTitle}>Блюда не найдены</Text>
            <Text style={styles.emptySubtitle}>
              Попробуйте изменить категорию, очистить строку поиска или сбросить
              диfilterтические фильтры.
            </Text>
            <TouchableOpacity
              style={styles.resetBtn}
              onPress={clearAllFilters}
              accessibilityRole="button"
              accessibilityLabel="Сбросить все фильтры"
            >
              <RotateCcw size={15} color="#0A0B0E" strokeWidth={2.4} />
              <Text style={styles.resetBtnText}>Сбросить все фильтры</Text>
            </TouchableOpacity>
          </View>
        ) : (
          filteredDishes.map((dish) => (
            <DishCardV2
              key={dish.id}
              item={dish}
              onPress={(item) => setSelectedDish(item)}
            />
          ))
        )}
      </ScrollView>

      {/* 3. Dish Detail Modal */}
      <DishDetailModal
        item={selectedDish}
        visible={selectedDish !== null}
        onClose={() => setSelectedDish(null)}
      />
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0A0B0E',
  },
  scrollContent: {
    paddingHorizontal: 16,
    paddingTop: 8,
    paddingBottom: 36,
  },
  dietarySection: {
    backgroundColor: '#13161F',
    borderWidth: 1,
    borderColor: '#1E2330',
    borderRadius: 14,
    padding: 12,
    marginTop: 6,
    marginBottom: 8,
    gap: 10,
  },
  dietaryHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  dietaryTitleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  dietaryTitle: {
    fontFamily: FONTS.mono,
    fontSize: 10,
    fontWeight: '700',
    color: '#D4A373',
    letterSpacing: 1,
  },
  clearTagsText: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    color: '#8E95A5',
    fontWeight: '600',
  },
  dietaryScroll: {
    gap: 8,
    paddingVertical: 2,
  },
  dietaryChip: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 9999,
    backgroundColor: '#0A0B0E',
    borderWidth: 1,
    borderColor: '#1E2330',
    ...Platform.select({
      web: {
        cursor: 'pointer',
      } as any,
    }),
  },
  dietaryChipActive: {
    borderColor: '#D4A373',
    backgroundColor: 'rgba(212, 163, 115, 0.15)',
  },
  dietaryChipText: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    color: '#8E95A5',
    fontWeight: '500',
  },
  dietaryChipTextActive: {
    color: '#D4A373',
    fontWeight: '700',
  },
  resultsInfoRow: {
    marginVertical: 10,
  },
  resultsCountText: {
    fontFamily: FONTS.mono,
    fontSize: 10.5,
    fontWeight: '700',
    color: '#8E95A5',
    letterSpacing: 1,
  },
  emptyContainer: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 48,
    paddingHorizontal: 24,
    backgroundColor: '#13161F',
    borderWidth: 1,
    borderColor: '#1E2330',
    borderRadius: 18,
    marginTop: 12,
    gap: 12,
  },
  emptyIconBox: {
    width: 72,
    height: 72,
    borderRadius: 36,
    backgroundColor: '#16191F',
    alignItems: 'center',
    justifyContent: 'center',
  },
  emptyTitle: {
    fontFamily: FONTS.serif,
    fontSize: 18,
    fontWeight: '700',
    color: '#F8F9FA',
  },
  emptySubtitle: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    color: '#8E95A5',
    textAlign: 'center',
    lineHeight: 19,
    maxWidth: 280,
  },
  resetBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: '#D4A373',
    paddingHorizontal: 18,
    paddingVertical: 10,
    borderRadius: 12,
    marginTop: 6,
    ...Platform.select({
      web: {
        cursor: 'pointer',
      } as any,
    }),
  },
  resetBtnText: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    fontWeight: '700',
    color: '#0A0B0E',
  },
});

export default MenuScreen;
