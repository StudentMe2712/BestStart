import React from 'react';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { COLORS } from '../src/constants/theme';
import { TastingProvider } from '../src/context/TastingContext';

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <TastingProvider>
        <StatusBar style="light" />
        <Stack
          screenOptions={{
            headerStyle: {
              backgroundColor: COLORS.obsidianCanvas,
            },
            headerTintColor: COLORS.textPrimary,
            contentStyle: {
              backgroundColor: COLORS.obsidianCanvas,
            },
          }}
        >
          <Stack.Screen
            name="index"
            options={{
              headerShown: false,
            }}
          />
          <Stack.Screen
            name="dish/[id]"
            options={{
              headerShown: false,
              presentation: 'modal',
              animation: 'slide_from_bottom',
            }}
          />
          <Stack.Screen
            name="planner"
            options={{
              headerShown: false,
              presentation: 'card',
              animation: 'slide_from_right',
            }}
          />
        </Stack>
      </TastingProvider>
    </SafeAreaProvider>
  );
}
