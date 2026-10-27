---
name: ios-hig-design
description: Apple Human Interface Guidelines (HIG) standards, tokens, and UX patterns for iOS 18 web and native applications.
---

# Apple HIG Design System & iOS 18 Architecture Standard

This skill codifies Apple Human Interface Guidelines (HIG) for iOS 18+ dark mode web and mobile applications within the BestStart framework.

---

## 1. Core Paradigm: "One Big Action" (Focal Point Clarity)

Modern iOS design avoids cognitive overload. Rather than stacking multiple cards, toolbars, and conflicting calls-to-action on a single screen:
* **One Primary Focal Point**: Every screen has a singular dominant action (Hero Deck / Main CTA).
* **Progressive Disclosure**: Secondary parameters (mood, duration, budget) belong in a native half-screen Bottom Sheet (`UISheetPresentationController` pattern), invoked on demand.
* **No "Card in Card in Card"**: Avoid nested boxes with borders. Screens feel spacious, breathing, and purposeful.
* **Tactile Primary Controls**: Dominant CTA buttons are tall (**56px**), generously rounded (**18px** radius), with vibrant gradients and matching ambient glow.

---

## 2. Layering & Surface Tokens (No "Cemetery of Grey Borders")

Authentic iOS apps (Bumble, Tinder, Apple Fitness+, Headspace, Apple Music) do **not** outline every element with `1px solid rgba(255,255,255,0.1)`. Elements are distinguished solely by depth, surface color, and ambient drop shadows.

```css
:root {
  /* Canvas Background */
  --ios-bg-primary: #000000;
  
  /* Elevated Surfaces */
  --ios-surface-1: #161618;          /* Base floating cards */
  --ios-surface-2: #1C1C1E;          /* Interactive pills, secondary items */
  --ios-surface-3: #2C2C2E;          /* Active / pressed items */
  --ios-surface-glass: rgba(28, 28, 32, 0.75); /* Frosted nav & tab bars */

  /* Shadows (Depth instead of borders) */
  --ios-shadow-card: 0 20px 40px rgba(0, 0, 0, 0.60);
  --ios-shadow-glow-pink: 0 10px 24px rgba(255, 55, 95, 0.35);
  --ios-shadow-pill: inset 0 0.5px 0 rgba(255, 255, 255, 0.12);

  /* Apple System Colors */
  --ios-pink: #FF375F;
  --ios-purple: #BF5AF2;
  --ios-amber: #FFD60A;
  --ios-teal: #64D2FF;
  --ios-green: #30D158;

  /* Typography Colors */
  --ios-label-primary: #FFFFFF;
  --ios-label-secondary: rgba(235, 235, 245, 0.60);
  --ios-label-tertiary: rgba(235, 235, 245, 0.32);
}
```

### Golden Rule:
```css
/* NEVER DO THIS IN iOS DARK MODE: */
.card { border: 1px solid rgba(255, 255, 255, 0.1); }

/* DO THIS INSTEAD: */
.card {
  background: #161618;
  border: none;
  border-radius: 28px;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.60);
}
```

---

## 3. Typography Hierarchy (SF Pro Native Scale)

Follow the standard San Francisco type scale:

| Semantic Role | Font Size | Weight | Tracking / Letter Spacing | Color |
|---|---|---|---|---|
| **Large Title** | `34px` | `700` (Bold) | `-0.03em` | `#FFFFFF` |
| **Card / Screen Title** | `24px` | `700` (Bold) | `-0.02em` | `#FFFFFF` |
| **Section Header** | `20px` | `600` (SemiBold) | `-0.015em` | `#FFFFFF` |
| **Primary CTA Button** | `17px` | `600` (SemiBold) | `-0.01em` | `#FFFFFF` |
| **Body / Subheadline** | `15px` | `400` / `500` | normal | `rgba(235,235,245,0.65)` |
| **Footnote / Eyebrow** | `13px` | `600` (SemiBold) | `0.04em` (Uppercase) | `rgba(235,235,245,0.60)` |
| **XP Capsule / Pill** | `13px` | `600` (SemiBold) | `-0.01em` | `#FFD60A` |

---

## 4. Navigation Bar Standards (iOS Large Title)

* **Eyebrow**: Clean uppercase string above title (`АЛИШЕР & ДАНА · УРОВЕНЬ 1`), `font-size: 13px; font-weight: 600; letter-spacing: 0.04em; color: rgba(235,235,245,0.6)`.
* **Title Row**:
  * Left: `Сегодня` (34px Bold).
  * Right: Single pill capsule `0 / 60 XP` (`background: #1C1C1E; border: none; border-radius: 999px; padding: 6px 14px; font-size: 13px; font-weight: 600; color: #FFD60A;`).
  * **No clutter**: No wrench icons, settings buttons, or debug controls visible to the end user in production headers.

---

## 5. The Magic Deck (Hero Card Component)

The hero centerpiece embodies tactile Apple delight:
* Floating card canvas (`background: #161618; border-radius: 28px; box-shadow: 0 20px 40px rgba(0,0,0,0.6); padding: 32px 24px; text-align: center;`).
* Gentle ambient levitation animation (`@keyframes floatDeck`).
* Shimmering metallic scratch card cover with golden spark accent (`✨`).
* Title: **«Что приготовил вечер?»** (24px Bold).
* Subtitle: *«Нажмите кнопку, чтобы подобрать секретное приключение под ваше настроение»* (15px Secondary).
* CTA: Tall 56px Apple Gradient Button (`linear-gradient(135deg, #FF375F 0%, #D92B6B 100%)`) with `box-shadow: 0 10px 24px rgba(255, 55, 95, 0.35)`.
* Secondary action: Subtle standalone text link underneath (`Как устроена игра? Попробовать демо-карточку →`).

---

## 6. Bottom Sheet (Native Modal Presentation)

* Appears smoothly with spring transition `cubic-bezier(0.22, 1, 0.36, 1)`.
* Background: `#1C1C1E` with rounded top corners (`border-radius: 24px 24px 0 0;`).
* Grab handle: `width: 36px; height: 5px; border-radius: 2.5px; background: rgba(255, 255, 255, 0.3); margin: 8px auto 16px;`.
* Contains rich selectors (Mood chips, duration, budget, venue) and a single definitive bottom button: **«Удиви нас»** (56px).

---

## 7. Tactile Physics & Animation Standards

* **Spring Transitions**: `cubic-bezier(0.22, 1, 0.36, 1)` for sheet slide-ins and modal reveals.
* **Haptic Emulation**: Trigger micro-haptics on taps (`navigator.vibrate?.(10)`).
* **Press Scale**: Buttons smoothly depress on touch: `transform: scale(0.97); transition: transform 0.15s ease;`.
