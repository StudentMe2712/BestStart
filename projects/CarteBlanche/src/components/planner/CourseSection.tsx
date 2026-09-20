import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  TextInput,
  Pressable,
  StyleSheet,
  StyleProp,
  ViewStyle,
} from 'react-native';
import { Star, Trash2, Sparkles, ChefHat } from 'lucide-react-native';
import { MenuItem, CourseCategory, DishFeedback } from '../../types/menu';
import {
  COLORS,
  FONTS,
  RADIUS,
  SPACING,
  TYPOGRAPHY,
} from '../../constants/theme';
import { GlassCard } from '../common/GlassCard';
import { useTasting } from '../../context/TastingContext';

export interface CourseSectionProps {
  courseName: CourseCategory;
  stepNumber: number;
  dishes: MenuItem[];
  style?: StyleProp<ViewStyle>;
}

const COURSE_STEP_TITLES: Record<CourseCategory, string> = {
  'Prelude': 'Закуски',
  'Main Courses': 'Основной курс',
  'Garden': 'Ботаника',
  'Desserts & Fromage': 'Десерты',
  'Cellar': 'Погреб',
};

interface DishItemRowProps {
  dish: MenuItem;
  feedback?: DishFeedback;
  onRemove: (dishId: string) => void;
  onUpdateFeedback: (dishId: string, rating: number, notes: string) => void;
}

const DishItemRow: React.FC<DishItemRowProps> = ({
  dish,
  feedback,
  onRemove,
  onUpdateFeedback,
}) => {
  const currentRating = feedback?.rating || 0;
  const [localNotes, setLocalNotes] = useState<string>(feedback?.notes || '');

  // Keep local notes synchronized if external feedback changes
  useEffect(() => {
    setLocalNotes(feedback?.notes || '');
  }, [feedback?.notes]);

  const handleStarPress = (starIndex: number) => {
    const newRating = currentRating === starIndex ? 0 : starIndex;
    onUpdateFeedback(dish.id, newRating, localNotes);
  };

  const handleNotesChange = (text: string) => {
    setLocalNotes(text);
    onUpdateFeedback(dish.id, currentRating, text);
  };

  return (
    <GlassCard
      cornerRadius={RADIUS.md}
      style={styles.dishRowCard}
      padding={SPACING.md}
      backgroundColor="rgba(22, 25, 31, 0.75)"
    >
      {/* Title, Price, and Remove Action */}
      <View style={styles.dishHeaderRow}>
        <View style={styles.dishTitleCol}>
          <Text style={styles.dishName}>{dish.name}</Text>
          <Text style={styles.dishOrigin}>{dish.origin}</Text>
        </View>

        <View style={styles.rightActionsRow}>
          <Text style={styles.dishPrice}>€{dish.price.toFixed(2)}</Text>
          <Pressable
            onPress={() => onRemove(dish.id)}
            style={({ pressed }) => [
              styles.removeButton,
              pressed && styles.removeButtonPressed,
            ]}
            accessibilityRole="button"
            accessibilityLabel={`Удалить ${dish.name} из дегустационного сета`}
            hitSlop={8}
          >
            <Trash2 size={13} color={COLORS.burgundy} strokeWidth={2.2} />
            <Text style={styles.removeButtonText}>Удалить</Text>
          </Pressable>
        </View>
      </View>

      {/* Guest Sommelier / Gastronomy Rating (1-5 Stars) */}
      <View style={styles.ratingSection}>
        <Text style={styles.ratingLabel}>ВАША ДЕГУСТАЦИОННАЯ ОЦЕНКА</Text>
        <View style={styles.starsRow}>
          {[1, 2, 3, 4, 5].map((star) => {
            const isFilled = star <= currentRating;
            return (
              <Pressable
                key={star}
                onPress={() => handleStarPress(star)}
                style={styles.starTouch}
                hitSlop={6}
                accessibilityRole="button"
                accessibilityLabel={`Оценка ${star} из 5`}
              >
                <Star
                  size={18}
                  color={isFilled ? COLORS.champagneAccent : 'rgba(255, 255, 255, 0.2)'}
                  fill={isFilled ? COLORS.champagneAccent : 'transparent'}
                  strokeWidth={2}
                />
              </Pressable>
            );
          })}
          {currentRating > 0 && (
            <Text style={styles.ratingValueText}>{currentRating}/5</Text>
          )}
        </View>
      </View>

      {/* Guest Notes Input */}
      <View style={styles.notesContainer}>
        <TextInput
          style={styles.notesInput}
          placeholder="Заметки о вкусе, аромате и впечатлениях..."
          placeholderTextColor="rgba(142, 149, 165, 0.5)"
          value={localNotes}
          onChangeText={handleNotesChange}
          multiline
          numberOfLines={2}
        />
      </View>
    </GlassCard>
  );
};

