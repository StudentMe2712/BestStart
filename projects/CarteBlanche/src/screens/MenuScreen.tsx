import React, { useState, useMemo } from 'react';
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  Pressable,
  Modal,
  StyleSheet,
  Platform,
} from 'react-native';
import {
  FilterX,
  Sparkles,
  SlidersHorizontal,
  RotateCcw,
  X,
  Check,
} from 'lucide-react-native';
import { Header } from '../components/common/Header';
import {
  CategorySelector,
  CategoryFilterType,
} from '../components/menu/CategorySelector';
import { DishCardV2 } from '../components/menu/DishCardV2';
import { DishDetailModal } from '../components/menu/DishDetailModal';
import { MENU_ITEMS } from '../data/menuDataKZT';
import { MenuItem } from '../types/menu';
import { COLORS, FONTS, RADIUS, SPACING } from '../constants/theme';
import { useCart } from '../context/CartContext';

export interface DietaryFilterItem {
  id: string;
  label: string;
  match: (dish: MenuItem) => boolean;
}

export const DIETARY_FILTERS: DietaryFilterItem[] = [
  {
    id: 'gluten-free',
    label: 'Без глютена',
    match: (dish) =>
      Boolean(
        dish.dietary?.includes('Без глютена') ||
          dish.dietaryTags?.includes('Gluten-Free')
      ),
  },
  {
    id: 'vegan',
    label: 'Веган',
    match: (dish) =>
      Boolean(
        dish.dietary?.includes('Веган') ||
          dish.dietaryTags?.includes('Vegan')
      ),
  },
  {
    id: 'vegetarian',
    label: 'Вегетарианское',
    match: (dish) =>
      Boolean(
        dish.dietary?.includes('Вегетарианское') ||
          dish.dietaryTags?.includes('Vegetarian')
      ),
  },
  {
    id: 'halal',
    label: 'Халяль',
    match: (dish) =>
      Boolean(
        dish.dietary?.includes('Халяль') ||
          dish.dietaryTags?.includes('Halal')
      ),
  },
  {
    id: 'spicy',
    label: 'Острое',
    match: (dish) =>
      Boolean(
        dish.dietary?.includes('Острое') ||
          (dish.flavor?.spiciness ?? 0) >= 0.25 ||
          (dish.flavorProfile?.spiciness ?? 0) >= 0.25 ||
          dish.shortDescription?.toLowerCase().includes('халапеньо') ||
          dish.fullStory?.toLowerCase().includes('халапеньо') ||
          dish.shortDescription?.toLowerCase().includes('остр')
      ),
  },
  {
    id: 'nut-free',
    label: 'Без орехов',
    match: (dish) =>
      Boolean(
        dish.dietary?.includes('Без орехов') ||
          dish.dietaryTags?.includes('Nut-Free') ||
          (!dish.shortDescription?.toLowerCase().includes('орех') &&
            !dish.fullStory?.toLowerCase().includes('орех') &&
            !dish.description?.toLowerCase().includes('орех'))
      ),
  },
  {
    id: 'dairy-free',
    label: 'Без лактозы',
    match: (dish) =>
      Boolean(
        dish.dietary?.includes('Без лактозы') ||
          dish.dietaryTags?.includes('Dairy-Free') ||
          dish.dietary?.includes('Веган') ||
          dish.dietaryTags?.includes('Vegan')
      ),
  },
  {
    id: 'pescatarian',
    label: 'Пескетарианство',
    match: (dish) =>
      Boolean(
        dish.dietary?.includes('Пескетарианство') ||
          dish.dietaryTags?.includes('Pescatarian')
      ),
  },
];

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
  const [isFilterModalVisible, setIsFilterModalVisible] = useState(false);
  const [selectedDietaryTags, setSelectedDietaryTags] = useState<Set<string>>(
    new Set()
  );

  const [selectedDish, setSelectedDish] = useState<MenuItem | null>(null);

  const toggleDietaryTag = (tagId: string) => {
    setSelectedDietaryTags((prev) => {
      const next = new Set(prev);
      if (next.has(tagId)) {
        next.delete(tagId);
      } else {
        next.add(tagId);
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
        for (const tagId of selectedDietaryTags) {
          const filterItem = DIETARY_FILTERS.find((f) => f.id === tagId);
          if (filterItem && !filterItem.match(dish)) {
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
        onFilterPress={() => setIsFilterModalVisible(true)}
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

      {/* 4. Compact Allergen / Dietary BottomSheet Modal */}
      <Modal
        visible={isFilterModalVisible}
        transparent
        animationType="slide"
        onRequestClose={() => setIsFilterModalVisible(false)}
      >
        <View style={styles.modalOverlay}>
          {/* Backdrop dismiss pressable */}
          <Pressable
            style={styles.modalBackdrop}
            onPress={() => setIsFilterModalVisible(false)}
            accessibilityLabel="Закрыть фильтры"
          />

          {/* Dark Luxe Container */}
          <View style={styles.filterModalContainer}>
            {/* Modal Handle Bar */}
            <View style={styles.filterModalHandle} />

            {/* Modal Header */}
            <View style={styles.filterModalHeader}>
              <View style={styles.filterModalTitleRow}>
                <SlidersHorizontal size={18} color="#D4A373" strokeWidth={2.2} />
                <Text style={styles.filterModalTitle}>
                  Диетические предпочтения
                </Text>
              </View>

              <View style={styles.filterModalActions}>
                {selectedDietaryTags.size > 0 && (
                  <TouchableOpacity
                    onPress={() => setSelectedDietaryTags(new Set())}
                    hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}
                    style={styles.filterResetBtn}
                    accessibilityRole="button"
                    accessibilityLabel="Сбросить все фильтры"
                  >
                    <Text style={styles.filterResetText}>Сбросить</Text>
                  </TouchableOpacity>
                )}

                <TouchableOpacity
                  onPress={() => setIsFilterModalVisible(false)}
                  hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}
                  style={styles.filterCloseBtn}
                  accessibilityRole="button"
                  accessibilityLabel="Закрыть окно фильтров"
                >
                  <X size={18} color="#8E95A5" strokeWidth={2.2} />
                </TouchableOpacity>
              </View>
            </View>

            {/* Interactive Grid of Dietary Chips */}
            <View style={styles.filterChipsGrid}>
              {DIETARY_FILTERS.map((chip) => {
                const isSelected = selectedDietaryTags.has(chip.id);
                return (
                  <TouchableOpacity
                    key={chip.id}
                    onPress={() => toggleDietaryTag(chip.id)}
                    style={[
                      styles.filterChip,
                      isSelected && styles.filterChipActive,
                    ]}
                    accessibilityRole="checkbox"
                    accessibilityState={{ checked: isSelected }}
                    accessibilityLabel={chip.label}
                  >
                    {isSelected && (
                      <Check
                        size={13}
                        color="#D4A373"
                        strokeWidth={2.8}
                        style={styles.filterChipCheck}
                      />
                    )}
                    <Text
                      style={[
                        styles.filterChipText,
                        isSelected && styles.filterChipTextActive,
                      ]}
                    >
                      {chip.label}
                    </Text>
                  </TouchableOpacity>
                );
              })}
            </View>

            {/* Apply Button */}
            <TouchableOpacity
              style={styles.filterApplyBtn}
              onPress={() => setIsFilterModalVisible(false)}
              activeOpacity={0.88}
              accessibilityRole="button"
              accessibilityLabel={`Применить фильтры ${
                activeFilterCount > 0 ? `(${activeFilterCount})` : ''
              }`}
            >
              <Text style={styles.filterApplyBtnText}>
                Применить фильтры {activeFilterCount > 0 ? `(${activeFilterCount})` : ''}
              </Text>
            </TouchableOpacity>
          </View>
        </View>
      </Modal>
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
    paddingBottom: 32,
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.76)',
    justifyContent: 'flex-end',
  },
  modalBackdrop: {
    ...StyleSheet.absoluteFill,
  },
  filterModalContainer: {
    backgroundColor: '#13161F',
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
    borderWidth: 1,
    borderColor: '#1E2330',
    padding: 20,
    paddingBottom: Platform.OS === 'ios' ? 36 : 24,
    width: '100%',
    maxWidth: 620,
    alignSelf: 'center',
    ...Platform.select({
      web: {
        boxShadow: '0 -8px 32px rgba(0, 0, 0, 0.65)',
      } as any,
    }),
  },
  filterModalHandle: {
    width: 36,
    height: 4,
    borderRadius: 2,
    backgroundColor: '#2C3242',
    alignSelf: 'center',
    marginBottom: 16,
  },
  filterModalHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 18,
  },
  filterModalTitleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  filterModalTitle: {
    fontFamily: FONTS.serif,
    fontSize: 17,
    fontWeight: '700',
    color: '#F8F9FA',
  },
  filterModalActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },
  filterResetBtn: {
    paddingVertical: 4,
    paddingHorizontal: 6,
  },
  filterResetText: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    color: '#D4A373',
    fontWeight: '600',
  },
  filterCloseBtn: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: '#1E2330',
    alignItems: 'center',
    justifyContent: 'center',
    ...Platform.select({
      web: {
        cursor: 'pointer',
      } as any,
    }),
  },
  filterChipsGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
    marginBottom: 20,
  },
  filterChip: {
    paddingHorizontal: 14,
    paddingVertical: 9,
    borderRadius: 9999,
    backgroundColor: '#0A0B0E',
    borderWidth: 1,
    borderColor: '#1E2330',
    flexDirection: 'row',
    alignItems: 'center',
    ...Platform.select({
      web: {
        cursor: 'pointer',
      } as any,
    }),
  },
  filterChipActive: {
    borderColor: '#D4A373',
    backgroundColor: 'rgba(212, 163, 115, 0.15)',
  },
  filterChipCheck: {
    marginRight: 6,
  },
  filterChipText: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    color: '#8E95A5',
    fontWeight: '500',
  },
  filterChipTextActive: {
    color: '#D4A373',
    fontWeight: '700',
  },
  filterApplyBtn: {
    height: 50,
    borderRadius: 14,
    backgroundColor: '#D4A373',
    alignItems: 'center',
    justifyContent: 'center',
    ...Platform.select({
      web: {
        cursor: 'pointer',
        boxShadow: '0 4px 16px rgba(212, 163, 115, 0.35)',
      } as any,
    }),
  },
  filterApplyBtnText: {
    fontFamily: FONTS.sans,
    fontSize: 15,
    fontWeight: '700',
    color: '#0A0B0E',
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
