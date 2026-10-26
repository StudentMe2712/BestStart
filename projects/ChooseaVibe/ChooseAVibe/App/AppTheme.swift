import SwiftUI

#if canImport(UIKit)
import UIKit
#endif

// MARK: - Палитра вайба (VibePalette)

/// Цветовые стили, градиенты и оформление для каждого настроения вечера
public struct VibePalette: Sendable {
    /// Основной акцентный цвет
    public let primary: Color
    /// Дополнительный мягкий цвет
    public let secondary: Color
    /// Фоновый градиент для карточки или баннера
    public let backgroundGradient: LinearGradient
    /// Градиент для кнопок и бейджей
    public let accentGradient: LinearGradient
    /// Имя системной иконки SF Symbols
    public let iconName: String
    /// Название настроения на русском языке
    public let title: String
    /// Цвет мягкого свечения
    public let glowColor: Color

    public init(
        primary: Color,
        secondary: Color,
        backgroundGradient: LinearGradient,
        accentGradient: LinearGradient,
        iconName: String,
        title: String,
        glowColor: Color
    ) {
        self.primary = primary
        self.secondary = secondary
        self.backgroundGradient = backgroundGradient
        self.accentGradient = accentGradient
        self.iconName = iconName
        self.title = title
        self.glowColor = glowColor
    }
}

// MARK: - Стили рубашек скретч-карточек (ScratchShirtTheme)

/// Тема оформления стираемого защитного слоя карточки
public struct ScratchShirtTheme: Identifiable, Sendable {
    /// Уникальный строковый идентификатор ("classic_silver", "cozy_kraft", etc.)
    public let id: String
    /// Название рубашки на русском языке
    public let title: String
    /// Атмосферное описание рубашки
    public let descriptionText: String
    /// Уровень пары, на котором открывается рубашка (1...5)
    public let unlockedAtLevel: Int
    /// Градиент стираемого слоя
    public let gradient: LinearGradient
    /// Цвет тонкой рамки карточки
    public let borderColor: Color
    /// Цвет искр и декоративных частиц
    public let particleColor: Color
    /// Имя системной иконки темы
    public let iconName: String

    public init(
        id: String,
        title: String,
        descriptionText: String,
        unlockedAtLevel: Int,
        gradient: LinearGradient,
        borderColor: Color,
        particleColor: Color,
        iconName: String
    ) {
        self.id = id
        self.title = title
        self.descriptionText = descriptionText
        self.unlockedAtLevel = unlockedAtLevel
        self.gradient = gradient
        self.borderColor = borderColor
        self.particleColor = particleColor
        self.iconName = iconName
    }
}

// MARK: - Тактильная отдача (HapticFeedback)

/// Утилита тактильной виброотдачи для интерактивных элементов и скретч-карточки
public enum HapticFeedback {
    /// Лёгкий щелчок при нажатии на чипы и переключатели
    public static func light() {
        #if canImport(UIKit)
        let generator = UIImpactFeedbackGenerator(style: .light)
        generator.prepare()
        generator.impactOccurred()
        #endif
    }

    /// Мягкий тактильный импульс при каждом движении стирания скретч-слоя
    public static func soft() {
        #if canImport(UIKit)
        let generator = UIImpactFeedbackGenerator(style: .soft)
        generator.prepare()
        generator.impactOccurred()
        #endif
    }

    /// Выразительный импульс при преодолении 45% порога авто-раскрытия карточки
    public static func medium() {
        #if canImport(UIKit)
        let generator = UIImpactFeedbackGenerator(style: .medium)
        generator.prepare()
        generator.impactOccurred()
        #endif
    }

    /// Торжественный виброотклик при завершении этапа, получении значка или Level Up
    public static func success() {
        #if canImport(UIKit)
        let generator = UINotificationFeedbackGenerator()
        generator.prepare()
        generator.notificationOccurred(.success)
        #endif
    }

    /// Предупреждающий отклик при сбросе настроек или отмене действия
    public static func warning() {
        #if canImport(UIKit)
        let generator = UINotificationFeedbackGenerator()
        generator.prepare()
        generator.notificationOccurred(.warning)
        #endif
    }
}

