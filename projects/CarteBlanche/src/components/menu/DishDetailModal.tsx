import React, { useState } from 'react';
import {
  View,
  Text,
  Image,
  Modal,
  ScrollView,
  Pressable,
  StyleSheet,
  Platform,
} from 'react-native';
import {
  X,
  Sparkles,
  ChefHat,
  Wine,
  Plus,
  Minus,
  Check,
  Utensils,
  Clock,
  Thermometer,
} from 'lucide-react-native';
import { MenuItem, CATEGORY_LABELS_V2, MenuCategory } from '../../types/menu';
import { COLORS, FONTS, RADIUS, SPACING, SHADOWS } from '../../constants/theme';
import { formatKZT } from '../../utils/currency';
import { useCart } from '../../context/CartContext';
import { FlavorRadarChart } from '../common/FlavorRadarChart';

export interface DishDetailModalProps {
  item: MenuItem | null;
  visible: boolean;
  onClose: () => void;
}

/**
 * DishDetailModal (Stage 4)
 * Haute Cuisine detail view presenting editorial photography, chef's culinary story,
 * 5-axis sensory flavor radar chart, burgundy sommelier pairing, and real-time cart controls.
 */
export const DishDetailModal: React.FC<DishDetailModalProps> = ({
  item,
  visible,
  onClose,
}) => {
  const { getItemQuantity, addToCart, updateQuantity } = useCart();
  const [imageError, setImageError] = useState(false);

  if (!item) {
    return null;
  }

  const quantity = getItemQuantity(item.id);
  const isChef = Boolean(item.isChefChoice || item.isChefSelection);
  const priceValue = item.priceKZT ?? item.price ?? 0;

  // Resolve category label
  const categoryLabel =
    item.categoryTitle ||
    (item.category && CATEGORY_LABELS_V2[item.category as MenuCategory]) ||
    item.course ||
    '';

  // Dietary chips
  const dietaryList: string[] =
    item.dietary && item.dietary.length > 0
      ? item.dietary
      : (item.dietaryTags as string[]) || [];

  // Flavor profile normalization for FlavorRadarChart (5 axes)
  const flavorProfile = {
    umami: item.flavor?.umami ?? item.flavorProfile?.umami ?? 0.5,
    acidity: item.flavor?.acidity ?? item.flavorProfile?.acidity ?? 0.5,
    sweetness: item.flavor?.sweetness ?? item.flavorProfile?.sweetness ?? 0.5,
    spiciness: item.flavor?.spiciness ?? item.flavorProfile?.spiciness ?? 0.5,
    texture:
      item.flavor?.texture ??
      item.flavor?.crispness ??
      item.flavorProfile?.texture ??
      0.5,
  };

  const storyText =
    item.fullStory || item.culinaryStory || item.shortDescription || item.description || '';

  const handleAdd = () => {
    addToCart(item);
  };

  const handleDecrease = () => {
    updateQuantity(item.id, -1);
  };

  const handleIncrease = () => {
    updateQuantity(item.id, 1);
  };

  return (
    <Modal
      visible={visible}
      transparent
      animationType="slide"
      onRequestClose={onClose}
    >
      <View style={styles.overlay}>
        {/* Backdrop dismiss pressable */}
        <Pressable
          style={styles.backdrop}
          onPress={onClose}
          accessibilityLabel="Закрыть модальное окно"
        />

        {/* Modal Sheet Container */}
        <View style={styles.sheetContainer}>
          {/* Floating Close Button */}
          <Pressable
            onPress={onClose}
            hitSlop={8}
            style={({ pressed }) => [
              styles.closeButton,
              pressed && styles.closeButtonPressed,
            ]}
            accessibilityRole="button"
            accessibilityLabel="Закрыть детали блюда"
          >
            <X size={20} color="#F8F9FA" strokeWidth={2.4} />
          </Pressable>

          {/* Scrollable Dish Details */}
          <ScrollView
            showsVerticalScrollIndicator={false}
            contentContainerStyle={styles.scrollContent}
          >
            {/* 1. Hero Food Photo */}
            <View style={styles.heroImageContainer}>
              {item.image && !imageError ? (
                <Image
                  source={{ uri: item.image }}
                  style={styles.heroImage}
                  resizeMode="cover"
                  onError={() => setImageError(true)}
                />
              ) : (
                <View style={styles.heroPlaceholder}>
                  <Utensils size={48} color={COLORS.borderV2} />
                </View>
              )}
            </View>

            {/* 2. Main Content Body */}
            <View style={styles.body}>
              {/* Badges Row */}
              <View style={styles.badgesRow}>
                {isChef && (
                  <View style={styles.chefBadge}>
                    <Sparkles size={12} color="#0A0B0E" strokeWidth={2.6} />
                    <Text style={styles.chefBadgeText}>Выбор шефа</Text>
                  </View>
                )}

                {Boolean(categoryLabel) && (
                  <View style={styles.metaBadge}>
                    <Text style={styles.metaBadgeText}>{categoryLabel}</Text>
                  </View>
                )}

                {Boolean(item.weight) && (
                  <View style={styles.metaBadge}>
                    <Text style={styles.metaBadgeText}>{item.weight}</Text>
                  </View>
                )}

                {dietaryList.map((tag, idx) => (
                  <View key={`${tag}-${idx}`} style={styles.dietaryBadge}>
                    <Text style={styles.dietaryBadgeText}>{tag}</Text>
                  </View>
                ))}
              </View>

              {/* Title & Price */}
              <View style={styles.headerBlock}>
                <Text style={styles.title}>{item.name}</Text>
                <Text style={styles.price}>{formatKZT(priceValue)}</Text>
              </View>

              {/* 3. Origin & Story Section */}
              {Boolean(storyText) && (
                <View style={styles.sectionCard}>
                  <View style={styles.sectionHeader}>
                    <ChefHat size={18} color="#D4A373" strokeWidth={2.2} />
                    <Text style={styles.sectionTitle}>
                      История шефа и происхождение ингредиентов
                    </Text>
                  </View>
                  <Text style={styles.storyText}>{storyText}</Text>
                </View>
              )}

              {/* 4. Sensory Flavor Radar Chart */}
              <View style={styles.sectionCard}>
                <View style={styles.sectionHeader}>
                  <Sparkles size={18} color="#D4A373" strokeWidth={2.2} />
                  <Text style={styles.sectionTitle}>
                    Вкусовой профиль (5 осей)
                  </Text>
                </View>
                <View style={styles.radarWrapper}>
                  <FlavorRadarChart
                    profile={flavorProfile}
                    size={190}
                    showLanguageToggle={false}
                    showValueLabels={true}
                  />
                </View>
              </View>

              {/* 5. Sommelier Pairing Card */}
              {Boolean(item.pairing) && (
                <View style={styles.sommelierCard}>
                  <View style={styles.sommelierHeaderRow}>
                    <View style={styles.sommelierIconBox}>
                      <Wine size={18} color="#D4A373" strokeWidth={2.2} />
                    </View>
                    <View style={styles.sommelierHeaderText}>
                      <Text style={styles.sommelierSubtitle}>
                        РЕКОМЕНДАЦИЯ СОМЕЛЬЕ
                      </Text>
                      {Boolean(item.pairing.type) && (
                        <Text style={styles.pairingType}>
                          {item.pairing.type}
                        </Text>
                      )}
                    </View>
                  </View>

                  <Text style={styles.pairingTitle}>
                    {item.pairing.title || item.pairing.name}
                  </Text>

                  {Boolean(
                    item.pairing.description || item.pairing.notes
                  ) && (
                    <Text style={styles.pairingDescription}>
                      {item.pairing.description || item.pairing.notes}
                    </Text>
                  )}

                  {/* Sommelier Extra details if available */}
                  {(Boolean(item.pairing.temperature) ||
                    Boolean(item.pairing.vintage)) && (
                    <View style={styles.sommelierMetaRow}>
                      {Boolean(item.pairing.vintage) && (
                        <View style={styles.sommelierMetaPill}>
                          <Clock size={12} color="#D4A373" />
                          <Text style={styles.sommelierMetaText}>
                            Винтаж: {item.pairing.vintage}
                          </Text>
                        </View>
                      )}
                      {Boolean(item.pairing.temperature) && (
                        <View style={styles.sommelierMetaPill}>
                          <Thermometer size={12} color="#D4A373" />
                          <Text style={styles.sommelierMetaText}>
                            {item.pairing.temperature}
                          </Text>
                        </View>
                      )}
                    </View>
                  )}
                </View>
              )}
            </View>
          </ScrollView>

          {/* 6. Fixed Bottom Action Bar */}
          <View style={styles.bottomBar}>
            {quantity > 0 ? (
              <View style={styles.cartActiveRow}>
                <View style={styles.cartInfoBox}>
                  <View style={styles.cartCheckRow}>
                    <Check size={14} color="#2EC4B6" strokeWidth={2.8} />
                    <Text style={styles.cartInfoText}>
                      В заказе: {quantity} шт
                    </Text>
                  </View>
                  <Text style={styles.cartTotalText}>
                    {formatKZT(priceValue * quantity)}
                  </Text>
                </View>

                <View style={styles.bottomStepper}>
                  <Pressable
                    onPress={handleDecrease}
                    hitSlop={8}
                    style={({ pressed }) => [
                      styles.bottomStepperBtn,
                      pressed && styles.bottomStepperBtnPressed,
                    ]}
                    accessibilityRole="button"
                    accessibilityLabel="Уменьшить количество"
                  >
                    <Minus size={16} color="#D4A373" strokeWidth={2.6} />
                  </Pressable>

                  <Text style={styles.bottomStepperQty}>{quantity}</Text>

                  <Pressable
                    onPress={handleIncrease}
                    hitSlop={8}
                    style={({ pressed }) => [
                      styles.bottomStepperBtnActive,
                      pressed && styles.bottomStepperBtnPressed,
                    ]}
                    accessibilityRole="button"
                    accessibilityLabel="Увеличить количество"
                  >
                    <Plus size={16} color="#0A0B0E" strokeWidth={2.8} />
                  </Pressable>
                </View>
              </View>
            ) : (
              <Pressable
                onPress={handleAdd}
                style={({ pressed }) => [
                  styles.addButton,
                  pressed && styles.addButtonPressed,
                ]}
                accessibilityRole="button"
                accessibilityLabel={`Добавить ${item.name} в заказ`}
              >
                <Plus size={18} color="#0A0B0E" strokeWidth={2.8} />
                <Text style={styles.addButtonText}>
                  Добавить в заказ • {formatKZT(priceValue)}
                </Text>
              </Pressable>
            )}
          </View>
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.78)',
    justifyContent: 'flex-end',
  },
  backdrop: {
    ...StyleSheet.absoluteFill,
  },
  sheetContainer: {
    maxHeight: '92%',
    height: '92%',
    backgroundColor: '#0A0B0E',
    borderTopLeftRadius: 26,
    borderTopRightRadius: 26,
    overflow: 'hidden',
    width: '100%',
    maxWidth: 620,
    alignSelf: 'center',
  },
  closeButton: {
    position: 'absolute',
    top: 16,
    right: 16,
    zIndex: 20,
    width: 38,
    height: 38,
    borderRadius: 19,
    backgroundColor: 'rgba(10, 11, 14, 0.75)',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.2)',
    alignItems: 'center',
    justifyContent: 'center',
    ...SHADOWS.subtle,
  },
  closeButtonPressed: {
    opacity: 0.8,
    transform: [{ scale: 0.94 }],
  },
  scrollContent: {
    paddingBottom: 24,
  },
  heroImageContainer: {
    width: '100%',
    height: 260,
    backgroundColor: '#16191F',
  },
  heroImage: {
    width: '100%',
    height: '100%',
  },
  heroPlaceholder: {
    width: '100%',
    height: '100%',
    backgroundColor: '#16191F',
    alignItems: 'center',
    justifyContent: 'center',
  },
  body: {
    paddingHorizontal: 20,
    paddingTop: 18,
  },
  badgesRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginBottom: 14,
    alignItems: 'center',
  },
  chefBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: '#D4A373',
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: 8,
  },
  chefBadgeText: {
    color: '#0A0B0E',
    fontFamily: FONTS.sans,
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 0.5,
  },
  metaBadge: {
    backgroundColor: '#13161F',
    borderWidth: 1,
    borderColor: '#1E2330',
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: 8,
  },
  metaBadgeText: {
    color: '#8E95A5',
    fontFamily: FONTS.sans,
    fontSize: 11,
    fontWeight: '600',
  },
  dietaryBadge: {
    backgroundColor: 'rgba(212, 163, 115, 0.1)',
    borderWidth: 1,
    borderColor: 'rgba(212, 163, 115, 0.3)',
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: 8,
  },
  dietaryBadgeText: {
    color: '#D4A373',
    fontFamily: FONTS.sans,
    fontSize: 11,
    fontWeight: '600',
  },
  headerBlock: {
    marginBottom: 20,
  },
  title: {
    color: '#F8F9FA',
    fontFamily: FONTS.serif,
    fontSize: 24,
    fontWeight: '700',
    lineHeight: 32,
    marginBottom: 6,
  },
  price: {
    color: '#D4A373',
    fontFamily: FONTS.mono,
    fontSize: 22,
    fontWeight: '700',
    letterSpacing: 0.5,
  },
  sectionCard: {
    backgroundColor: '#13161F',
    borderWidth: 1,
    borderColor: '#1E2330',
    borderRadius: 16,
    padding: 16,
    marginBottom: 16,
  },
  sectionHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginBottom: 10,
  },
  sectionTitle: {
    color: '#F8F9FA',
    fontFamily: FONTS.sans,
    fontSize: 15,
    fontWeight: '700',
    letterSpacing: 0.3,
  },
  storyText: {
    color: '#A2A9B8',
    fontFamily: FONTS.sans,
    fontSize: 14,
    lineHeight: 22,
    fontWeight: '400',
  },
  radarWrapper: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 4,
  },
  sommelierCard: {
    backgroundColor: 'rgba(140, 45, 56, 0.22)',
    borderWidth: 1,
    borderColor: 'rgba(229, 169, 98, 0.35)',
    borderRadius: 16,
    padding: 18,
    marginBottom: 20,
  },
  sommelierHeaderRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    marginBottom: 10,
  },
  sommelierIconBox: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: 'rgba(229, 169, 98, 0.15)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  sommelierHeaderText: {
    flex: 1,
  },
  sommelierSubtitle: {
    color: '#D4A373',
    fontFamily: FONTS.sans,
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.2,
  },
  pairingType: {
    color: '#8E95A5',
    fontFamily: FONTS.sans,
    fontSize: 12,
    fontWeight: '500',
  },
  pairingTitle: {
    color: '#F8F9FA',
    fontFamily: FONTS.serif,
    fontSize: 18,
    fontWeight: '700',
    lineHeight: 24,
    marginBottom: 6,
  },
  pairingDescription: {
    color: 'rgba(248, 249, 250, 0.85)',
    fontFamily: FONTS.sans,
    fontSize: 13,
    lineHeight: 20,
    fontWeight: '400',
  },
  sommelierMetaRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginTop: 12,
  },
  sommelierMetaPill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: 'rgba(0, 0, 0, 0.3)',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 6,
  },
  sommelierMetaText: {
    color: '#D4A373',
    fontFamily: FONTS.sans,
    fontSize: 11,
    fontWeight: '500',
  },
  bottomBar: {
    backgroundColor: '#13161F',
    borderTopWidth: 1,
    borderTopColor: '#1E2330',
    paddingHorizontal: 20,
    paddingVertical: 16,
    paddingBottom: Platform.OS === 'ios' ? 28 : 16,
  },
  addButton: {
    height: 52,
    borderRadius: 14,
    backgroundColor: '#D4A373',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    ...SHADOWS.glowChampagne,
  },
  addButtonPressed: {
    opacity: 0.9,
    transform: [{ scale: 0.98 }],
  },
  addButtonText: {
    color: '#0A0B0E',
    fontFamily: FONTS.sans,
    fontSize: 16,
    fontWeight: '700',
  },
  cartActiveRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 12,
  },
  cartInfoBox: {
    flex: 1,
  },
  cartCheckRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  cartInfoText: {
    color: '#F8F9FA',
    fontFamily: FONTS.sans,
    fontSize: 14,
    fontWeight: '700',
  },
  cartTotalText: {
    color: '#D4A373',
    fontFamily: FONTS.mono,
    fontSize: 15,
    fontWeight: '700',
    marginTop: 2,
  },
  bottomStepper: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  bottomStepperBtn: {
    width: 40,
    height: 40,
    borderRadius: 20,
    borderWidth: 1,
    borderColor: '#D4A373',
    backgroundColor: '#13161F',
    alignItems: 'center',
    justifyContent: 'center',
  },
  bottomStepperBtnActive: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: '#D4A373',
    alignItems: 'center',
    justifyContent: 'center',
  },
  bottomStepperBtnPressed: {
    opacity: 0.8,
    transform: [{ scale: 0.92 }],
  },
  bottomStepperQty: {
    color: '#F8F9FA',
    fontFamily: FONTS.sans,
    fontSize: 17,
    fontWeight: '700',
    minWidth: 26,
    textAlign: 'center',
  },
});

export default DishDetailModal;
