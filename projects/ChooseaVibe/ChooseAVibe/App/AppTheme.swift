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

    public static let midnightBg = Color(red: 10/255.0, green: 8/255.0, blue: 12/255.0) // #0A080C Cinematic Editorial
    public static let primaryRose = Color(red: 212/255.0, green: 68/255.0, blue: 106/255.0) // #D4446A Deep Wine Rose
    public static let warmGold = Color(red: 226/255.0, green: 194/255.0, blue: 179/255.0) // #E2C2B3 Warm Gold Accents
    public static let softText = Color(red: 0.92, green: 0.92, blue: 0.96, opacity: 0.60) // rgba(235, 235, 245, 0.60)
    public static let cardSurface = Color(red: 26/255.0, green: 21/255.0, blue: 28/255.0, opacity: 0.78) // Editorial Glass Surface
    public static let cardSurfaceLight = Color(red: 38/255.0, green: 28/255.0, blue: 40/255.0, opacity: 0.88)
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
            colors: [
                Color(red: 184/255.0, green: 59/255.0, blue: 94/255.0),
                Color(red: 224/255.0, green: 90/255.0, blue: 122/255.0),
                Color(red: 158/255.0, green: 42/255.0, blue: 75/255.0)
            ],
            startPoint: .leading,
            endPoint: .trailing
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

// MARK: - Журнальная типографика Serif (Editorial Serif Design)

public extension Font {
    /// Крупный журнальный заголовок антиквой (34pt Serif Bold)
    static var editorialLargeTitle: Font {
        .system(size: 34, weight: .bold, design: .serif)
    }

    /// Заголовок антиквой первого уровня (28pt Serif Bold)
    static var editorialTitle1: Font {
        .system(size: 28, weight: .bold, design: .serif)
    }

    /// Заголовок антиквой второго уровня (22pt Serif Bold)
    static var editorialTitle2: Font {
        .system(size: 22, weight: .bold, design: .serif)
    }

    /// Заголовок антиквой третьего уровня (20pt Serif Semibold)
    static var editorialTitle3: Font {
        .system(size: 20, weight: .semibold, design: .serif)
    }

    /// Акцентный подзаголовок или цитата антиквой (17pt Serif Medium)
    static var editorialHeadline: Font {
        .system(size: 17, weight: .medium, design: .serif)
    }

    /// Настраиваемый журнальный шрифт с засечками (.serif)
    static func editorialSerif(size: CGFloat, weight: Font.Weight = .bold) -> Font {
        .system(size: size, weight: weight, design: .serif)
    }
}

// MARK: - Модификаторы представлений SwiftUI

/// Стиль основной карточки задания в кинематографичном винном стиле
public struct VibeCardModifier: ViewModifier {
    var backgroundColor: Color = AppTheme.cardSurface
    var cornerRadius: CGFloat = AppTheme.CornerRadius.card
    var borderColor: Color = AppTheme.warmGold.opacity(0.12)

