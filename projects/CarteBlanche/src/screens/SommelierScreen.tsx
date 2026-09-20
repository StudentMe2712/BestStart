import React, { useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  Image,
  TouchableOpacity,
  StyleSheet,
  Platform,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import {
  Wine,
  Sparkles,
  Thermometer,
  Clock,
  Plus,
  Check,
  Compass,
  ArrowRight,
  Award,
} from 'lucide-react-native';
import { MENU_ITEMS } from '../data/menuDataKZT';
import { MenuItem } from '../types/menu';
import { COLORS, FONTS, RADIUS, SHADOWS, SPACING } from '../constants/theme';
import { formatKZT } from '../utils/currency';
import { useCart } from '../context/CartContext';
import { DishDetailModal } from '../components/menu/DishDetailModal';

export interface SommelierScreenProps {
  onSelectDish?: (dish: MenuItem) => void;
}

type SommelierFilter = 'all' | 'cellar' | 'pairings';

/**
 * SommelierScreen (Stage 5)
 * Haute Cuisine Cellar & Sommelier Tasting Guide.
 * Features:
 * - Dynamic Island SafeArea top clearance (56px)
 * - Chef Sommelier editorial welcome quote
 * - Cellared signature wines and curated dish pairings
 * - Sensory flavor overview & harmony tags
 * - Real-time add-to-cart functionality
 */
export const SommelierScreen: React.FC<SommelierScreenProps> = ({
  onSelectDish,
}) => {
  let insets = { top: 0, bottom: 0, left: 0, right: 0 };
  try {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    insets = useSafeAreaInsets();
  } catch {
    // Fallback if rendered outside SafeAreaProvider
  }

  const { addToCart, getItemQuantity } = useCart();
  const [selectedFilter, setSelectedFilter] = useState<SommelierFilter>('all');
  const [modalDish, setModalDish] = useState<MenuItem | null>(null);

  const paddingTop = Platform.OS === 'web' ? 56 : Math.max(insets.top + 12, 52);

  // Cellared items (category === 'drinks')
  const cellarItems = MENU_ITEMS.filter((item) => item.category === 'drinks');

  // Pairings items (dishes that have a curated pairing)
  const pairedDishes = MENU_ITEMS.filter(
    (item) => item.category !== 'drinks' && item.pairing && item.pairing.title
  );

  const handleDishClick = (dish: MenuItem) => {
    if (onSelectDish) {
      onSelectDish(dish);
    } else {
      setModalDish(dish);
    }
  };

  return (
    <View style={styles.container}>
      {/* 1. Header */}
      <View style={[styles.header, { paddingTop }]}>
        <View style={styles.badgeRow}>
          <View style={styles.badgePill}>
            <Wine size={13} color="#D4A373" />
            <Text style={styles.badgeText}>ENOTHEQUE & TASTING GUIDE</Text>
          </View>
        </View>

        <Text style={styles.title}>Винный погреб & Сомелье</Text>
        <Text style={styles.subtitle}>
          Авторская дегустационная карта и эногастрономические пары
        </Text>
      </View>

      <ScrollView
        showsVerticalScrollIndicator={false}
        contentContainerStyle={styles.scrollContent}
      >
        {/* 2. Chef Sommelier Manifesto Card */}
        <View style={styles.chefCard}>
          <View style={styles.chefHeader}>
            <View style={styles.chefIconBox}>
              <Award size={20} color="#D4A373" strokeWidth={2.2} />
            </View>
            <View style={styles.chefTitleBox}>
              <Text style={styles.chefRole}>CARTE BLANCHE SOMMELIER</Text>
              <Text style={styles.chefTitle}>
                Гармония терруара и вкуса от шеф-сомелье Carte Blanche
              </Text>
            </View>
          </View>

          <Text style={styles.chefQuote}>
            «Мы верим, что идеальный винный пейринг — это не просто сопровождение,
            а кульминация гастрономического опыта, где ноты дуба, минеральности и
            танинов открывают новые грани авторских блюд шефа.»
          </Text>
        </View>

        {/* 3. Category Filter Tabs */}
        <View style={styles.filterRow}>
          <TouchableOpacity
            style={[
              styles.filterPill,
              selectedFilter === 'all' && styles.filterPillActive,
            ]}
            onPress={() => setSelectedFilter('all')}
            accessibilityRole="tab"
            accessibilityState={{ selected: selectedFilter === 'all' }}
          >
            <Text
              style={[
                styles.filterText,
                selectedFilter === 'all' && styles.filterTextActive,
              ]}
            >
              Вся карта ({cellarItems.length + pairedDishes.length})
            </Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={[
              styles.filterPill,
              selectedFilter === 'cellar' && styles.filterPillActive,
            ]}
            onPress={() => setSelectedFilter('cellar')}
            accessibilityRole="tab"
            accessibilityState={{ selected: selectedFilter === 'cellar' }}
          >
            <Text
              style={[
                styles.filterText,
                selectedFilter === 'cellar' && styles.filterTextActive,
              ]}
            >
              Погреб ({cellarItems.length})
            </Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={[
              styles.filterPill,
              selectedFilter === 'pairings' && styles.filterPillActive,
            ]}
            onPress={() => setSelectedFilter('pairings')}
            accessibilityRole="tab"
            accessibilityState={{ selected: selectedFilter === 'pairings' }}
          >
            <Text
              style={[
                styles.filterText,
                selectedFilter === 'pairings' && styles.filterTextActive,
              ]}
            >
              Винные пары ({pairedDishes.length})
            </Text>
          </TouchableOpacity>
        </View>

        {/* 4. Cellared Wines & Bar (Grand Crus & Infusions) */}
        {(selectedFilter === 'all' || selectedFilter === 'cellar') && (
          <View style={styles.section}>
            <View style={styles.sectionHeader}>
              <Wine size={18} color="#D4A373" strokeWidth={2.4} />
              <Text style={styles.sectionTitle}>Флагманские позиции погреба</Text>
            </View>

            {cellarItems.map((drink) => {
              const qty = getItemQuantity(drink.id);
              const price = drink.priceKZT ?? drink.price ?? 0;

              return (
                <View key={drink.id} style={styles.cellarCard}>
                  {/* Photo Header */}
                  <View style={styles.cellarImageContainer}>
                    <Image
                      source={{ uri: drink.image }}
                      style={styles.cellarImage}
                      resizeMode="cover"
                    />
                    <View style={styles.cellarOverlayBadge}>
                      <Text style={styles.cellarOverlayText}>{drink.weight}</Text>
                    </View>
                  </View>

                  {/* Details */}
                  <View style={styles.cellarBody}>
                    <Text style={styles.cellarTitle}>{drink.name}</Text>
                    <Text style={styles.cellarDesc}>{drink.shortDescription}</Text>

                    {/* Sommelier Pairing notes & terroir */}
                    <View style={styles.cellarMetaRow}>
                      {Boolean(drink.origin) && (
                        <View style={styles.metaPill}>
                          <Compass size={12} color="#D4A373" />
                          <Text style={styles.metaText}>{drink.origin}</Text>
                        </View>
                      )}
                      {Boolean(drink.pairing?.temperature) && (
                        <View style={styles.metaPill}>
                          <Thermometer size={12} color="#D4A373" />
                          <Text style={styles.metaText}>
                            {drink.pairing.temperature}
                          </Text>
                        </View>
                      )}
                      {Boolean(drink.pairing?.vintage) && (
                        <View style={styles.metaPill}>
                          <Clock size={12} color="#D4A373" />
                          <Text style={styles.metaText}>
                            {drink.pairing.vintage}
                          </Text>
                        </View>
                      )}
                    </View>

                    {/* Action Row */}
                    <View style={styles.cellarActionRow}>
                      <Text style={styles.cellarPrice}>{formatKZT(price)}</Text>

                      <TouchableOpacity
                        style={[
                          styles.addWineBtn,
                          qty > 0 && styles.addWineBtnAdded,
                        ]}
                        onPress={() => addToCart(drink)}
                        activeOpacity={0.8}
                        accessibilityRole="button"
                        accessibilityLabel={`Добавить ${drink.name} в корзину`}
                      >
                        {qty > 0 ? (
                          <>
                            <Check size={14} color="#0A0B0E" strokeWidth={2.8} />
                            <Text style={styles.addWineBtnText}>
                              В заказе ({qty})
                            </Text>
                          </>
                        ) : (
                          <>
                            <Plus size={14} color="#0A0B0E" strokeWidth={2.8} />
                            <Text style={styles.addWineBtnText}>В заказ</Text>
                          </>
                        )}
                      </TouchableOpacity>
                    </View>
                  </View>
                </View>
              );
            })}
          </View>
        )}

        {/* 5. Curated Wine Pairings for Fine Dining Dishes */}
        {(selectedFilter === 'all' || selectedFilter === 'pairings') && (
          <View style={styles.section}>
            <View style={styles.sectionHeader}>
              <Sparkles size={18} color="#D4A373" strokeWidth={2.4} />
              <Text style={styles.sectionTitle}>
                Эногастрономические дуэты
              </Text>
            </View>

            {pairedDishes.map((dish) => {
              const pairing = dish.pairing;
              const dishPrice = dish.priceKZT ?? dish.price ?? 0;
              const dishQty = getItemQuantity(dish.id);

              return (
                <View key={`pairing-${dish.id}`} style={styles.pairingCard}>
                  {/* Wine Header */}
                  <View style={styles.wineHeaderRow}>
                    <View style={styles.wineBadge}>
                      <Wine size={16} color="#D4A373" />
                      <Text style={styles.wineBadgeText}>
                        {pairing.type || 'Рекомендованная пара'}
                      </Text>
                    </View>

                    {Boolean(pairing.temperature) && (
                      <View style={styles.tempBadge}>
                        <Thermometer size={12} color="#8E95A5" />
                        <Text style={styles.tempText}>{pairing.temperature}</Text>
                      </View>
                    )}
                  </View>

                  <Text style={styles.wineTitle}>{pairing.title || pairing.name}</Text>

                  {Boolean(pairing.producer || pairing.vintage) && (
                    <Text style={styles.wineProducer}>
                      {[pairing.producer, pairing.vintage]
                        .filter(Boolean)
                        .join(' • ')}
                    </Text>
                  )}

                  <Text style={styles.wineNotes}>
                    {pairing.description || pairing.notes}
                  </Text>

                  {/* Harmony Link to Dish */}
                  <TouchableOpacity
                    style={styles.dishCompanionCard}
                    onPress={() => handleDishClick(dish)}
                    activeOpacity={0.75}
                    accessibilityRole="button"
                    accessibilityLabel={`Открыть блюдо ${dish.name}`}
                  >
                    <Image
                      source={{ uri: dish.image }}
                      style={styles.dishThumb}
                      resizeMode="cover"
                    />

                    <View style={styles.dishInfo}>
                      <Text style={styles.dishPairLabel}>ПАРА К БЛЮДУ</Text>
                      <Text style={styles.dishName} numberOfLines={1}>
                        {dish.name}
                      </Text>
                      <Text style={styles.dishPriceText}>
                        {formatKZT(dishPrice)}
                      </Text>
                    </View>

                    <TouchableOpacity
                      style={[
                        styles.quickAddDishBtn,
                        dishQty > 0 && styles.quickAddDishBtnActive,
                      ]}
                      onPress={(e) => {
                        e.stopPropagation();
                        addToCart(dish);
                      }}
                      accessibilityRole="button"
                      accessibilityLabel={`Добавить ${dish.name} в корзину`}
                    >
                      {dishQty > 0 ? (
                        <Check size={14} color="#0A0B0E" strokeWidth={2.8} />
                      ) : (
                        <Plus size={14} color="#0A0B0E" strokeWidth={2.8} />
                      )}
                    </TouchableOpacity>
                  </TouchableOpacity>
                </View>
              );
            })}
          </View>
        )}
      </ScrollView>

      {/* Dish Detail Modal if triggered internally */}
      <DishDetailModal
        item={modalDish}
        visible={modalDish !== null}
        onClose={() => setModalDish(null)}
      />
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0A0B0E',
  },
  header: {
    backgroundColor: '#0A0B0E',
    borderBottomWidth: 1,
    borderBottomColor: '#1E2330',
    paddingHorizontal: 16,
    paddingBottom: 16,
    gap: 6,
  },
  badgeRow: {
    flexDirection: 'row',
  },
  badgePill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: 'rgba(212, 163, 115, 0.1)',
    borderWidth: 1,
    borderColor: 'rgba(212, 163, 115, 0.3)',
    borderRadius: 8,
    paddingHorizontal: 8,
    paddingVertical: 4,
  },
  badgeText: {
    fontFamily: FONTS.mono,
    fontSize: 10,
    fontWeight: '700',
    color: '#D4A373',
    letterSpacing: 1,
  },
  title: {
    fontFamily: FONTS.serif,
    fontSize: 22,
    fontWeight: '700',
    color: '#F8F9FA',
    marginTop: 4,
  },
  subtitle: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    color: '#8E95A5',
  },
  scrollContent: {
    paddingHorizontal: 16,
    paddingTop: 16,
    paddingBottom: 110,
    gap: 20,
  },
  chefCard: {
    backgroundColor: 'rgba(140, 45, 56, 0.18)',
    borderWidth: 1,
    borderColor: 'rgba(212, 163, 115, 0.35)',
    borderRadius: 18,
    padding: 16,
    gap: 12,
  },
  chefHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },
  chefIconBox: {
    width: 38,
    height: 38,
    borderRadius: 19,
    backgroundColor: 'rgba(212, 163, 115, 0.18)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  chefTitleBox: {
    flex: 1,
  },
  chefRole: {
    fontFamily: FONTS.mono,
    fontSize: 10,
    fontWeight: '700',
    color: '#D4A373',
    letterSpacing: 1,
  },
  chefTitle: {
    fontFamily: FONTS.serif,
    fontSize: 15,
    fontWeight: '700',
    color: '#F8F9FA',
    lineHeight: 20,
  },
  chefQuote: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    lineHeight: 20,
    color: 'rgba(248, 249, 250, 0.85)',
    fontStyle: 'italic',
  },
  filterRow: {
    flexDirection: 'row',
    gap: 8,
  },
  filterPill: {
    flex: 1,
    paddingVertical: 10,
    paddingHorizontal: 8,
    borderRadius: 12,
    backgroundColor: '#13161F',
    borderWidth: 1,
    borderColor: '#1E2330',
    alignItems: 'center',
    justifyContent: 'center',
    ...Platform.select({
      web: {
        cursor: 'pointer',
      } as any,
    }),
  },
  filterPillActive: {
    backgroundColor: 'rgba(212, 163, 115, 0.15)',
    borderColor: '#D4A373',
  },
  filterText: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    color: '#8E95A5',
    fontWeight: '600',
    textAlign: 'center',
  },
  filterTextActive: {
    color: '#D4A373',
    fontWeight: '700',
  },
  section: {
    gap: 14,
  },
  sectionHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginTop: 4,
  },
  sectionTitle: {
    fontFamily: FONTS.serif,
    fontSize: 17,
    fontWeight: '700',
    color: '#F8F9FA',
  },
  cellarCard: {
    backgroundColor: '#13161F',
    borderWidth: 1,
    borderColor: '#1E2330',
    borderRadius: 18,
    overflow: 'hidden',
  },
  cellarImageContainer: {
    width: '100%',
    height: 150,
    position: 'relative',
    backgroundColor: '#1A1E26',
  },
  cellarImage: {
    width: '100%',
    height: '100%',
  },
  cellarOverlayBadge: {
    position: 'absolute',
    top: 10,
    right: 10,
    backgroundColor: 'rgba(10, 11, 14, 0.75)',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.15)',
    borderRadius: 6,
    paddingHorizontal: 8,
    paddingVertical: 3,
  },
  cellarOverlayText: {
    fontFamily: FONTS.mono,
    fontSize: 11,
    fontWeight: '600',
    color: '#F8F9FA',
  },
  cellarBody: {
    padding: 16,
    gap: 10,
  },
  cellarTitle: {
    fontFamily: FONTS.serif,
    fontSize: 17,
    fontWeight: '700',
    color: '#F8F9FA',
    lineHeight: 22,
  },
  cellarDesc: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    lineHeight: 18,
    color: '#8E95A5',
  },
  cellarMetaRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 6,
    marginTop: 2,
  },
  metaPill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: '#16191F',
    borderWidth: 1,
    borderColor: '#1E2330',
    borderRadius: 6,
    paddingHorizontal: 8,
    paddingVertical: 4,
  },
  metaText: {
    fontFamily: FONTS.sans,
    fontSize: 11,
    color: '#D4A373',
    fontWeight: '500',
  },
  cellarActionRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingTop: 6,
  },
  cellarPrice: {
    fontFamily: FONTS.mono,
    fontSize: 18,
    fontWeight: '700',
    color: '#D4A373',
  },
  addWineBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: '#D4A373',
    paddingHorizontal: 16,
    paddingVertical: 9,
    borderRadius: 12,
    ...Platform.select({
      web: {
        cursor: 'pointer',
      } as any,
    }),
  },
  addWineBtnAdded: {
    backgroundColor: '#2EC4B6',
  },
  addWineBtnText: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    fontWeight: '700',
    color: '#0A0B0E',
  },
  pairingCard: {
    backgroundColor: '#13161F',
    borderWidth: 1,
    borderColor: '#1E2330',
    borderRadius: 18,
    padding: 16,
    gap: 8,
  },
  wineHeaderRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 2,
  },
  wineBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  wineBadgeText: {
    fontFamily: FONTS.mono,
    fontSize: 11,
    fontWeight: '700',
    color: '#D4A373',
    letterSpacing: 0.5,
  },
  tempBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },
  tempText: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    color: '#8E95A5',
  },
  wineTitle: {
    fontFamily: FONTS.serif,
    fontSize: 16,
    fontWeight: '700',
    color: '#F8F9FA',
    lineHeight: 22,
  },
  wineProducer: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    color: '#D4A373',
    fontWeight: '500',
  },
  wineNotes: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    lineHeight: 19,
    color: 'rgba(248, 249, 250, 0.8)',
    marginBottom: 4,
  },
  dishCompanionCard: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#16191F',
    borderWidth: 1,
    borderColor: 'rgba(212, 163, 115, 0.25)',
    borderRadius: 14,
    padding: 10,
    gap: 10,
    marginTop: 4,
    ...Platform.select({
      web: {
        cursor: 'pointer',
      } as any,
    }),
  },
  dishThumb: {
    width: 48,
    height: 48,
    borderRadius: 10,
  },
  dishInfo: {
    flex: 1,
    gap: 2,
  },
  dishPairLabel: {
    fontFamily: FONTS.mono,
    fontSize: 9,
    fontWeight: '700',
    color: '#8E95A5',
    letterSpacing: 0.8,
  },
  dishName: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    fontWeight: '600',
    color: '#F8F9FA',
  },
  dishPriceText: {
    fontFamily: FONTS.mono,
    fontSize: 12,
    fontWeight: '700',
    color: '#D4A373',
  },
  quickAddDishBtn: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: '#D4A373',
    alignItems: 'center',
    justifyContent: 'center',
  },
  quickAddDishBtnActive: {
    backgroundColor: '#2EC4B6',
  },
});

export default SommelierScreen;