// MARK: - Тема приложения (AppTheme)

/// Единая дизайн-система приложения Choose a Vibe
public enum AppTheme {

    // MARK: 1. Системные цвета и фоны

    public static let midnightBg = Color(red: 0.0, green: 0.0, blue: 0.0) // #000000 iOS 18 Dark Mode
    public static let primaryRose = Color(red: 1.00, green: 0.22, blue: 0.37) // #FF375F iOS Pink
    public static let warmGold = Color(red: 1.00, green: 0.84, blue: 0.04) // #FFD60A iOS Amber
    public static let softText = Color(red: 0.92, green: 0.92, blue: 0.96, opacity: 0.60) // rgba(235, 235, 245, 0.60)
    public static let cardSurface = Color(red: 22/255.0, green: 22/255.0, blue: 24/255.0) // #161618 (Level 1)
    public static let cardSurfaceLight = Color(red: 33/255.0, green: 33/255.0, blue: 36/255.0) // #212124 (Level 2)
    public static let heroGradient = Gradients.brandPrimary

    // MARK: 2. Палитры 5 вайбов

    /// Романтика — нежность, тёплый розово-малиновый румянец
    public static let romantic = VibePalette(
        primary: Color(red: 1.00, green: 0.23, blue: 0.44),       // #FF3B70
        secondary: Color(red: 1.00, green: 0.65, blue: 0.75),     // #FFA6BF
        backgroundGradient: LinearGradient(
            colors: [Color(red: 0.98, green: 0.92, blue: 0.94), Color(red: 0.95, green: 0.85, blue: 0.89)],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        ),
        accentGradient: LinearGradient(
            colors: [Color(red: 1.00, green: 0.35, blue: 0.55), Color(red: 0.95, green: 0.15, blue: 0.35)],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        ),
        iconName: "heart.fill",
        title: "Романтика",
        glowColor: Color(red: 1.00, green: 0.23, blue: 0.44, opacity: 0.35)
    )

    /// Игривое — солнечный задор, оранжево-янтарный драйв
    public static let playful = VibePalette(
        primary: Color(red: 1.00, green: 0.58, blue: 0.00),       // #FF9500
        secondary: Color(red: 1.00, green: 0.84, blue: 0.04),     // #FFD60A
        backgroundGradient: LinearGradient(
            colors: [Color(red: 0.99, green: 0.95, blue: 0.88), Color(red: 0.98, green: 0.90, blue: 0.80)],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        ),
        accentGradient: LinearGradient(
            colors: [Color(red: 1.00, green: 0.65, blue: 0.15), Color(red: 1.00, green: 0.40, blue: 0.00)],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        ),
        iconName: "sparkles",
        title: "Игривое",
        glowColor: Color(red: 1.00, green: 0.58, blue: 0.00, opacity: 0.35)
    )

    /// Спонтанное — неоновый пурпур, вспышка индиго и авантюры
    public static let spontaneous = VibePalette(
        primary: Color(red: 0.51, green: 0.22, blue: 0.93),       // #8338EC
        secondary: Color(red: 0.23, green: 0.53, blue: 1.00),     // #3A86FF
        backgroundGradient: LinearGradient(
            colors: [Color(red: 0.93, green: 0.90, blue: 0.98), Color(red: 0.88, green: 0.85, blue: 0.96)],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        ),
        accentGradient: LinearGradient(
            colors: [Color(red: 0.55, green: 0.25, blue: 0.95), Color(red: 0.20, green: 0.50, blue: 1.00)],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        ),
        iconName: "bolt.fill",
        title: "Спонтанное",
        glowColor: Color(red: 0.51, green: 0.22, blue: 0.93, opacity: 0.35)
    )