    public func body(content: Content) -> some View {
        content
            .background(backgroundColor)
            .clipShape(RoundedRectangle(cornerRadius: cornerRadius, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                    .stroke(borderColor, lineWidth: 1)
            )
            .shadow(
                color: Color.black.opacity(0.40),
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
    /// Оформление карточки с закруглёнными углами, золотисто-розовым контуром и глубокой тенью
    func vibeCardStyle(
        backgroundColor: Color = AppTheme.cardSurface,
        cornerRadius: CGFloat = AppTheme.CornerRadius.card,
        borderColor: Color = AppTheme.warmGold.opacity(0.12)
    ) -> some View {
        modifier(VibeCardModifier(backgroundColor: backgroundColor, cornerRadius: cornerRadius, borderColor: borderColor))
    }

    /// Добавление атмосферного свечения вайба
    func vibeGlow(color: Color, radius: CGFloat = 16) -> some View {
        modifier(VibeGlowModifier(glowColor: color, radius: radius))
    }
}

// MARK: - Стили интерактивных кнопок

/// Главная акцентная кнопка действия в Cinematic Editorial стиле
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
                    .opacity(isEnabled ? (configuration.isPressed ? 0.88 : 1.0) : 0.4)
            )
            .clipShape(RoundedRectangle(cornerRadius: AppTheme.CornerRadius.medium, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: AppTheme.CornerRadius.medium, style: .continuous)
                    .stroke(Color.white.opacity(0.18), lineWidth: 1)
            )
            .scaleEffect(configuration.isPressed ? 0.98 : 1.0)
            .animation(.easeInOut(duration: 0.15), value: configuration.isPressed)
            .shadow(
                color: AppTheme.primaryRose.opacity(configuration.isPressed ? 0.15 : 0.35),
                radius: 14,
                x: 0,
                y: 6
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
            .background(Color.white.opacity(configuration.isPressed ? 0.14 : 0.08))
            .clipShape(RoundedRectangle(cornerRadius: AppTheme.CornerRadius.medium, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: AppTheme.CornerRadius.medium, style: .continuous)
                    .stroke(Color.white.opacity(0.10), lineWidth: 1)
            )
            .scaleEffect(configuration.isPressed ? 0.98 : 1.0)
            .animation(.easeInOut(duration: 0.15), value: configuration.isPressed)
    }
}

// MARK: - Sprint 10: Calm Luxury Architecture & Progressive Disclosure

/// Архитектурные токены и концепция Calm Luxury & Progressive Disclosure (Спринт 10).
///
/// Концепция Progressive Disclosure (прогрессивного раскрытия информации):
/// 1. **Разгрузка главного экрана («Сегодня»)**:
///    - Главный экран освобождён от перегруженных информационных блоков, сетки 4 плиток вайбов и виджета статуса.
///    - Визуальным якорем экрана выступает медитативная карточка тайны `today-secret-hero` («Что приготовил вечер?»)
///      с тактильным тиснением, золотистой искрой и единственным главным капсульным действием (`btn-start-evening`).
///    - Прогресс пары, уровень, XP-бар и метрики перенесены в естественное место хранения — экран «Коллекция» (`collection-status-card`).
///    - Старые блоки `today-status-card` и `today-vibes-grid` скрыты, сохраняя максимальную чистоту интерфейса.
///
/// 2. **Модальный подбор (Zen Luxury Bottom Sheet)**:
///    - Вся когнитивная нагрузка выбора критериев (4 вайба, бюджет, время) перенесена в шторку `bottom-sheet-vibe`.
///    - Двойной CTA: точный подбор («✦ Начать приключение ›») или быстрый сюрприз в один тап («🎲 Сделайте всё за нас»).
///    - Контекстное открытие полноэкранного секретного конверта брифинга (`modal-briefing`).
public enum CalmLuxuryDesign {
    /// Радиус скругления модальной шторки подбора (Apple HIG Bottom Sheet)
    public static let sheetCornerRadius: CGFloat = 28.0

    /// Размеры ручки-индикатора свайпа шторки
    public static let sheetHandleWidth: CGFloat = 36.0
    public static let sheetHandleHeight: CGFloat = 5.0

    /// Затемнение фона под модальной шторкой
    public static let backdropDimColor = Color.black.opacity(0.65)

    /// Высота главной капсульной кнопки CTA
    public static let pillCtaHeight: CGFloat = 54.0

    /// Высота вторичной кнопки чистого сюрприза
    public static let pureSurpriseCtaHeight: CGFloat = 48.0

    /// Радиус скругления капсульных пилюль и кнопок
    public static let pillCornerRadius: CGFloat = 999.0

    /// Тонкие ювелирные рамки карточек и чипов
    public static let subtleBorderColor = Color.white.opacity(0.12)
    public static let goldAccentBorderColor = Color(red: 255/255, green: 209/255, blue: 102/255, opacity: 0.35)
}

/// Стиль премиальной капсульной кнопки Calm Luxury (Pill CTA)
public struct CalmLuxuryPillButtonStyle: ButtonStyle {
    public var isAccent: Bool = true

    public init(isAccent: Bool = true) {
        self.isAccent = isAccent
    }

    public func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 16, weight: .semibold, design: .rounded))
            .foregroundColor(.white)
            .frame(maxWidth: .infinity)
            .frame(height: isAccent ? CalmLuxuryDesign.pillCtaHeight : CalmLuxuryDesign.pureSurpriseCtaHeight)
            .background(
                isAccent
                    ? AppTheme.Gradients.brandPrimary
                    : LinearGradient(
                        colors: [Color.white.opacity(0.12), Color.white.opacity(0.06)],
                        startPoint: .top,
                        endPoint: .bottom
                    )
            )
            .clipShape(Capsule())
            .overlay(
                Capsule()
                    .stroke(
                        isAccent ? Color.white.opacity(0.24) : CalmLuxuryDesign.subtleBorderColor,
                        lineWidth: 1
                    )
            )
            .scaleEffect(configuration.isPressed ? 0.98 : 1.0)
            .animation(.easeInOut(duration: 0.15), value: configuration.isPressed)
            .shadow(
                color: isAccent ? AppTheme.primaryRose.opacity(0.3) : Color.clear,
                radius: 12,
                x: 0,
                y: 4
            )
    }
}

public extension View {
    /// Применение стиля капсулы Calm Luxury
    func calmLuxuryPill(isAccent: Bool = true) -> some View {
        buttonStyle(CalmLuxuryPillButtonStyle(isAccent: isAccent))
    }
}