export const CourseSection: React.FC<CourseSectionProps> = ({
  courseName,
  stepNumber,
  dishes,
  style,
}) => {
  const { feedbacks, removeFromTastingSet, setDishFeedback } = useTasting();

  if (!dishes || dishes.length === 0) {
    return null;
  }

  const courseSubtotal = dishes.reduce((sum, d) => sum + d.price, 0);

  return (
    <View style={[styles.container, style]}>
      {/* Course Group Header */}
      <View style={styles.courseHeader}>
        <View style={styles.stepBadge}>
          <Text style={styles.stepBadgeText}>{stepNumber}</Text>
        </View>

        <View style={styles.headerTitleBox}>
          <Text style={styles.courseTitle}>
            {stepNumber}-я подача • {COURSE_STEP_TITLES[courseName] || courseName}
          </Text>
          <Text style={styles.courseSubtitle}>
            {dishes.length} {dishes.length === 1 ? 'позиция' : 'позиции'} • €{courseSubtotal.toFixed(2)}
          </Text>
        </View>
      </View>

      {/* List of Dishes for this Course */}
      <View style={styles.dishesList}>
        {dishes.map((dish) => (
          <DishItemRow
            key={dish.id}
            dish={dish}
            feedback={feedbacks[dish.id]}
            onRemove={removeFromTastingSet}
            onUpdateFeedback={setDishFeedback}
          />
        ))}
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    marginBottom: SPACING.xl,
  },
  courseHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    marginBottom: SPACING.sm,
    paddingHorizontal: 2,
  },
  stepBadge: {
    width: 24,
    height: 24,
    borderRadius: RADIUS.full,
    backgroundColor: 'rgba(229, 169, 98, 0.2)',
    borderWidth: 1,
    borderColor: COLORS.champagneAccent,
    alignItems: 'center',
    justifyContent: 'center',
  },
  stepBadgeText: {
    fontFamily: FONTS.mono,
    fontSize: 11,
    fontWeight: '700',
    color: COLORS.champagneAccent,
  },
  headerTitleBox: {
    flex: 1,
  },
  courseTitle: {
    fontFamily: FONTS.serif,
    fontSize: 16,
    fontWeight: '700',
    color: COLORS.textPrimary,
    letterSpacing: 0.3,
  },
  courseSubtitle: {
    fontFamily: FONTS.sans,
    fontSize: 11,
    color: COLORS.textSecondary,
    marginTop: 1,
  },
  dishesList: {
    gap: 10,
  },
  dishRowCard: {
    borderWidth: 1,
    borderColor: COLORS.glassBorder,
  },
  dishHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: 12,
  },
  dishTitleCol: {
    flex: 1,
  },
  dishName: {
    fontFamily: FONTS.serif,
    fontSize: 15,
    fontWeight: '600',
    color: COLORS.textPrimary,
    lineHeight: 20,
  },
  dishOrigin: {
    fontFamily: FONTS.sans,
    fontSize: 11,
    color: COLORS.textSecondary,
    marginTop: 2,
  },
  rightActionsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },
  dishPrice: {
    fontFamily: FONTS.mono,
    fontSize: 15,
    fontWeight: '700',
    color: COLORS.champagneAccent,
  },
  removeButton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingHorizontal: 8,
    paddingVertical: 5,
    borderRadius: RADIUS.sm,
    backgroundColor: 'rgba(140, 45, 56, 0.15)',
    borderWidth: 1,
    borderColor: 'rgba(140, 45, 56, 0.3)',
  },
  removeButtonText: {
    fontFamily: FONTS.sans,
    fontSize: 11,
    fontWeight: '600',
    color: COLORS.burgundyLight,
  },
  removeButtonPressed: {
    opacity: 0.7,
    transform: [{ scale: 0.95 }],
  },
  ratingSection: {
    marginTop: 10,
    paddingTop: 8,
    borderTopWidth: 1,
    borderTopColor: 'rgba(255, 255, 255, 0.05)',
  },
  ratingLabel: {
    fontFamily: FONTS.mono,
    fontSize: 9,
    fontWeight: '700',
    color: COLORS.textSecondary,
    letterSpacing: 1,
    marginBottom: 4,
  },
  starsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },
  starTouch: {
    padding: 2,
  },
  ratingValueText: {
    fontFamily: FONTS.mono,
    fontSize: 11,
    fontWeight: '700',
    color: COLORS.champagneAccent,
    marginLeft: 6,
  },
  notesContainer: {
    marginTop: 8,
  },
  notesInput: {
    backgroundColor: 'rgba(14, 16, 19, 0.65)',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.07)',
    borderRadius: RADIUS.sm,
    paddingHorizontal: 10,
    paddingVertical: 8,
    fontSize: 12,
    fontFamily: FONTS.sans,
    color: COLORS.textPrimary,
    minHeight: 36,
  },
});

export default CourseSection;
