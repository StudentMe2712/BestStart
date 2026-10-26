//
//  CatalogModels.swift
//  ChooseAVibe
//
//  Модели статического каталога приключений, глав и карточек заданий.
//  Декодируются из файла adventures_catalog_ru.json (Bundle).
//

import Foundation

// MARK: - Теги настроения (VibeTag)

/// Настроение или вайб вечера.
public enum VibeTag: String, Codable, CaseIterable, Identifiable, Hashable, Sendable {
    case romantic = "Романтика"
    case playful = "Игривое"
    case spontaneous = "Спонтанное"
    case cozy = "Уютное"
    case deep = "Глубокое"

    public var id: String { rawValue }

    /// Отображаемое название вайба
    public var displayName: String { rawValue }

    /// Название системной SF-иконки
    public var iconName: String {
        switch self {
        case .romantic:
            return "heart.fill"
        case .playful:
            return "sparkles"
        case .spontaneous:
            return "bolt.fill"
        case .cozy:
            return "cup.and.saucer.fill"
        case .deep:
            return "moon.stars.fill"
        }
    }

    /// Системная иконка (алиас)
    public var systemIcon: String { iconName }

    /// Краткое описание вайба для пользователя
    public var description: String {
        switch self {
        case .romantic:
            return "Нежность, сближение и тёплая атмосфера"
        case .playful:
            return "Юмор, лёгкие дуэли и весёлые челленджи"
        case .spontaneous:
            return "Выход из рутины и маленькие авантюры"
        case .cozy:
            return "Спокойный темп, плед, вкусная еда и домашнее тепло"
        case .deep:
            return "Искренние разговоры и новые грани друг друга"
        }
    }

    /// Имя палитры/акцентного цвета
    public var accentColorName: String {
        switch self {
        case .romantic:
            return "vibeRomantic"
        case .playful:
            return "vibePlayful"
        case .spontaneous:
            return "vibeSpontaneous"
        case .cozy:
            return "vibeCozy"
        case .deep:
            return "vibeDeep"
        }
    }
}

// MARK: - Длительность (DurationTier)

/// Доступное время на приключение.
public enum DurationTier: String, Codable, CaseIterable, Identifiable, Hashable, Sendable {
    case short = "15–30 мин"
    case medium = "30–60 мин"
    case long = "60–120 мин"

    public var id: String { rawValue }

    /// Отображаемое название
    public var displayName: String { rawValue }

    /// Примерная длительность в минутах
    public var approximateMinutes: Int {
        switch self {
        case .short: return 25
        case .medium: return 45
        case .long: return 90
        }
    }

    /// Системная иконка
    public var iconName: String {
        switch self {
        case .short: return "timer"
        case .medium: return "clock"
        case .long: return "hourglass"
        }
    }

    /// Системная иконка (алиас)
    public var systemIcon: String { iconName }
}

// MARK: - Бюджет (BudgetTier)

/// Бюджет на вечер в казахстанских тенге (₸).
public enum BudgetTier: Int, Codable, CaseIterable, Comparable, Identifiable, Hashable, Sendable {
    case free = 0          // 0 ₸ (Бесплатно)
    case low = 2000        // До 2 000 ₸
    case medium = 5000     // До 5 000 ₸
    case unlimited = 15000 // Без лимита

    public var id: Int { rawValue }

    public static func < (lhs: BudgetTier, rhs: BudgetTier) -> Bool {
        lhs.rawValue < rhs.rawValue
    }

    /// Отображаемое название с пояснением
    public var displayName: String {
        switch self {
        case .free:
            return "0 ₸ (Бесплатно)"
        case .low:
            return "До 2 000 ₸"
        case .medium:
            return "До 5 000 ₸"
        case .unlimited:
            return "Без лимита"
        }
    }

    /// Отображаемый текст (алиас)
    public var displayText: String { displayName }

    /// Короткая строка с валютой
    public var shortTitle: String {
        switch self {
        case .free:
            return "0 ₸"
        case .low:
            return "2 000 ₸"
        case .medium:
            return "5 000 ₸"
        case .unlimited:
            return "15 000+ ₸"
        }
    }

    /// Форматированная строка
    public var formattedWithCurrency: String {
        if self == .free {
            return "Бесплатно (0 ₸)"
        }
        return "до \(rawValue.formatted()) ₸"
    }

    /// Системная иконка
    public var iconName: String {
        switch self {
        case .free:
            return "gift.fill"
        case .low:
            return "banknote"
        case .medium:
            return "creditcard"
        case .unlimited:
            return "sparkles"
        }
    }

    /// Системная иконка (алиас)
    public var systemIcon: String { iconName }
}

// MARK: - Локация (LocationType)