    /// Уютное — терракота, тёплый плед, корица и домашний покой
    public static let cozy = VibePalette(
        primary: Color(red: 0.88, green: 0.48, blue: 0.37),       // #E07A5F
        secondary: Color(red: 0.96, green: 0.64, blue: 0.38),     // #F4A261
        backgroundGradient: LinearGradient(
            colors: [Color(red: 0.98, green: 0.94, blue: 0.90), Color(red: 0.95, green: 0.88, blue: 0.82)],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        ),
        accentGradient: LinearGradient(
            colors: [Color(red: 0.92, green: 0.55, blue: 0.35), Color(red: 0.80, green: 0.40, blue: 0.25)],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        ),
        iconName: "cup.and.saucer.fill",
        title: "Уютное",
        glowColor: Color(red: 0.88, green: 0.48, blue: 0.37, opacity: 0.35)
    )

    /// Глубокое — сапфировая ночь, искренность и звёздное небо
    public static let deep = VibePalette(
        primary: Color(red: 0.17, green: 0.30, blue: 0.49),       // #2B4C7E
        secondary: Color(red: 0.29, green: 0.56, blue: 0.89),     // #4A90E2
        backgroundGradient: LinearGradient(
            colors: [Color(red: 0.90, green: 0.93, blue: 0.98), Color(red: 0.84, green: 0.88, blue: 0.95)],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        ),
        accentGradient: LinearGradient(
            colors: [Color(red: 0.25, green: 0.42, blue: 0.68), Color(red: 0.10, green: 0.20, blue: 0.38)],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        ),
        iconName: "moon.stars.fill",
        title: "Глубокое",
        glowColor: Color(red: 0.29, green: 0.56, blue: 0.89, opacity: 0.35)
    )

    /// Получение палитры по тегу вайба
    public static func palette(for vibe: VibeTag) -> VibePalette {
        switch vibe {
        case .romantic: return romantic
        case .playful: return playful
        case .spontaneous: return spontaneous
        case .cozy: return cozy
        case .deep: return deep
        }
    }

    // MARK: 3. Рубашки скретч-карточек (5 коллекционных тем)

    /// Рубашка 1: Классическое серебро (Уровень 1)
    public static let classicSilverShirt = ScratchShirtTheme(
        id: "classic_silver",
        title: "Классическое серебро",
        descriptionText: "Элегантное серебристое напыление с мягким металлическим сиянием",
        unlockedAtLevel: 1,
        gradient: LinearGradient(
            colors: [
                Color(white: 0.92),
                Color(white: 0.72),
                Color(white: 0.88),
                Color(white: 0.68)
            ],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        ),
        borderColor: Color(white: 0.78),
        particleColor: Color(white: 0.95),
        iconName: "sparkle"
    )

    /// Рубашка 2: Уютный крафт (Уровень 2)
    public static let cozyKraftShirt = ScratchShirtTheme(
        id: "cozy_kraft",
        title: "Уютный крафт",
        descriptionText: "Тёплая крафтовая текстура с естественными древесными оттенками",
        unlockedAtLevel: 2,
        gradient: LinearGradient(
            colors: [
                Color(red: 0.84, green: 0.74, blue: 0.62),
                Color(red: 0.72, green: 0.60, blue: 0.48),
                Color(red: 0.64, green: 0.52, blue: 0.40)
            ],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        ),
        borderColor: Color(red: 0.60, green: 0.48, blue: 0.36),
        particleColor: Color(red: 0.92, green: 0.85, blue: 0.76),
        iconName: "leaf.fill"
    )

    /// Рубашка 3: Розовое золото (Уровень 3)
    public static let roseGoldShirt = ScratchShirtTheme(
        id: "rose_gold",
        title: "Розовое золото",
        descriptionText: "Изысканный металлик с тёплым золотисто-розовым переливом",
        unlockedAtLevel: 3,
        gradient: LinearGradient(
            colors: [
                Color(red: 0.97, green: 0.78, blue: 0.75),
                Color(red: 0.88, green: 0.62, blue: 0.60),
                Color(red: 0.94, green: 0.72, blue: 0.70)
            ],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        ),
        borderColor: Color(red: 0.82, green: 0.52, blue: 0.50),
        particleColor: Color(red: 1.00, green: 0.88, blue: 0.86),
        iconName: "suit.heart.fill"
    )

