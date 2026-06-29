import {
  Beaker,
  Bookmark,
  Bug,
  CircleCheck,
  Code,
  Flag,
  FolderOpen,
  Heart,
  Lightbulb,
  Quote,
  Sparkles,
  Star,
  Tag,
  Target,
  TriangleAlert,
  Zap,
  type LucideIcon,
} from 'lucide-react';

// Curated Lucide set used for category icons (system + custom). Color always travels with a
// label/icon so meaning never rests on color alone.
export const CATEGORY_ICONS: Record<string, LucideIcon> = {
  Lightbulb,
  CircleCheck,
  TriangleAlert,
  Quote,
  Tag,
  Flag,
  Bookmark,
  Heart,
  Zap,
  Code,
  Beaker,
  Target,
  FolderOpen,
  Star,
  Bug,
  Sparkles,
};

/** Icon keys offered when creating a custom category. */
export const CUSTOM_ICON_CHOICES = Object.keys(CATEGORY_ICONS);

/** Render a category icon by key, falling back to a generic tag. */
export function CategoryIcon({
  name,
  size = 13,
  strokeWidth = 2,
  className,
}: {
  name: string;
  size?: number;
  strokeWidth?: number;
  className?: string;
}) {
  const Icon = CATEGORY_ICONS[name] ?? Tag;
  return <Icon size={size} strokeWidth={strokeWidth} className={className} />;
}