/// Место проведения приключения.
public enum LocationType: String, Codable, CaseIterable, Identifiable, Hashable, Sendable {
    case home = "Дома"
    case outside = "Вне дома"
    case any = "Неважно"

    public var id: String { rawValue }

    /// Отображаемое название
    public var displayName: String { rawValue }

    /// Системная иконка
    public var iconName: String {
        switch self {
        case .home:
            return "house.fill"
        case .outside:
            return "figure.walk"
        case .any:
            return "globe.europe.africa.fill"
        }
    }

    /// Системная иконка (алиас)
    public var systemIcon: String { iconName }
}

// MARK: - Личные границы / Стоп-теги (BoundaryTag)

/// Теги ограничений и комфорта для пары.
public enum BoundaryTag: String, Codable, CaseIterable, Identifiable, Hashable, Sendable {
    case dancingOrSinging = "Без танцев и пения"
    case strangersInteraction = "Без общения с прохожими"
    case drinksOrFood = "Без еды и напитков"
    case activeMovement = "Без активной нагрузки"

    public var id: String { rawValue }

    /// Отображаемое название
    public var displayName: String { rawValue }

    /// Системная иконка
    public var iconName: String {
        switch self {
        case .dancingOrSinging:
            return "music.note"
        case .strangersInteraction:
            return "person.2.slash"
        case .drinksOrFood:
            return "fork.knife"
        case .activeMovement:
            return "figure.run"
        }
    }

    /// Системная иконка (алиас)
    public var systemIcon: String { iconName }

    /// Пояснение фильтра
    public var description: String {
        switch self {
        case .dancingOrSinging:
            return "Исключить задания с пением, танцами и перформансами"
        case .strangersInteraction:
            return "Исключить контакты с незнакомыми людьми на улице"
        case .drinksOrFood:
            return "Исключить дегустации, готовку и заказы еды"
        case .activeMovement:
            return "Исключить пробежки, долгие подъёмы и физическую нагрузку"
        }
    }
}

// MARK: - Вариант карточки задания (ChallengeCardVariant)

/// Конкретный вариант задания на этапе (основной или фолбэк).
public struct ChallengeCardVariant: Codable, Hashable, Sendable {
    public let title: String
    public let instruction: String
    public let hint: String?
    public let estimatedMinutes: Int

    public init(
        title: String,
        instruction: String,
        hint: String? = nil,
        estimatedMinutes: Int = 10
    ) {
        self.title = title
        self.instruction = instruction
        self.hint = hint
        self.estimatedMinutes = estimatedMinutes
    }
}

// MARK: - Определение карточки этапа (ChallengeCardDefinition)

/// Карточка этапа приключения (содержит primary и сюжетный fallback).
public struct ChallengeCardDefinition: Codable, Identifiable, Hashable, Sendable {
    public let id: String
    public let stepIndex: Int // 1...4
    public let primary: ChallengeCardVariant
    public let fallback: ChallengeCardVariant
    public let boundaryTags: [BoundaryTag]

    public init(
        id: String,
        stepIndex: Int,
        primary: ChallengeCardVariant,
        fallback: ChallengeCardVariant,
        boundaryTags: [BoundaryTag] = []
    ) {
        self.id = id
        self.stepIndex = stepIndex
        self.primary = primary
        self.fallback = fallback
        self.boundaryTags = boundaryTags
    }

    /// Возвращает активный вариант карточки в зависимости от флага замены
    public func variant(useFallback: Bool) -> ChallengeCardVariant {
        useFallback ? fallback : primary
    }
}

// MARK: - Определение приключения (AdventureDefinition)

/// Полное описание приключения из каталога.
public struct AdventureDefinition: Codable, Identifiable, Hashable, Sendable {
    public let id: String
    public let chapterId: String
    public let orderInChapter: Int
    public let title: String
    public let subtitle: String
    public let preparationNote: String
    public let vibes: [VibeTag]
    public let duration: DurationTier
    public let budget: BudgetTier
    public let location: LocationType
    public let cards: [ChallengeCardDefinition]

    public init(
        id: String,
        chapterId: String,
        orderInChapter: Int,
        title: String,
        subtitle: String,
        preparationNote: String,
        vibes: [VibeTag],
        duration: DurationTier,
        budget: BudgetTier,
        location: LocationType,
        cards: [ChallengeCardDefinition]
    ) {
        self.id = id
        self.chapterId = chapterId
        self.orderInChapter = orderInChapter
        self.title = title
        self.subtitle = subtitle
        self.preparationNote = preparationNote
        self.vibes = vibes
        self.duration = duration
        self.budget = budget
        self.location = location
        self.cards = cards
    }

    /// Количество этапов в приключении (обычно 4)
    public var cardCount: Int {
        cards.count
    }