    /// Рубашка 4: Звёздная полночь (Уровень 4)
    public static let midnightStarsShirt = ScratchShirtTheme(
        id: "midnight_stars",
        title: "Звёздная полночь",
        descriptionText: "Глубокий индиго с мерцающими звёздными крапинками",
        unlockedAtLevel: 4,
        gradient: LinearGradient(
            colors: [
                Color(red: 0.16, green: 0.14, blue: 0.36),
                Color(red: 0.08, green: 0.06, blue: 0.22),
                Color(red: 0.04, green: 0.03, blue: 0.14)
            ],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        ),
        borderColor: Color(red: 0.34, green: 0.32, blue: 0.62),
        particleColor: Color(red: 0.92, green: 0.90, blue: 1.00),
        iconName: "star.fill"
    )

    /// Рубашка 5: Сияющий голографик (Уровень 5)
    public static let shiningHolographicShirt = ScratchShirtTheme(
        id: "shining_holographic",
        title: "Сияющий голографик",
        descriptionText: "Радужный переливающийся спектр для легендарных моментов",
        unlockedAtLevel: 5,
        gradient: LinearGradient(
            colors: [
                Color(red: 0.98, green: 0.74, blue: 0.88),
                Color(red: 0.62, green: 0.86, blue: 0.98),
                Color(red: 0.84, green: 0.74, blue: 0.98),
                Color(red: 0.98, green: 0.88, blue: 0.70)
            ],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        ),
        borderColor: Color(red: 0.78, green: 0.65, blue: 0.95),
        particleColor: Color.white,
        iconName: "wand.and.stars"
    )

    /// Список всех доступных рубашек
    public static let allScratchShirts: [ScratchShirtTheme] = [
        classicSilverShirt,
        cozyKraftShirt,
        roseGoldShirt,
        midnightStarsShirt,
        shiningHolographicShirt
    ]

    /// Поиск темы рубашки по идентификатору (с безопасным фолбэком на серебро)
    public static func scratchShirt(byId id: String) -> ScratchShirtTheme {
        if id == "cozy_craft" {
            return cozyKraftShirt
        }
        return allScratchShirts.first { $0.id == id } ?? classicSilverShirt
    }

    // MARK: 4. Скругления (Corner Radius)

    public enum CornerRadius {
        public static let small: CGFloat = 8
        public static let chip: CGFloat = 14
        public static let medium: CGFloat = 16
        public static let large: CGFloat = 24
        public static let card: CGFloat = 28
    }

    // MARK: 5. Тени (Shadows)

    public enum Shadows {
        public static let cardShadowColor = Color.black.opacity(0.14)
        public static let cardShadowRadius: CGFloat = 18
        public static let cardShadowX: CGFloat = 0
        public static let cardShadowY: CGFloat = 8

        public static let subtleShadowColor = Color.black.opacity(0.06)
        public static let subtleShadowRadius: CGFloat = 8
        public static let subtleShadowX: CGFloat = 0
        public static let subtleShadowY: CGFloat = 3

        public static let goldGlowColor = Color(red: 1.00, green: 0.75, blue: 0.15, opacity: 0.40)
        public static let goldGlowRadius: CGFloat = 20
    }

    // MARK: 6. Акцентные градиенты

    public enum Gradients {
        public static let brandPrimary = LinearGradient(
            colors: [Color(red: 1.00, green: 0.23, blue: 0.44), Color(red: 0.51, green: 0.22, blue: 0.93)],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )

        public static let goldReward = LinearGradient(
            colors: [Color(red: 1.00, green: 0.82, blue: 0.28), Color(red: 0.92, green: 0.62, blue: 0.12)],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )

        public static let darkSurface = LinearGradient(
            colors: [Color(red: 0.12, green: 0.13, blue: 0.18), Color(red: 0.08, green: 0.09, blue: 0.12)],
            startPoint: .top,
            endPoint: .bottom
        )

        public static let backgroundSoft = LinearGradient(
            colors: [Color(red: 0.98, green: 0.97, blue: 0.99), Color(red: 0.94, green: 0.93, blue: 0.96)],
            startPoint: .top,
            endPoint: .bottom
        )
    }

    // MARK: 7. Быстрые вызовы виброотклика (Haptics)

