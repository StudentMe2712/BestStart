import React from 'react';
import {
  ScrollView,
  Pressable,
  Text,
  StyleSheet,
  View,
  StyleProp,
  ViewStyle,
} from 'react-native';
import {
  Sparkles,
  Flame,
  Leaf,
  Cake,
  Wine,
  LucideIcon,
} from 'lucide-react-native';
import { CourseCategory, ALL_COURSE_CATEGORIES } from '../../types/menu';
import { COLORS, FONTS, RADIUS, SPACING, SHADOWS } from '../../constants/theme';
import { useTasting } from '../../context/TastingContext';
import { SEED_MENU } from '../../data/seedMenu';

export interface CategoryPillsProps {
  selectedCourse?: CourseCategory | 'All';
  onSelectCourse?: (course: CourseCategory | 'All') => void;
  showCounts?: boolean;
  style?: StyleProp<ViewStyle>;
}

interface CategoryItem {
  id: CourseCategory | 'All';
  label: string;
  icon: LucideIcon;
}

const CATEGORIES: CategoryItem[] = [
  { id: 'All', label: 'All Courses', icon: Sparkles },
  { id: 'Prelude', label: 'Prelude', icon: Sparkles },
  { id: 'Main Courses', label: 'Main Courses', icon: Flame },
  { id: 'Garden', label: 'Garden', icon: Leaf },
  { id: 'Desserts & Fromage', label: 'Desserts & Fromage', icon: Cake },
  { id: 'Cellar', label: 'Cellar', icon: Wine },
];

export const CategoryPills: React.FC<CategoryPillsProps> = ({
  selectedCourse: propSelectedCourse,
  onSelectCourse: propOnSelectCourse,
  showCounts = true,
  style,
}) => {
  const context = useTasting();
  const activeCourse = propSelectedCourse ?? context.selectedCourse;
  const handleSelect = propOnSelectCourse ?? context.setSelectedCourse;

  const getDishCount = (categoryId: CourseCategory | 'All'): number => {
    if (categoryId === 'All') return SEED_MENU.length;
    return SEED_MENU.filter((dish) => dish.course === categoryId).length;
  };

  return (
    <View style={[styles.wrapper, style]}>
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={styles.scrollContainer}
      >
        {CATEGORIES.map((item) => {
          const isActive = activeCourse === item.id;
          const IconComponent = item.icon;
          const count = getDishCount(item.id);

          return (
            <Pressable
              key={item.id}
              onPress={() => handleSelect(item.id)}
              accessibilityRole="tab"
              accessibilityState={{ selected: isActive }}
              accessibilityLabel={`${item.label} course category`}
              style={({ pressed }) => [
                styles.pill,
                isActive ? styles.pillActive : styles.pillInactive,
                isActive && SHADOWS.glowChampagne,
                pressed && styles.pillPressed,
              ]}
            >
              <IconComponent
                size={14}
                color={isActive ? COLORS.obsidianCanvas : COLORS.champagneAccent}
                strokeWidth={isActive ? 2.5 : 2}
              />
              <Text
                style={[
                  styles.pillText,
                  isActive ? styles.pillTextActive : styles.pillTextInactive,
                ]}
              >
                {item.label}
              </Text>
              {showCounts && (
                <View
                  style={[
                    styles.countBadge,
                    isActive ? styles.countBadgeActive : styles.countBadgeInactive,
                  ]}
                >
                  <Text
                    style={[
                      styles.countText,
                      isActive ? styles.countTextActive : styles.countTextInactive,
                    ]}
                  >
                    {count}
                  </Text>
                </View>
              )}
            </Pressable>
          );
        })}
      </ScrollView>
    </View>
  );
};

const styles = StyleSheet.create({
  wrapper: {
    marginVertical: SPACING.xs,
  },
  scrollContainer: {
    paddingHorizontal: SPACING.xl,
    gap: 8,
    alignItems: 'center',
    paddingVertical: SPACING.xs,
  },
  pill: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 14,
    paddingVertical: 9,
    borderRadius: RADIUS.full,
    gap: 7,
    borderWidth: 1,
  },
  pillActive: {
    backgroundColor: COLORS.champagneAccent,
    borderColor: COLORS.champagneAccent,
  },
  pillInactive: {
    backgroundColor: 'rgba(22, 25, 31, 0.85)',
    borderColor: COLORS.glassBorder,
  },
  pillPressed: {
    opacity: 0.85,
    transform: [{ scale: 0.98 }],
  },
  pillText: {
    fontSize: 13,
    letterSpacing: 0.3,
  },
  pillTextActive: {
    fontFamily: FONTS.sans,
    fontWeight: '700',
    color: COLORS.obsidianCanvas,
  },
  pillTextInactive: {
    fontFamily: FONTS.sans,
    fontWeight: '500',
    color: COLORS.textSecondary,
  },
  countBadge: {
    paddingHorizontal: 6,
    paddingVertical: 1,
    borderRadius: RADIUS.full,
    minWidth: 18,
    alignItems: 'center',
    justifyContent: 'center',
  },
  countBadgeActive: {
    backgroundColor: 'rgba(14, 16, 19, 0.2)',
  },
  countBadgeInactive: {
    backgroundColor: 'rgba(255, 255, 255, 0.07)',
  },
  countText: {
    fontSize: 11,
    fontFamily: FONTS.mono,
    fontWeight: '600',
  },
  countTextActive: {
    color: COLORS.obsidianCanvas,
  },
  countTextInactive: {
    color: COLORS.textSecondary,
  },
});

export default CategoryPills;
