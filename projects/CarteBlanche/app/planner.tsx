import React, { useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  Pressable,
  StyleSheet,
  Modal,
  Platform,
} from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import {
  ChevronLeft,
  Sparkles,
  Trash2,
  CheckCircle2,
  Share2,
  UtensilsCrossed,
  X,
  Star,
} from 'lucide-react-native';
import {
  CourseCategory,
  ALL_COURSE_CATEGORIES,
  MenuItem,
  DINING_AREA_LABELS_RU,
} from '../src/types/menu';
import {
  COLORS,
  FONTS,
  RADIUS,
  SPACING,
  TYPOGRAPHY,
  SHADOWS,
} from '../src/constants/theme';
import { GlassCard } from '../src/components/common/GlassCard';
import { CourseSection } from '../src/components/planner/CourseSection';
import { BillCalculator } from '../src/components/planner/BillCalculator';
import { useTasting } from '../src/context/TastingContext';

export default function PlannerScreen() {
  const router = useRouter();
  const {
    tastingSet,
    tastingSubtotal,
    tastingCount,
    clearTastingSet,
    feedbacks,
    selectedArea,
  } = useTasting();

  const [showClearConfirm, setShowClearConfirm] = useState<boolean>(false);
  const [showCompletionModal, setShowCompletionModal] = useState<boolean>(false);

  // Group dishes by canonical course order
  const groupedCourses = ALL_COURSE_CATEGORIES.map((category, index) => {
    const dishes = tastingSet.filter((item) => item.course === category);
    return {
      category,
      stepNumber: index + 1,
      dishes,
    };
  }).filter((group) => group.dishes.length > 0);

  const handleConfirmClear = () => {
    clearTastingSet();
    setShowClearConfirm(false);
  };

  const formatDishCount = (count: number) => {
    if (count % 10 === 1 && count % 100 !== 11) return `${count} блюдо`;
    if (count % 10 >= 2 && count % 10 <= 4 && (count % 100 < 10 || count % 100 >= 20)) {
      return `${count} блюда`;
    }
    return `${count} блюд`;
  };

  return (
    <SafeAreaView style={styles.container} edges={['top', 'bottom']}>
      {/* 1. Header Bar */}
      <View style={styles.headerBar}>
        <Pressable
          onPress={() => router.back()}
          style={styles.backButton}
          accessibilityLabel="Назад к меню"
          hitSlop={10}
        >
          <ChevronLeft size={22} color={COLORS.textPrimary} strokeWidth={2.5} />
          <Text style={styles.backButtonText}>МЕНЮ</Text>
        </Pressable>

        <View style={styles.headerTitleBox}>
          <Text style={styles.headerTitle}>ДЕГУСТАЦИОННЫЙ СЕТ И ФОЛИО</Text>
          <Text style={styles.headerSubtitle}>
            {DINING_AREA_LABELS_RU[selectedArea] || selectedArea} • {formatDishCount(tastingCount)}
          </Text>
        </View>

        {tastingCount > 0 ? (
          <Pressable
            onPress={() => setShowClearConfirm(true)}
            style={styles.clearHeaderButton}
            accessibilityLabel="Очистить дегустационный сет"
            hitSlop={8}
          >
            <Trash2 size={16} color={COLORS.burgundy} strokeWidth={2.2} />
          </Pressable>
        ) : (
          <View style={styles.headerRightPlaceholder} />
        )}
      </View>

      {/* 2. Body: Empty State OR Populated Flight */}
      {tastingCount === 0 ? (
        <View style={styles.emptyContainer}>
          <View style={styles.emptyIconCircle}>
            <UtensilsCrossed size={40} color={COLORS.champagneAccent} strokeWidth={1.8} />
          </View>
          <Text style={styles.emptyTitle}>Ваш дегустационный сет пуст</Text>
          <Text style={styles.emptySubtitle}>
            Исследуйте меню высокой кухни и составьте персональную последовательность подач.
          </Text>
          <Pressable
            onPress={() => router.push('/')}
            style={({ pressed }) => [
              styles.exploreMenuButton,
              pressed && styles.buttonPressed,
            ]}
          >
            <Sparkles size={16} color={COLORS.obsidianCanvas} strokeWidth={2.5} />
            <Text style={styles.exploreMenuText}>Выбрать блюда из меню</Text>
          </Pressable>
        </View>
      ) : (
        <ScrollView
          contentContainerStyle={styles.scrollContent}
          showsVerticalScrollIndicator={false}
        >
          {/* Progress Overview Banner */}
          <GlassCard
            cornerRadius={RADIUS.lg}
            style={styles.summaryBanner}
            padding={SPACING.md}
            backgroundColor="rgba(229, 169, 98, 0.08)"
            strokeColor="rgba(229, 169, 98, 0.3)"
          >
            <View style={styles.bannerRow}>
              <View style={styles.bannerItem}>
                <Text style={styles.bannerLabel}>ПОДАЧИ</Text>
                <Text style={styles.bannerValue}>{groupedCourses.length} курсов</Text>
              </View>
              <View style={styles.bannerDivider} />
              <View style={styles.bannerItem}>
                <Text style={styles.bannerLabel}>БЛЮДА В СЕТЕ</Text>
                <Text style={styles.bannerValue}>{tastingCount} позиций</Text>
              </View>
              <View style={styles.bannerDivider} />
              <View style={styles.bannerItem}>
                <Text style={styles.bannerLabel}>СУММА СЕТА</Text>
                <Text style={styles.bannerPrice}>€{tastingSubtotal.toFixed(2)}</Text>
              </View>
            </View>
          </GlassCard>

          {/* Course Groups (1-я Prelude -> 2-я Main Courses -> etc.) */}
          <View style={styles.courseSectionsWrapper}>
            {groupedCourses.map((group) => (
              <CourseSection
                key={group.category}
                courseName={group.category}
                stepNumber={group.stepNumber}
                dishes={group.dishes}
              />
            ))}
          </View>

          {/* Interactive Guest Folio & Split Calculator */}
          <BillCalculator subtotal={tastingSubtotal} />

          {/* Bottom Actions: Clear & Complete Degustation */}
          <View style={styles.actionButtonsRow}>
            <Pressable
              onPress={() => setShowClearConfirm(true)}
              style={({ pressed }) => [
                styles.actionClearBtn,
                pressed && styles.buttonPressed,
              ]}
            >
              <Trash2 size={16} color={COLORS.burgundy} strokeWidth={2} />
              <Text style={styles.actionClearText}>Очистить сет</Text>
            </Pressable>

            <Pressable
              onPress={() => setShowCompletionModal(true)}
              style={({ pressed }) => [
                styles.actionCompleteBtn,
                pressed && styles.buttonPressed,
              ]}
            >
              <Sparkles size={16} color={COLORS.obsidianCanvas} strokeWidth={2.4} />
              <Text style={styles.actionCompleteText}>Завершить дегустацию</Text>
            </Pressable>
          </View>
        </ScrollView>
      )}

      {/* Confirmation Modal for Clearing Set */}
      <Modal
        visible={showClearConfirm}
        transparent
        animationType="fade"
        onRequestClose={() => setShowClearConfirm(false)}
      >
        <View style={styles.modalBackdrop}>
          <GlassCard
            cornerRadius={RADIUS.xl}
            style={styles.confirmModalCard}
            padding={SPACING.xl}
            backgroundColor="rgba(22, 25, 31, 0.96)"
            strokeColor="rgba(140, 45, 56, 0.5)"
          >
            <Text style={styles.confirmModalTitle}>Очистить дегустационный сет?</Text>
            <Text style={styles.confirmModalText}>
              Все выбранные позиции и дегустационные заметки будут удалены из текущего сета.
            </Text>

            <View style={styles.confirmModalActions}>
              <Pressable
                onPress={() => setShowClearConfirm(false)}
                style={styles.cancelModalBtn}
              >
                <Text style={styles.cancelModalBtnText}>Отмена</Text>
              </Pressable>
              <Pressable
                onPress={handleConfirmClear}
                style={styles.destructiveModalBtn}
              >
                <Text style={styles.destructiveModalBtnText}>Очистить</Text>
              </Pressable>
            </View>
          </GlassCard>
        </View>
      </Modal>

      {/* Degustation Completion / Folio Export Modal */}
      <Modal
        visible={showCompletionModal}
        transparent
        animationType="slide"
        onRequestClose={() => setShowCompletionModal(false)}
      >
        <View style={styles.modalBackdrop}>
          <GlassCard
            cornerRadius={RADIUS.xl}
            style={styles.completionModalCard}
            padding={SPACING.xl}
            backgroundColor="rgba(14, 16, 19, 0.98)"
            strokeColor={COLORS.champagneAccent}
          >
            <View style={styles.completionHeader}>
              <View style={styles.completionHeaderLeft}>
                <Sparkles size={18} color={COLORS.champagneAccent} />
                <Text style={styles.completionEditorialTag}>ДЕГУСТАЦИОННОЕ ФОЛИО</Text>
              </View>
              <Pressable
                onPress={() => setShowCompletionModal(false)}
                style={styles.modalCloseBtn}
                hitSlop={8}
                accessibilityLabel="Закрыть модальное окно"
              >
                <X size={20} color={COLORS.textSecondary} />
              </Pressable>
            </View>

            <Text style={styles.completionTitle}>Итоговое дегустационное фолио</Text>
            <Text style={styles.completionSubtitle}>
              Благодарим за визит в Carte Blanche
            </Text>

            {/* Degustation Summary List */}
            <ScrollView style={styles.completionDishesList} showsVerticalScrollIndicator={false}>
              {tastingSet.map((dish, i) => {
                const fb = feedbacks[dish.id];
                return (
                  <View key={dish.id} style={styles.summaryDishItem}>
                    <View style={styles.summaryDishRow}>
                      <Text style={styles.summaryDishNumber}>{i + 1}.</Text>
                      <Text style={styles.summaryDishName}>{dish.name}</Text>
                      <Text style={styles.summaryDishPrice}>€{dish.price.toFixed(2)}</Text>
                    </View>
                    {fb?.rating ? (
                      <View style={styles.summaryRatingRow}>
                        {[1, 2, 3, 4, 5].map((star) => (
                          <Star
                            key={star}
                            size={12}
                            color={star <= fb.rating ? COLORS.champagneAccent : 'rgba(255,255,255,0.2)'}
                            fill={star <= fb.rating ? COLORS.champagneAccent : 'transparent'}
                          />
                        ))}
                        {fb.notes ? (
                          <Text style={styles.summaryNotesText} numberOfLines={1}>
                            "{fb.notes}"
                          </Text>
                        ) : null}
                      </View>
                    ) : null}
                  </View>
                );
              })}
            </ScrollView>

            <View style={styles.completionTotalBar}>
              <Text style={styles.completionTotalLabel}>ИТОГО К ОПЛАТЕ:</Text>
              <Text style={styles.completionTotalValue}>€{tastingSubtotal.toFixed(2)}</Text>
            </View>

            <Pressable
              onPress={() => {
                setShowCompletionModal(false);
                router.push('/');
              }}
              style={({ pressed }) => [
                styles.completionDoneBtn,
                pressed && styles.buttonPressed,
              ]}
              accessibilityRole="button"
              accessibilityLabel="Закрыть"
            >
              <CheckCircle2 size={18} color={COLORS.obsidianCanvas} strokeWidth={2.5} />
              <Text style={styles.completionDoneBtnText}>Закрыть</Text>
            </Pressable>
          </GlassCard>
        </View>
      </Modal>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: COLORS.obsidianCanvas,
  },
  headerBar: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: SPACING.lg,
    paddingVertical: SPACING.md,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.06)',
  },
  backButton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingVertical: 4,
    paddingHorizontal: 6,
    borderRadius: RADIUS.sm,
    backgroundColor: 'rgba(255, 255, 255, 0.05)',
  },
  backButtonText: {
    fontFamily: FONTS.mono,
    fontSize: 11,
    fontWeight: '700',
    color: COLORS.textPrimary,
    letterSpacing: 1.2,
  },
  headerTitleBox: {
    alignItems: 'center',
  },
  headerTitle: {
    fontFamily: FONTS.mono,
    fontSize: 13,
    fontWeight: '700',
    color: COLORS.champagneAccent,
    letterSpacing: 1.5,
  },
  headerSubtitle: {
    fontFamily: FONTS.sans,
    fontSize: 11,
    color: COLORS.textSecondary,
    marginTop: 1,
  },
  clearHeaderButton: {
    padding: 6,
    borderRadius: RADIUS.sm,
    backgroundColor: 'rgba(140, 45, 56, 0.15)',
    borderWidth: 1,
    borderColor: 'rgba(140, 45, 56, 0.35)',
  },
  headerRightPlaceholder: {
    width: 32,
  },
  emptyContainer: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: SPACING.xxxl,
  },
  emptyIconCircle: {
    width: 80,
    height: 80,
    borderRadius: RADIUS.full,
    backgroundColor: 'rgba(229, 169, 98, 0.12)',
    borderWidth: 1,
    borderColor: 'rgba(229, 169, 98, 0.3)',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: SPACING.lg,
    ...SHADOWS.glowChampagne,
  },
  emptyTitle: {
    fontFamily: FONTS.serif,
    fontSize: 22,
    fontWeight: '700',
    color: COLORS.textPrimary,
    marginBottom: 8,
  },
  emptySubtitle: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    lineHeight: 20,
    color: COLORS.textSecondary,
    textAlign: 'center',
    marginBottom: SPACING.xl,
  },
  exploreMenuButton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    backgroundColor: COLORS.champagneAccent,
    paddingHorizontal: 22,
    paddingVertical: 12,
    borderRadius: RADIUS.full,
    ...SHADOWS.glowChampagne,
  },
  exploreMenuText: {
    fontFamily: FONTS.sans,
    fontSize: 14,
    fontWeight: '700',
    color: COLORS.obsidianCanvas,
  },
  scrollContent: {
    paddingHorizontal: SPACING.xl,
    paddingTop: SPACING.md,
    paddingBottom: 60,
  },
  summaryBanner: {
    marginBottom: SPACING.xl,
  },
  bannerRow: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    alignItems: 'center',
  },
  bannerItem: {
    alignItems: 'center',
  },
  bannerLabel: {
    fontFamily: FONTS.mono,
    fontSize: 9,
    fontWeight: '700',
    color: COLORS.textSecondary,
    letterSpacing: 1,
    marginBottom: 2,
  },
  bannerValue: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    fontWeight: '600',
    color: COLORS.textPrimary,
  },
  bannerPrice: {
    fontFamily: FONTS.mono,
    fontSize: 15,
    fontWeight: '700',
    color: COLORS.champagneAccent,
  },
  bannerDivider: {
    width: 1,
    height: 28,
    backgroundColor: 'rgba(255, 255, 255, 0.1)',
  },
  courseSectionsWrapper: {
    marginBottom: SPACING.md,
  },
  actionButtonsRow: {
    flexDirection: 'row',
    gap: 12,
    marginTop: SPACING.lg,
    marginBottom: SPACING.xl,
  },
  actionClearBtn: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    paddingVertical: 13,
    borderRadius: RADIUS.full,
    backgroundColor: 'rgba(140, 45, 56, 0.18)',
    borderWidth: 1,
    borderColor: 'rgba(140, 45, 56, 0.4)',
  },
  actionClearText: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    fontWeight: '600',
    color: COLORS.burgundyLight,
  },
  actionCompleteBtn: {
    flex: 2,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    paddingVertical: 13,
    borderRadius: RADIUS.full,
    backgroundColor: COLORS.champagneAccent,
    ...SHADOWS.glowChampagne,
  },
  actionCompleteText: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    fontWeight: '700',
    color: COLORS.obsidianCanvas,
  },
  buttonPressed: {
    opacity: 0.85,
    transform: [{ scale: 0.98 }],
  },
  modalBackdrop: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.8)',
    justifyContent: 'center',
    alignItems: 'center',
    paddingHorizontal: SPACING.xl,
  },
  confirmModalCard: {
    width: '100%',
    maxWidth: 380,
  },
  confirmModalTitle: {
    fontFamily: FONTS.serif,
    fontSize: 18,
    fontWeight: '700',
    color: COLORS.textPrimary,
    marginBottom: 8,
  },
  confirmModalText: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    lineHeight: 19,
    color: COLORS.textSecondary,
    marginBottom: SPACING.lg,
  },
  confirmModalActions: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    gap: 10,
  },
  cancelModalBtn: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: RADIUS.sm,
  },
  cancelModalBtnText: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    color: COLORS.textSecondary,
    fontWeight: '500',
  },
  destructiveModalBtn: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: RADIUS.sm,
    backgroundColor: COLORS.burgundy,
  },
  destructiveModalBtnText: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    color: '#FFFFFF',
    fontWeight: '600',
  },
  completionModalCard: {
    width: '100%',
    maxWidth: 420,
    maxHeight: '80%',
  },
  completionHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: SPACING.sm,
  },
  completionHeaderLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  completionEditorialTag: {
    fontFamily: FONTS.mono,
    fontSize: 10,
    fontWeight: '700',
    color: COLORS.champagneAccent,
    letterSpacing: 1.4,
  },
  modalCloseBtn: {
    padding: 4,
  },
  completionTitle: {
    fontFamily: FONTS.serif,
    fontSize: 20,
    fontWeight: '700',
    color: COLORS.textPrimary,
    marginBottom: 4,
  },
  completionSubtitle: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    lineHeight: 18,
    color: COLORS.textSecondary,
    marginBottom: SPACING.md,
  },
  completionDishesList: {
    maxHeight: 220,
    marginVertical: SPACING.xs,
  },
  summaryDishItem: {
    paddingVertical: 6,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.05)',
  },
  summaryDishRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  summaryDishNumber: {
    fontFamily: FONTS.mono,
    fontSize: 12,
    color: COLORS.champagneAccent,
    marginRight: 6,
  },
  summaryDishName: {
    flex: 1,
    fontFamily: FONTS.serif,
    fontSize: 13,
    color: COLORS.textPrimary,
  },
  summaryDishPrice: {
    fontFamily: FONTS.mono,
    fontSize: 12,
    color: COLORS.champagneAccent,
  },
  summaryRatingRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 3,
    marginTop: 3,
    marginLeft: 18,
  },
  summaryNotesText: {
    fontFamily: FONTS.sans,
    fontSize: 11,
    fontStyle: 'italic',
    color: COLORS.textSecondary,
    marginLeft: 6,
    flex: 1,
  },
  completionTotalBar: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 12,
    borderTopWidth: 1,
    borderTopColor: 'rgba(255, 255, 255, 0.1)',
    marginTop: SPACING.sm,
  },
  completionTotalLabel: {
    fontFamily: FONTS.mono,
    fontSize: 12,
    fontWeight: '700',
    letterSpacing: 1.2,
    color: COLORS.textPrimary,
  },
  completionTotalValue: {
    fontFamily: FONTS.mono,
    fontSize: 20,
    fontWeight: '800',
    color: COLORS.champagneAccent,
  },
  completionDoneBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    backgroundColor: COLORS.champagneAccent,
    paddingVertical: 12,
    borderRadius: RADIUS.full,
    marginTop: SPACING.md,
  },
  completionDoneBtnText: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    fontWeight: '700',
    color: COLORS.obsidianCanvas,
  },
});
