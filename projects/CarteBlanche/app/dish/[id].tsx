import React, { useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  Pressable,
  StyleSheet,
  Platform,
} from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import {
  ChevronLeft,
  Crown,
  Wine,
  Thermometer,
  Sparkles,
  Check,
  Plus,
  Compass,
  Flame,
  Info,
} from 'lucide-react-native';
import { SEED_MENU } from '../../src/data/seedMenu';
import { COURSE_CATEGORY_SHORT_RU } from '../../src/types/menu';
import {
  COLORS,
  FONTS,
  RADIUS,
  SPACING,
  TYPOGRAPHY,
  SHADOWS,
} from '../../src/constants/theme';
import { GlassCard } from '../../src/components/common/GlassCard';
import { DietaryBadge } from '../../src/components/common/DietaryBadge';
import {
  FlavorRadarChart,
  AxisLanguage,
} from '../../src/components/common/FlavorRadarChart';
import { useTasting } from '../../src/context/TastingContext';

export default function DishDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { isInTastingSet, addToTastingSet, removeFromTastingSet } = useTasting();

  const [radarLanguage, setRadarLanguage] = useState<AxisLanguage>('RU');

  const dish = SEED_MENU.find((item) => item.id === id);
  const inSet = dish ? isInTastingSet(dish.id) : false;

  if (!dish) {
    return (
      <SafeAreaView style={styles.notFoundContainer} edges={['top', 'bottom']}>
        <View style={styles.topBar}>
          <Pressable
            onPress={() => router.back()}
            style={styles.backButton}
            accessibilityLabel="Назад"
          >
            <ChevronLeft size={22} color={COLORS.textPrimary} />
          </Pressable>
        </View>
        <View style={styles.notFoundContent}>
          <Sparkles size={48} color={COLORS.champagneAccent} />
          <Text style={styles.notFoundTitle}>Позиция не найдена</Text>
          <Text style={styles.notFoundText}>
            Выбранное блюдо отсутствует в дегустационной карте ресторана.
          </Text>
          <Pressable
            onPress={() => router.back()}
            style={styles.returnButton}
          >
            <Text style={styles.returnButtonText}>Вернуться в меню</Text>
          </Pressable>
        </View>
      </SafeAreaView>
    );
  }

  const handleToggleSet = () => {
    if (inSet) {
      removeFromTastingSet(dish.id);
    } else {
      addToTastingSet(dish);
    }
  };

  return (
    <SafeAreaView style={styles.container} edges={['top', 'bottom']}>
      {/* 1. Header Bar */}
      <View style={styles.topBar}>
        <Pressable
          onPress={() => router.back()}
          style={styles.backButton}
          accessibilityLabel="Назад к меню"
          hitSlop={12}
        >
          <ChevronLeft size={22} color={COLORS.textPrimary} strokeWidth={2.5} />
          <Text style={styles.backButtonText}>МЕНЮ</Text>
        </Pressable>

        <View style={styles.topBarRight}>
          <Text style={styles.topBarPrice}>€{dish.price.toFixed(2)}</Text>
        </View>
      </View>

      {/* 2. Scrollable Body Content */}
      <ScrollView
        contentContainerStyle={styles.scrollContent}
        showsVerticalScrollIndicator={false}
      >
        {/* Category, Chef Badge & Origin */}
        <View style={styles.metaHeaderRow}>
          <View style={styles.categoryBadge}>
            <Text style={styles.categoryBadgeText}>
              {(COURSE_CATEGORY_SHORT_RU[dish.course] || dish.course).toUpperCase()}
            </Text>
          </View>

          {dish.isChefSelection && (
            <View style={styles.chefBadge}>
              <Crown size={12} color={COLORS.champagneAccent} strokeWidth={2.5} />
              <Text style={styles.chefBadgeText}>ВЫБОР ШЕФА</Text>
            </View>
          )}
        </View>

        {/* Dish Title */}
        <Text style={styles.title}>{dish.name}</Text>

        {/* Origin / Terroir */}
        {dish.origin && (
          <View style={styles.originRow}>
            <Compass size={13} color={COLORS.champagneAccent} strokeWidth={2} />
            <Text style={styles.originLabel}>Терруар и происхождение:</Text>
            <Text style={styles.originText}>{dish.origin}</Text>
          </View>
        )}

        {/* Full Gastronomic Description */}
        <Text style={styles.fullDescription}>{dish.description}</Text>

        {/* Dietary Badges */}
        {dish.dietaryTags && dish.dietaryTags.length > 0 && (
          <View style={styles.tagsRow}>
            {dish.dietaryTags.map((tag) => (
              <DietaryBadge key={tag} tag={tag} size="md" />
            ))}
          </View>
        )}

        {/* Sensory Flavor Profile Radar Chart */}
        <GlassCard
          cornerRadius={RADIUS.xl}
          style={styles.radarCard}
          padding={SPACING.lg}
        >
          <View style={styles.sectionHeaderRow}>
            <View style={styles.sectionTitleCluster}>
              <Sparkles size={14} color={COLORS.champagneAccent} />
              <Text style={styles.sectionTitle}>СЕНСОРНЫЙ ВКУСОВОЙ ПРОФИЛЬ</Text>
            </View>

            {/* Language Switcher */}
            <View style={styles.langSelector}>
              {(['RU', 'EN'] as AxisLanguage[]).map((lang) => (
                <Pressable
                  key={lang}
                  onPress={() => setRadarLanguage(lang)}
                  style={[
                    styles.langBtn,
                    radarLanguage === lang && styles.langBtnActive,
                  ]}
                  hitSlop={4}
                >
                  <Text
                    style={[
                      styles.langBtnText,
                      radarLanguage === lang && styles.langBtnTextActive,
                    ]}
                  >
                    {lang}
                  </Text>
                </Pressable>
              ))}
            </View>
          </View>

          <View style={styles.chartCenterWrapper}>
            <FlavorRadarChart
              profile={dish.flavorProfile}
              size={190}
              language={radarLanguage}
              showValueLabels
            />
          </View>
        </GlassCard>

        {/* Culinary Story & Terroir */}
        <GlassCard
          cornerRadius={RADIUS.lg}
          style={styles.storyCard}
          padding={SPACING.lg}
        >
          <View style={styles.storyHeader}>
            <Flame size={15} color={COLORS.champagneAccent} />
            <Text style={styles.storySectionTitle}>ИСТОРИЯ БЛЮДА И ТЕХНИКА ШЕФА</Text>
          </View>
          <Text style={styles.storyBody}>{dish.culinaryStory}</Text>
        </GlassCard>

        {/* Sommelier Pairing Card */}
        <View style={styles.pairingCardWrapper}>
          <View style={styles.pairingHeaderBar}>
            <View style={styles.pairingIconBadge}>
              <Wine size={16} color="#FFFFFF" strokeWidth={2.2} />
            </View>
            <View style={styles.pairingTitleCol}>
              <Text style={styles.pairingTag}>
                РЕКОМЕНДАЦИЯ СОМЕЛЬЕ И ПОГРЕБ • {dish.pairing.type.toUpperCase()}
              </Text>
              <Text style={styles.pairingName}>
                {dish.pairing.name}
              </Text>
            </View>
          </View>

          {/* Producer & Vintage & Temp */}
          <View style={styles.pairingMetaGrid}>
            <View style={styles.metaCol}>
              <Text style={styles.metaLabel}>ПРОИЗВОДИТЕЛЬ</Text>
              <Text style={styles.metaValue}>{dish.pairing.producer}</Text>
            </View>

            <View style={styles.metaCol}>
              <Text style={styles.metaLabel}>ВИНТАЖ</Text>
              <Text style={styles.metaValue}>{dish.pairing.vintage}</Text>
            </View>

            <View style={styles.metaColRight}>
              <Text style={styles.metaLabel}>ТЕМПЕРАТУРА ПОДАЧИ</Text>
              <View style={styles.tempBadge}>
                <Thermometer size={12} color={COLORS.champagneAccent} />
                <Text style={styles.tempText}>{dish.pairing.temperature}</Text>
              </View>
            </View>
          </View>

          {/* Harmony Notes */}
          <View style={styles.notesBox}>
            <View style={styles.notesHeaderRow}>
              <Info size={13} color={COLORS.champagneAccent} style={styles.notesIcon} />
              <Text style={styles.notesHeaderLabel}>ГАРМОНИЯ ВКУСА</Text>
            </View>
            <Text style={styles.notesText}>{dish.pairing.notes}</Text>
          </View>
        </View>
      </ScrollView>

      {/* 3. Floating Bottom Action Button */}
      <View style={styles.floatingBottomBar}>
        <Pressable
          onPress={handleToggleSet}
          accessibilityRole="button"
          style={({ pressed }) => [
            styles.actionButton,
            inSet ? styles.actionButtonRemove : styles.actionButtonAdd,
            pressed && styles.actionButtonPressed,
          ]}
        >
          {inSet ? (
            <>
              <Check size={18} color={COLORS.textPrimary} strokeWidth={2.8} />
              <Text style={styles.actionButtonTextRemove}>
                В дегустационном сете (Удалить)
              </Text>
            </>
          ) : (
            <>
              <Plus size={18} color={COLORS.obsidianCanvas} strokeWidth={2.8} />
              <Text style={styles.actionButtonTextAdd}>
                Добавить в дегустационный сет • €{dish.price.toFixed(2)}
              </Text>
            </>
          )}
        </Pressable>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: COLORS.obsidianCanvas,
  },
  topBar: {
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
  topBarRight: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  topBarPrice: {
    fontFamily: FONTS.mono,
    fontSize: 18,
    fontWeight: '700',
    color: COLORS.champagneAccent,
    letterSpacing: 0.5,
  },
  scrollContent: {
    paddingHorizontal: SPACING.xl,
    paddingTop: SPACING.lg,
    paddingBottom: 110,
  },
  metaHeaderRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginBottom: SPACING.xs,
  },
  categoryBadge: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: RADIUS.xs,
    backgroundColor: 'rgba(255, 255, 255, 0.06)',
    borderWidth: 1,
    borderColor: COLORS.glassBorder,
  },
  categoryBadgeText: {
    fontFamily: FONTS.mono,
    fontSize: 10,
    fontWeight: '700',
    color: COLORS.champagneAccent,
    letterSpacing: 1.2,
  },
  chefBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: RADIUS.full,
    backgroundColor: 'rgba(229, 169, 98, 0.15)',
    borderWidth: 1,
    borderColor: 'rgba(229, 169, 98, 0.4)',
  },
  chefBadgeText: {
    fontFamily: FONTS.mono,
    fontSize: 9,
    fontWeight: '700',
    color: COLORS.champagneAccent,
    letterSpacing: 0.8,
  },
  title: {
    fontFamily: FONTS.serif,
    fontSize: 26,
    lineHeight: 32,
    fontWeight: '700',
    color: COLORS.textPrimary,
    marginTop: 6,
    marginBottom: 4,
  },
  originRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    flexWrap: 'wrap',
    marginBottom: SPACING.md,
  },
  originLabel: {
    fontFamily: FONTS.mono,
    fontSize: 11,
    fontWeight: '700',
    color: COLORS.textSecondary,
    letterSpacing: 0.8,
  },
  originText: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    color: COLORS.champagneAccent,
    fontWeight: '600',
    letterSpacing: 0.2,
  },
  fullDescription: {
    fontFamily: FONTS.sans,
    fontSize: 15,
    lineHeight: 23,
    color: COLORS.textSecondary,
    marginBottom: SPACING.lg,
  },
  tagsRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginBottom: SPACING.xl,
  },
  radarCard: {
    marginBottom: SPACING.xl,
  },
  sectionHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: SPACING.sm,
  },
  sectionTitleCluster: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  sectionTitle: {
    fontFamily: FONTS.mono,
    fontSize: 11,
    fontWeight: '700',
    color: COLORS.champagneAccent,
    letterSpacing: 1.4,
  },
  langSelector: {
    flexDirection: 'row',
    backgroundColor: 'rgba(0, 0, 0, 0.35)',
    borderRadius: RADIUS.sm,
    padding: 2,
    borderWidth: 1,
    borderColor: COLORS.glassBorder,
  },
  langBtn: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: RADIUS.xs,
  },
  langBtnActive: {
    backgroundColor: COLORS.champagneAccent,
  },
  langBtnText: {
    fontFamily: FONTS.mono,
    fontSize: 10,
    fontWeight: '600',
    color: COLORS.textSecondary,
  },
  langBtnTextActive: {
    color: COLORS.obsidianCanvas,
    fontWeight: '700',
  },
  chartCenterWrapper: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: SPACING.xs,
  },
  storyCard: {
    marginBottom: SPACING.xl,
  },
  storyHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    marginBottom: SPACING.sm,
  },
  storySectionTitle: {
    fontFamily: FONTS.mono,
    fontSize: 11,
    fontWeight: '700',
    color: COLORS.champagneAccent,
    letterSpacing: 1.4,
  },
  storyBody: {
    fontFamily: FONTS.serif,
    fontSize: 14,
    lineHeight: 22,
    color: COLORS.textPrimary,
    fontStyle: 'italic',
    opacity: 0.95,
  },
  pairingCardWrapper: {
    backgroundColor: 'rgba(140, 45, 56, 0.22)',
    borderRadius: RADIUS.xl,
    borderWidth: 1,
    borderColor: 'rgba(140, 45, 56, 0.55)',
    padding: SPACING.lg,
    position: 'relative',
    overflow: 'hidden',
  },
  pairingHeaderBar: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SPACING.md,
    marginBottom: SPACING.md,
  },
  pairingIconBadge: {
    width: 38,
    height: 38,
    borderRadius: RADIUS.full,
    backgroundColor: COLORS.burgundy,
    alignItems: 'center',
    justifyContent: 'center',
    ...SHADOWS.glowBurgundy,
  },
  pairingTag: {
    fontFamily: FONTS.mono,
    fontSize: 10,
    fontWeight: '700',
    color: COLORS.champagneAccent,
    letterSpacing: 1.1,
  },
  pairingName: {
    fontFamily: FONTS.serif,
    fontSize: 17,
    lineHeight: 22,
    fontWeight: '700',
    color: COLORS.textPrimary,
    marginTop: 2,
  },
  pairingTitleCol: {
    flex: 1,
  },
  pairingMetaGrid: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: 'rgba(0, 0, 0, 0.28)',
    borderRadius: RADIUS.md,
    padding: SPACING.md,
    marginBottom: SPACING.md,
  },
  metaCol: {
    flex: 1,
  },
  metaColRight: {
    alignItems: 'flex-end',
  },
  metaLabel: {
    fontFamily: FONTS.mono,
    fontSize: 9,
    fontWeight: '700',
    color: COLORS.textSecondary,
    letterSpacing: 1,
    marginBottom: 2,
  },
  metaValue: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    color: COLORS.textPrimary,
    fontWeight: '600',
  },
  tempBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: RADIUS.sm,
    backgroundColor: 'rgba(229, 169, 98, 0.15)',
    borderWidth: 1,
    borderColor: 'rgba(229, 169, 98, 0.3)',
  },
  tempText: {
    fontFamily: FONTS.mono,
    fontSize: 11,
    fontWeight: '700',
    color: COLORS.champagneAccent,
  },
  notesBox: {
    backgroundColor: 'rgba(14, 16, 19, 0.5)',
    borderRadius: RADIUS.md,
    padding: SPACING.md,
  },
  notesHeaderRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    marginBottom: 4,
  },
  notesHeaderLabel: {
    fontFamily: FONTS.mono,
    fontSize: 9.5,
    fontWeight: '700',
    color: COLORS.champagneAccent,
    letterSpacing: 1.1,
  },
  notesIcon: {
    marginTop: 0,
  },
  notesText: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    lineHeight: 19,
    color: COLORS.textSecondary,
  },
  floatingBottomBar: {
    position: 'absolute',
    bottom: 0,
    left: 0,
    right: 0,
    paddingHorizontal: SPACING.xl,
    paddingTop: SPACING.md,
    paddingBottom: Platform.OS === 'ios' ? SPACING.xl : SPACING.lg,
    backgroundColor: 'rgba(14, 16, 19, 0.92)',
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
  actionButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    paddingVertical: 14,
    borderRadius: RADIUS.full,
  },
  actionButtonAdd: {
    backgroundColor: COLORS.champagneAccent,
    ...SHADOWS.glowChampagne,
  },
  actionButtonRemove: {
    backgroundColor: 'rgba(140, 45, 56, 0.35)',
    borderWidth: 1,
    borderColor: COLORS.burgundy,
  },
  actionButtonPressed: {
    opacity: 0.88,
    transform: [{ scale: 0.98 }],
  },
  actionButtonTextAdd: {
    fontFamily: FONTS.sans,
    fontSize: 14,
    fontWeight: '700',
    color: COLORS.obsidianCanvas,
    letterSpacing: 0.3,
  },
  actionButtonTextRemove: {
    fontFamily: FONTS.sans,
    fontSize: 14,
    fontWeight: '600',
    color: COLORS.textPrimary,
    letterSpacing: 0.3,
  },
  notFoundContainer: {
    flex: 1,
    backgroundColor: COLORS.obsidianCanvas,
  },
  notFoundContent: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: SPACING.xxxl,
    gap: SPACING.md,
  },
  notFoundTitle: {
    fontFamily: FONTS.serif,
    fontSize: 22,
    fontWeight: '700',
    color: COLORS.textPrimary,
  },
  notFoundText: {
    fontFamily: FONTS.sans,
    fontSize: 14,
    color: COLORS.textSecondary,
    textAlign: 'center',
    lineHeight: 20,
  },
  returnButton: {
    marginTop: SPACING.md,
    paddingHorizontal: 20,
    paddingVertical: 10,
    borderRadius: RADIUS.full,
    backgroundColor: COLORS.champagneAccent,
  },
  returnButtonText: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    fontWeight: '700',
    color: COLORS.obsidianCanvas,
  },
});