    public static func hapticLight() { HapticFeedback.light() }
    public static func hapticSoft() { HapticFeedback.soft() }
    public static func hapticMedium() { HapticFeedback.medium() }
    public static func hapticSuccess() { HapticFeedback.success() }
    public static func hapticWarning() { HapticFeedback.warning() }

    // MARK: 8. Удобные градиенты
    public static var goldGradient: LinearGradient {
        Gradients.goldReward
    }

    public static func gradient(for vibe: VibeTag) -> LinearGradient {
        palette(for: vibe).accentGradient
    }

    public static func scratchThemeGradient(for themeId: String) -> LinearGradient {
        scratchShirt(byId: themeId).gradient
    }
}

// MARK: - Модификаторы представлений SwiftUI

/// Стиль основной карточки задания
public struct VibeCardModifier: ViewModifier {
    var backgroundColor: Color = Color.white
    var cornerRadius: CGFloat = AppTheme.CornerRadius.card

    public func body(content: Content) -> some View {
        content
            .background(backgroundColor)
            .clipShape(RoundedRectangle(cornerRadius: cornerRadius, style: .continuous))
            .shadow(
                color: AppTheme.Shadows.cardShadowColor,
                radius: AppTheme.Shadows.cardShadowRadius,
                x: AppTheme.Shadows.cardShadowX,
                y: AppTheme.Shadows.cardShadowY
            )
    }
}

/// Стиль мягкого неонового свечения
public struct VibeGlowModifier: ViewModifier {
    var glowColor: Color
    var radius: CGFloat = 16

    public func body(content: Content) -> some View {
        content
            .shadow(color: glowColor, radius: radius, x: 0, y: 0)
    }
}

// MARK: - Расширения View для лаконичного синтаксиса

public extension View {
    /// Оформление карточки с закруглёнными углами и мягкой тенью
    func vibeCardStyle(
        backgroundColor: Color = Color.white,
        cornerRadius: CGFloat = AppTheme.CornerRadius.card
    ) -> some View {
        modifier(VibeCardModifier(backgroundColor: backgroundColor, cornerRadius: cornerRadius))
    }

    /// Добавление атмосферного свечения вайба
    func vibeGlow(color: Color, radius: CGFloat = 16) -> some View {
        modifier(VibeGlowModifier(glowColor: color, radius: radius))
    }
}

// MARK: - Стили интерактивных кнопок

/// Главная акцентная кнопка действия
public struct VibePrimaryButtonStyle: ButtonStyle {
    var gradient: LinearGradient = AppTheme.Gradients.brandPrimary
    var isEnabled: Bool = true

    public init(gradient: LinearGradient = AppTheme.Gradients.brandPrimary, isEnabled: Bool = true) {
        self.gradient = gradient
        self.isEnabled = isEnabled
    }

    public func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 17, weight: .bold, design: .rounded))
            .foregroundColor(.white)
            .frame(maxWidth: .infinity)
            .frame(height: 54)
            .background(
                gradient
                    .opacity(isEnabled ? (configuration.isPressed ? 0.85 : 1.0) : 0.4)
            )
            .clipShape(RoundedRectangle(cornerRadius: AppTheme.CornerRadius.medium, style: .continuous))
            .scaleEffect(configuration.isPressed ? 0.98 : 1.0)
            .animation(.easeInOut(duration: 0.15), value: configuration.isPressed)
            .shadow(
                color: Color.black.opacity(configuration.isPressed ? 0.05 : 0.15),
                radius: 10,
                x: 0,
                y: 5
            )
    }
}

/// Вторичная мягкая кнопка
public struct VibeSecondaryButtonStyle: ButtonStyle {
    public init() {}

    public func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 16, weight: .medium, design: .rounded))
            .foregroundColor(Color.primary)
            .frame(maxWidth: .infinity)
            .frame(height: 54)
            .background(Color.secondary.opacity(configuration.isPressed ? 0.20 : 0.12))
            .clipShape(RoundedRectangle(cornerRadius: AppTheme.CornerRadius.medium, style: .continuous))
            .scaleEffect(configuration.isPressed ? 0.98 : 1.0)
            .animation(.easeInOut(duration: 0.15), value: configuration.isPressed)
    }
}