    /// Основной (ведущий) вайб приключения
    public var primaryVibe: VibeTag? {
        vibes.first
    }

    /// Дополнительный вайб приключения (при наличии)
    public var secondaryVibe: VibeTag? {
        vibes.count > 1 ? vibes[1] : nil
    }

    /// Суммарное примерное время в минутах
    public var totalEstimatedMinutes: Int {
        cards.reduce(0) { $0 + $1.primary.estimatedMinutes }
    }

    /// Поиск карточки по номеру шага (1..4)
    public func card(for stepIndex: Int) -> ChallengeCardDefinition? {
        cards.first { $0.stepIndex == stepIndex }
    }

    /// Строка со списком вайбов через плюс ("Романтика + Уютное")
    public var formattedVibesList: String {
        vibes.map(\.displayName).joined(separator: " + ")
    }

    /// Форматированная плашка параметров
    public var formattedSummarySubtitle: String {
        "\(formattedVibesList) · \(duration.displayName) · \(location.displayName) · \(budget.shortTitle)"
    }
}

// MARK: - Определение главы (ChapterDefinition)

/// Тематическая глава на карте приключений.
public struct ChapterDefinition: Codable, Identifiable, Hashable, Sendable {
    public let id: String
    public let order: Int
    public let title: String
    public let subtitle: String
    public let requiredCompletedCount: Int
    public let adventureIds: [String]

    // Совместимость со сторонними модулями
    public var chapterNumber: Int { order }
    public var requiredCompletedPreviousQuests: Int { requiredCompletedCount }

    public init(
        id: String,
        order: Int,
        title: String,
        subtitle: String,
        requiredCompletedCount: Int,
        adventureIds: [String]
    ) {
        self.id = id
        self.order = order
        self.title = title
        self.subtitle = subtitle
        self.requiredCompletedCount = requiredCompletedCount
        self.adventureIds = adventureIds
    }

    public init(
        id: String,
        chapterNumber: Int,
        title: String,
        subtitle: String,
        requiredCompletedPreviousQuests: Int,
        adventureIds: [String]
    ) {
        self.id = id
        self.order = chapterNumber
        self.title = title
        self.subtitle = subtitle
        self.requiredCompletedCount = requiredCompletedPreviousQuests
        self.adventureIds = adventureIds
    }

    /// Заголовок с номером ("Глава 1 · Первые искры")
    public var formattedTitle: String {
        "Глава \(order) · «\(title)»"
    }
}

// MARK: - Определение достижения (AchievementDefinition)

/// Значок-достижение пары.
public struct AchievementDefinition: Codable, Identifiable, Hashable, Sendable {
    public let id: String
    public let title: String
    public let description: String
    public let icon: String
    public let bonusXP: Int

    // Совместимость с ProgressionEngine
    public var descriptionText: String { description }
    public var iconName: String { icon }
    public var xpReward: Int { bonusXP }
    public var idValue: String { id }

    public init(
        id: String,
        title: String,
        description: String,
        icon: String,
        bonusXP: Int = 20
    ) {
        self.id = id
        self.title = title
        self.description = description
        self.icon = icon
        self.bonusXP = bonusXP
    }

    public init(
        id: String,
        title: String,
        descriptionText: String,
        iconName: String,
        xpReward: Int = 20
    ) {
        self.id = id
        self.title = title
        self.description = descriptionText
        self.icon = iconName
        self.bonusXP = xpReward
    }
}

// MARK: - Корневой контейнер каталога (CatalogContainer)

/// Корневой объект JSON-каталога приключений.
public struct CatalogContainer: Codable, Hashable, Sendable {
    public let chapters: [ChapterDefinition]
    public let adventures: [AdventureDefinition]
    public let achievements: [AchievementDefinition]

    public init(
        chapters: [ChapterDefinition] = [],
        adventures: [AdventureDefinition] = [],
        achievements: [AchievementDefinition] = []
    ) {
        self.chapters = chapters
        self.adventures = adventures
        self.achievements = achievements
    }

    /// Поиск приключения по ID
    public func adventure(by id: String) -> AdventureDefinition? {
        adventures.first { $0.id == id }
    }

    /// Поиск главы по ID
    public func chapter(by id: String) -> ChapterDefinition? {
        chapters.first { $0.id == id }
    }

    /// Поиск достижения по ID
    public func achievement(by id: String) -> AchievementDefinition? {
        achievements.first { $0.id == id }
    }

    /// Приключения для заданной главы
    public func adventures(for chapterId: String) -> [AdventureDefinition] {
        adventures
            .filter { $0.chapterId == chapterId }
            .sorted { $0.orderInChapter < $1.orderInChapter }
    }
}
