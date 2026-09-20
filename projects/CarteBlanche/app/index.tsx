import React from 'react';
import {
  View,
  Text,
  ScrollView,
  TextInput,
  Pressable,
  StyleSheet,
  Platform,
} from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import {
  Sparkles,
  Search,
  X,
  ArrowRight,
  UtensilsCrossed,
  FilterX,
} from 'lucide-react-native';
import { DINING_AREAS, ALL_DIETARY_TAGS } from '../src/types/menu';
import {
  COLORS,
  FONTS,
  RADIUS,
  SPACING,
  TYPOGRAPHY,
  SHADOWS,
} from '../src/constants/theme';
import { DietaryBadge } from '../src/components/common/DietaryBadge';
import { CategoryPills } from '../src/components/menu/CategoryPills';
import { DishCard } from '../src/components/menu/DishCard';
import { useTasting } from '../src/context/TastingContext';

export default function MenuHomeScreen() {
  const router = useRouter();
  const {
    selectedArea,
    setSelectedArea,
    searchQuery,
    setSearchQuery,
    selectedDietaryTags,
    toggleDietaryTag,
    clearDietaryTags,
    setSelectedCourse,
    filteredDishes,
    tastingCount,
    tastingSubtotal,
  } = useTasting();

  const handleResetFilters = () => {
    setSearchQuery('');
    clearDietaryTags();
    setSelectedCourse('All');
  };

  return (
    <SafeAreaView style={styles.safeArea} edges={['top', 'bottom']}>
      <ScrollView
        contentContainerStyle={[
          styles.scrollContent,
          tastingCount > 0 && styles.scrollContentWithBar,
        ]}
        showsVerticalScrollIndicator={false}
        keyboardShouldPersistTaps="handled"
      >
        {/* 1. Luxury Brand Header & Dining Area Selector */}
        <View style={styles.header}>
          <View style={styles.headerTopRow}>
            <View style={styles.brandCluster}>
              <Sparkles size={12} color={COLORS.champagneAccent} />
              <Text style={styles.editorialLabel}>HAUTE CUISINE TASTING GUIDE</Text>
            </View>

            {/* Dining Area Selector */}
            <View style={styles.areaSelectorRow}>
              {DINING_AREAS.map((area) => {
                const isActive = selectedArea === area;
                return (
                  <Pressable
                    key={area}
                    onPress={() => setSelectedArea(area)}
                    style={[
                      styles.areaPill,
                      isActive && styles.areaPillActive,
                    ]}
                    accessibilityRole="tab"
                    accessibilityState={{ selected: isActive }}
                  >
                    <Text
                      style={[
                        styles.areaText,
                        isActive && styles.areaTextActive,
                      ]}
                    >
                      {area}
                    </Text>
                  </Pressable>
                );
              })}
            </View>
          </View>

          <Text style={styles.brandTitle}>CARTE BLANCHE</Text>
          <Text style={styles.brandSubtitle}>
            Offline-first sensory tasting menu & sommelier pairings
          </Text>
        </View>

        {/* 2. Search Bar with Glassmorphism */}
        <View style={styles.searchContainer}>
          <View style={styles.searchBar}>
            <Search size={16} color={COLORS.textSecondary} style={styles.searchIcon} />
            <TextInput
              style={styles.searchInput}
              placeholder="Поиск по названию, ингредиентам, терруару..."
              placeholderTextColor="rgba(142, 149, 165, 0.6)"
              value={searchQuery}
              onChangeText={setSearchQuery}
              returnKeyType="search"
              clearButtonMode="never"
            />
            {searchQuery.length > 0 && (
              <Pressable
                onPress={() => setSearchQuery('')}
                style={styles.searchClearBtn}
                hitSlop={8}
                accessibilityLabel="Очистить строку поиска"
              >
                <X size={14} color={COLORS.textSecondary} />
              </Pressable>
            )}
          </View>
        </View>

        {/* 3. Course Category Pills (Horizontal Scroll) */}
        <CategoryPills />

        {/* 4. Dietary & Allergen Filter Bar */}
        <View style={styles.dietaryFilterSection}>
          <View style={styles.dietaryHeaderRow}>
            <Text style={styles.microHeader}>ДИЕТИЧЕСКИЕ ПРЕДПОЧТЕНИЯ</Text>
            {selectedDietaryTags.size > 0 && (
              <Pressable onPress={clearDietaryTags} hitSlop={6}>
                <Text style={styles.clearFilterText}>
                  Сбросить ({selectedDietaryTags.size})
                </Text>
              </Pressable>
            )}
          </View>

          <ScrollView
            horizontal
            showsHorizontalScrollIndicator={false}
            contentContainerStyle={styles.dietaryScroll}
          >
            {ALL_DIETARY_TAGS.map((tag) => {
              const isSelected = selectedDietaryTags.has(tag);
              return (
                <DietaryBadge
                  key={tag}
                  tag={tag}
                  isInteractive
                  isSelected={isSelected}
                  onPress={() => toggleDietaryTag(tag)}
                />
              );
            })}
          </ScrollView>
        </View>

        {/* 5. Dishes List or Empty Results */}
        <View style={styles.dishesSection}>
          <View style={styles.dishesHeaderRow}>
            <Text style={styles.microHeader}>
              ПОЗИЦИИ МЕНЮ ({filteredDishes.length})
            </Text>
          </View>

          {filteredDishes.length === 0 ? (
            <View style={styles.noResultsBox}>
              <FilterX size={32} color={COLORS.textSecondary} />
              <Text style={styles.noResultsTitle}>Ничего не найдено</Text>
              <Text style={styles.noResultsText}>
                По заданным критериям фильтрации блюда не найдены. Попробуйте изменить параметры поиска или диетические метки.
              </Text>
              <Pressable
                onPress={handleResetFilters}
                style={styles.resetFiltersButton}
              >
                <Text style={styles.resetFiltersText}>Сбросить все фильтры</Text>
              </Pressable>
            </View>
          ) : (
            filteredDishes.map((dish) => (
              <DishCard key={dish.id} dish={dish} />
            ))
          )}
        </View>
      </ScrollView>

      {/* 6. Floating Tasting Bar (Bottom Bar) */}
      {tastingCount > 0 && (
        <View style={styles.floatingBarWrapper}>
          <View style={styles.floatingBar}>
            {/* Left Info: Badge Count & Subtotal */}
            <View style={styles.floatingBarLeft}>
              <View style={styles.badgeCircle}>
                <Text style={styles.badgeCountText}>{tastingCount}</Text>
              </View>
              <View style={styles.floatingPriceBox}>
                <Text style={styles.floatingBarMicroLabel}>СЕТ ДЕГУСТАЦИИ</Text>
                <Text style={styles.floatingBarPrice}>€{tastingSubtotal.toFixed(2)}</Text>
              </View>
            </View>

            {/* Right Action: Transition to Planner */}
            <Pressable
              onPress={() => router.push('/planner')}
              style={({ pressed }) => [
                styles.floatingBarButton,
                pressed && styles.floatingButtonPressed,
              ]}
              accessibilityRole="button"
              accessibilityLabel="Открыть дегустационный сет"
            >
              <Text style={styles.floatingBarButtonText}>Дегустационный сет</Text>
              <ArrowRight size={16} color={COLORS.obsidianCanvas} strokeWidth={2.6} />
            </Pressable>
          </View>
        </View>
      )}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safeArea: {
    flex: 1,
    backgroundColor: COLORS.obsidianCanvas,
  },
  scrollContent: {
    paddingBottom: 40,
  },
  scrollContentWithBar: {
    paddingBottom: 110,
  },
  header: {
    paddingHorizontal: SPACING.xl,
    paddingTop: SPACING.lg,
    paddingBottom: SPACING.sm,
  },
  headerTopRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: SPACING.xs,
    flexWrap: 'wrap',
    gap: 8,
  },
  brandCluster: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  editorialLabel: {
    ...TYPOGRAPHY.microLabel,
  },
  areaSelectorRow: {
    flexDirection: 'row',
    gap: 6,
  },
  areaPill: {
    paddingHorizontal: 9,
    paddingVertical: 4,
    borderRadius: RADIUS.full,
    borderWidth: 1,
    borderColor: COLORS.glassBorder,
    backgroundColor: 'rgba(22, 25, 31, 0.7)',
  },
  areaPillActive: {
    borderColor: COLORS.champagneAccent,
    backgroundColor: 'rgba(229, 169, 98, 0.16)',
  },
  areaText: {
    fontSize: 10.5,
    color: COLORS.textSecondary,
    fontFamily: FONTS.sans,
    fontWeight: '500',
  },
  areaTextActive: {
    color: COLORS.champagneAccent,
    fontWeight: '700',
  },
  brandTitle: {
    ...TYPOGRAPHY.largeTitle,
    letterSpacing: 2,
    marginTop: 6,
  },
  brandSubtitle: {
    ...TYPOGRAPHY.subheadline,
    marginTop: 2,
  },
  searchContainer: {
    paddingHorizontal: SPACING.xl,
    marginTop: SPACING.md,
    marginBottom: SPACING.xs,
  },
  searchBar: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(22, 25, 31, 0.85)',
    borderRadius: RADIUS.md,
    borderWidth: 1,
    borderColor: COLORS.glassBorder,
    paddingHorizontal: 12,
    paddingVertical: Platform.OS === 'ios' ? 10 : 6,
  },
  searchIcon: {
    marginRight: 8,
  },
  searchInput: {
    flex: 1,
    fontFamily: FONTS.sans,
    fontSize: 13,
    color: COLORS.textPrimary,
    padding: 0,
  },
  searchClearBtn: {
    padding: 4,
    marginLeft: 6,
  },
  dietaryFilterSection: {
    paddingHorizontal: SPACING.xl,
    marginTop: SPACING.md,
    marginBottom: SPACING.xs,
  },
  dietaryHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: SPACING.xs,
  },
  microHeader: {
    ...TYPOGRAPHY.microLabel,
    color: COLORS.textSecondary,
  },
  clearFilterText: {
    fontFamily: FONTS.mono,
    fontSize: 11,
    color: COLORS.champagneAccent,
    fontWeight: '600',
  },
  dietaryScroll: {
    gap: 8,
    paddingVertical: SPACING.xs,
  },
  dishesSection: {
    paddingHorizontal: SPACING.xl,
    marginTop: SPACING.md,
    gap: SPACING.sm,
  },
  dishesHeaderRow: {
    marginBottom: 4,
  },
  noResultsBox: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: SPACING.xxxl,
    paddingHorizontal: SPACING.lg,
    backgroundColor: 'rgba(22, 25, 31, 0.5)',
    borderRadius: RADIUS.lg,
    borderWidth: 1,
    borderColor: COLORS.glassBorder,
    marginTop: SPACING.sm,
  },
  noResultsTitle: {
    fontFamily: FONTS.serif,
    fontSize: 17,
    fontWeight: '700',
    color: COLORS.textPrimary,
    marginTop: 12,
    marginBottom: 6,
  },
  noResultsText: {
    fontFamily: FONTS.sans,
    fontSize: 12.5,
    lineHeight: 18,
    color: COLORS.textSecondary,
    textAlign: 'center',
    marginBottom: SPACING.lg,
  },
  resetFiltersButton: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: RADIUS.full,
    backgroundColor: 'rgba(229, 169, 98, 0.15)',
    borderWidth: 1,
    borderColor: COLORS.champagneAccent,
  },
  resetFiltersText: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    fontWeight: '700',
    color: COLORS.champagneAccent,
  },
  floatingBarWrapper: {
    position: 'absolute',
    bottom: 0,
    left: 0,
    right: 0,
    paddingHorizontal: SPACING.xl,
    paddingTop: SPACING.sm,
    paddingBottom: Platform.OS === 'ios' ? SPACING.xl : SPACING.md,
    backgroundColor: 'rgba(14, 16, 19, 0.94)',
    borderTopWidth: 1,
    borderTopColor: COLORS.glassBorder,
    ...Platform.select({
      web: {
        backdropFilter: 'blur(20px)',
        WebkitBackdropFilter: 'blur(20px)',
      },
      default: {},
    }),
  },
  floatingBar: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: 'rgba(22, 25, 31, 0.95)',
    borderRadius: RADIUS.full,
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderWidth: 1,
    borderColor: 'rgba(229, 169, 98, 0.35)',
    ...SHADOWS.glowChampagne,
  },
  floatingBarLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  badgeCircle: {
    width: 28,
    height: 28,
    borderRadius: RADIUS.full,
    backgroundColor: COLORS.champagneAccent,
    alignItems: 'center',
    justifyContent: 'center',
  },
  badgeCountText: {
    fontFamily: FONTS.mono,
    fontSize: 13,
    fontWeight: '800',
    color: COLORS.obsidianCanvas,
  },
  floatingPriceBox: {},
  floatingBarMicroLabel: {
    fontFamily: FONTS.mono,
    fontSize: 8.5,
    fontWeight: '700',
    color: COLORS.textSecondary,
    letterSpacing: 1,
  },
  floatingBarPrice: {
    fontFamily: FONTS.mono,
    fontSize: 16,
    fontWeight: '700',
    color: COLORS.champagneAccent,
  },
  floatingBarButton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: COLORS.champagneAccent,
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: RADIUS.full,
  },
  floatingButtonPressed: {
    opacity: 0.85,
    transform: [{ scale: 0.97 }],
  },
  floatingBarButtonText: {
    fontFamily: FONTS.sans,
    fontSize: 12.5,
    fontWeight: '700',
    color: COLORS.obsidianCanvas,
  },
});
