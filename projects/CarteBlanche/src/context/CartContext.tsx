import React, {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
  useMemo,
  ReactNode,
} from 'react';
import { Platform } from 'react-native';
import { MenuItem } from '../types/menu';

export interface CartItem {
  menuItem: MenuItem;
  quantity: number;
}

export interface CartContextType {
  items: CartItem[];
  itemsCount: number; // total sum of quantities
  totalAmount: number; // sum of (item.menuItem.priceKZT * item.quantity)
  tipPercentage: number; // 0 | 10 | 15, default 10
  tipAmount: number; // Math.round(totalAmount * (tipPercentage / 100))
  grandTotal: number; // totalAmount + tipAmount
  tableNumber: string; // default "Стол №4"
  setTableNumber: (table: string) => void;
  setTipPercentage: (tip: number) => void;
  addToCart: (item: MenuItem) => void;
  removeFromCart: (itemId: string) => void;
  updateQuantity: (itemId: string, delta: number) => void; // if quantity + delta <= 0, remove item
  getItemQuantity: (itemId: string) => number;
  clearCart: () => void;
}

const STORAGE_KEY = 'carte_blanche_cart_v2';
const DEFAULT_TABLE_NUMBER = 'Стол №4';
const DEFAULT_TIP_PERCENTAGE = 10;

interface PersistedCartState {
  items?: CartItem[];
  tipPercentage?: number;
  tableNumber?: string;
}

/**
 * Safely loads persisted cart state from localStorage in Web environment.
 */
const loadInitialCartState = (): {
  items: CartItem[];
  tipPercentage: number;
  tableNumber: string;
} => {
  if (Platform.OS === 'web' && typeof window !== 'undefined' && window.localStorage) {
    try {
      const raw = window.localStorage.getItem(STORAGE_KEY);
      if (raw) {
        const parsed: PersistedCartState = JSON.parse(raw);
        return {
          items: Array.isArray(parsed.items) ? parsed.items : [],
          tipPercentage:
            typeof parsed.tipPercentage === 'number'
              ? parsed.tipPercentage
              : DEFAULT_TIP_PERCENTAGE,
          tableNumber:
            typeof parsed.tableNumber === 'string' && parsed.tableNumber.trim()
              ? parsed.tableNumber
              : DEFAULT_TABLE_NUMBER,
        };
      }
    } catch {
      // Storage access or parse error fallback
    }
  }

  return {
    items: [],
    tipPercentage: DEFAULT_TIP_PERCENTAGE,
    tableNumber: DEFAULT_TABLE_NUMBER,
  };
};

const CartContext = createContext<CartContextType | undefined>(undefined);

export interface CartProviderProps {
  children: ReactNode;
}

export const CartProvider: React.FC<CartProviderProps> = ({ children }) => {
  const initial = useMemo(() => loadInitialCartState(), []);

  const [items, setItems] = useState<CartItem[]>(initial.items);
  const [tipPercentage, setTipPercentageState] = useState<number>(initial.tipPercentage);
  const [tableNumber, setTableNumberState] = useState<string>(initial.tableNumber);

  // Sync state to localStorage on changes (Web only, try/catch protected)
  useEffect(() => {
    if (Platform.OS === 'web' && typeof window !== 'undefined' && window.localStorage) {
      try {
        const payload: PersistedCartState = {
          items,
          tipPercentage,
          tableNumber,
        };
        window.localStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
      } catch {
        // Safe fail-silent if quota exceeded or disabled
      }
    }
  }, [items, tipPercentage, tableNumber]);

  const setTableNumber = useCallback((table: string) => {
    setTableNumberState(table || DEFAULT_TABLE_NUMBER);
  }, []);

  const setTipPercentage = useCallback((tip: number) => {
    setTipPercentageState(tip);
  }, []);

  const addToCart = useCallback((item: MenuItem) => {
    setItems((prev) => {
      const index = prev.findIndex((ci) => ci.menuItem.id === item.id);
      if (index > -1) {
        const next = [...prev];
        next[index] = {
          ...next[index],
          quantity: next[index].quantity + 1,
        };
        return next;
      }
      return [...prev, { menuItem: item, quantity: 1 }];
    });
  }, []);

  const removeFromCart = useCallback((itemId: string) => {
    setItems((prev) => prev.filter((ci) => ci.menuItem.id !== itemId));
  }, []);

  const updateQuantity = useCallback((itemId: string, delta: number) => {
    setItems((prev) => {
      return prev.flatMap((ci) => {
        if (ci.menuItem.id === itemId) {
          const nextQty = ci.quantity + delta;
          if (nextQty <= 0) {
            return [];
          }
          return [{ ...ci, quantity: nextQty }];
        }
        return [ci];
      });
    });
  }, []);

  const getItemQuantity = useCallback(
    (itemId: string): number => {
      const found = items.find((ci) => ci.menuItem.id === itemId);
      return found ? found.quantity : 0;
    },
    [items]
  );

  const clearCart = useCallback(() => {
    setItems([]);
  }, []);

  const itemsCount = useMemo(() => {
    return items.reduce((sum, item) => sum + item.quantity, 0);
  }, [items]);

  const totalAmount = useMemo(() => {
    return items.reduce((sum, item) => {
      const price = item.menuItem.priceKZT ?? item.menuItem.price ?? 0;
      return sum + price * item.quantity;
    }, 0);
  }, [items]);

  const tipAmount = useMemo(() => {
    return Math.round(totalAmount * (tipPercentage / 100));
  }, [totalAmount, tipPercentage]);

  const grandTotal = useMemo(() => {
    return totalAmount + tipAmount;
  }, [totalAmount, tipAmount]);

  const contextValue = useMemo<CartContextType>(
    () => ({
      items,
      itemsCount,
      totalAmount,
      tipPercentage,
      tipAmount,
      grandTotal,
      tableNumber,
      setTableNumber,
      setTipPercentage,
      addToCart,
      removeFromCart,
      updateQuantity,
      getItemQuantity,
      clearCart,
    }),
    [
      items,
      itemsCount,
      totalAmount,
      tipPercentage,
      tipAmount,
      grandTotal,
      tableNumber,
      setTableNumber,
      setTipPercentage,
      addToCart,
      removeFromCart,
      updateQuantity,
      getItemQuantity,
      clearCart,
    ]
  );

  return <CartContext.Provider value={contextValue}>{children}</CartContext.Provider>;
};

export const useCart = (): CartContextType => {
  const context = useContext(CartContext);
  if (!context) {
    throw new Error('useCart must be used within a CartProvider');
  }
  return context;
};
