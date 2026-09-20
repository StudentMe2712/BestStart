import React, { useState } from 'react';
import { View, StyleSheet } from 'react-native';
import { BottomTabBar, TabType } from './src/components/common/BottomTabBar';
import { MenuScreen } from './src/screens/MenuScreen';
import { SommelierScreen } from './src/screens/SommelierScreen';
import { CartScreen } from './src/screens/CartScreen';

export interface AppProps {
  initialTab?: TabType;
}

/**
 * Root Carte Blanche V2 Application Component.
 * Features 3-tab bottom navigation (Menu, Sommelier, Cart)
 * with persistent luxury Dark Luxe V2 styling.
 */
export function App({ initialTab = 'menu' }: AppProps) {
  const [activeTab, setActiveTab] = useState<TabType>(initialTab);

  return (
    <View style={styles.container}>
      <View style={styles.screenContainer}>
        {activeTab === 'menu' && (
          <MenuScreen onNavigateToCart={() => setActiveTab('cart')} />
        )}
        {activeTab === 'sommelier' && (
          <SommelierScreen />
        )}
        {activeTab === 'cart' && (
          <CartScreen onNavigateToMenu={() => setActiveTab('menu')} />
        )}
      </View>

      <BottomTabBar activeTab={activeTab} onTabChange={setActiveTab} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0A0B0E',
  },
  screenContainer: {
    flex: 1,
  },
});

export default App;
