//
//  ProgressModels.swift
//  ChooseAVibe
//
//  Персистентные модели SwiftData (CoupleProfile, AdventureSessionRecord)
//  и структуры геймификации: уровни, рубашки карт, достижения.
//

import Foundation
import SwiftData

// MARK: - Рубашки скретч-карт (ScratchCardTheme)

/// Визуальная тема защитного скретч-слоя карточки.
public struct ScratchCardTheme: Identifiable, Hashable, Sendable {
    public let id: String
    public let name: String
    public let requiredXP: Int
    public let description: String
    public let systemIcon: String
    public let colorHexCodes: [String]

    public init(
        id: String,
        name: String,
        requiredXP: Int,
        description: String,
        systemIcon: String,
        colorHexCodes: [String]
    ) {
        self.id = id
        self.name = name
        self.requiredXP = requiredXP
        self.description = description
        self.systemIcon = systemIcon
        self.colorHexCodes = colorHexCodes
    }

    // 5 тем оформления согласно спецификации
    public static let classicSilver = ScratchCardTheme(
        id: "classic_silver",
        name: "Классическое серебро",
        requiredXP: 0,
        description: "Сдержанный матовый блеск серебра с деликатной металлической текстурой",
        systemIcon: "circle.circle",
        colorHexCodes: ["#D8D8DC", "#B0B0B8", "#8E8E93"]
    )

    public static let cozyKraft = ScratchCardTheme(
        id: "cozy_kraft",
        name: "Уютный крафт",
        requiredXP: 60,
        description: "Тёплая текстура крафтовой бумаги с золотым тиснением и домашним уютом",
        systemIcon: "book.closed.fill",
        colorHexCodes: ["#D2B48C", "#C19A6B", "#8B5A2B"]
    )

    public static let roseGold = ScratchCardTheme(
        id: "rose_gold",
        name: "Розовое золото",
        requiredXP: 180,
        description: "Нежный мерцающий металлик с тёплым закатным отблеском",
        systemIcon: "heart.fill",
        colorHexCodes: ["#F4C2C2", "#E8A598", "#B76E79"]
    )

    public static let starryMidnight = ScratchCardTheme(
        id: "starry_midnight",
        name: "Звёздная полночь",
        requiredXP: 330,
        description: "Глубокий синий глянец с россыпью мерцающих серебряных созвездий",
        systemIcon: "moon.stars.fill",
        colorHexCodes: ["#1B2735", "#090A0F", "#4B6CB7"]
    )

    public static let shiningHolo = ScratchCardTheme(
        id: "shining_holo",
        name: "Сияющий голографик",
        requiredXP: 500,
        description: "Переливающаяся радужная призма для истинных исследователей впечатлений",
        systemIcon: "sparkles",
        colorHexCodes: ["#FF9A9E", "#FECFEF", "#A1C4FD", "#C2E9FB"]
    )

    /// Полный список всех рубашек в порядке разблокировки
    public static let allThemes: [ScratchCardTheme] = [
        .classicSilver,
        .cozyKraft,
        .roseGold,
        .starryMidnight,
        .shiningHolo
    ]

    /// Поиск темы по ID с безопасным фолбэком на «Классическое серебро»
    public static func theme(by id: String) -> ScratchCardTheme {
        allThemes.first { $0.id == id } ?? .classicSilver
    }
}

// MARK: - Шкала уровней пары (LevelMilestone)

/// Уровень прогрессии пары в игре.
public struct LevelMilestone: Identifiable, Hashable, Sendable {
    public let level: Int
    public let title: String
    public let thresholdXP: Int
    public let rewardThemeId: String
    public let badgeIcon: String

    public var id: Int { level }

    public init(
        level: Int,
        title: String,
        thresholdXP: Int,
        rewardThemeId: String,
        badgeIcon: String
    ) {
        self.level = level
        self.title = title
        self.thresholdXP = thresholdXP
        self.rewardThemeId = rewardThemeId
        self.badgeIcon = badgeIcon
    }

    /// Отображаемый заголовок с номером
    public var formattedTitle: String {
        "Уровень \(level) · «\(title)»"
    }

    /// 5 фиксированных уровней по спецификации v1.0
    public static let allMilestones: [LevelMilestone] = [
        LevelMilestone(
            level: 1,
            title: "Любопытный дуэт",
            thresholdXP: 0,
            rewardThemeId: "classic_silver",
            badgeIcon: "sparkle"
        ),
        LevelMilestone(
            level: 2,
            title: "Искатели моментов",
            thresholdXP: 60,
            rewardThemeId: "cozy_kraft",
            badgeIcon: "flame.fill"
        ),
        LevelMilestone(
            level: 3,
            title: "Сообщники по вечерам",
            thresholdXP: 180,
            rewardThemeId: "rose_gold",
            badgeIcon: "star.fill"
        ),
        LevelMilestone(
            level: 4,
            title: "Хранители историй",
            thresholdXP: 330,
            rewardThemeId: "starry_midnight",
            badgeIcon: "moon.stars.fill"
        ),
        LevelMilestone(
            level: 5,
            title: "Легенды приключений",
            thresholdXP: 500,
            rewardThemeId: "shining_holo",
            badgeIcon: "crown.fill"
        )
    ]

    /// Текущий достигнутый уровень для заданного количества опыта
    public static func milestone(for xp: Int) -> LevelMilestone {
        let matching = allMilestones.filter { xp >= $0.thresholdXP }
        return matching.last ?? allMilestones[0]
    }

    /// Следующий целевой уровень (nil, если уже достигнут максимальный 5 уровень)
    public static func nextMilestone(for xp: Int) -> LevelMilestone? {
        allMilestones.first { $0.thresholdXP > xp }
    }
}

// MARK: - Предопределённые значки достижений

extension AchievementDefinition {
    /// 8 базовых значков MVP
    public static let firstStep = AchievementDefinition(
        id: "first_step",
        title: "Первый шаг",
        description: "Завершить своё самое первое приключение",
        icon: "shoeprints.fill",
        bonusXP: 20
    )

    public static let cozyMasters = AchievementDefinition(
        id: "cozy_masters",
        title: "Мастера уюта",
        description: "Пройти 3 домашних приключения",
        icon: "house.fill",
        bonusXP: 20
    )

    public static let windOfChange = AchievementDefinition(
        id: "wind_of_change",
        title: "Ветер перемен",
        description: "Завершить первое приключение вне дома",
        icon: "wind",
        bonusXP: 20
    )

    public static let pureImprovisation = AchievementDefinition(
        id: "pure_improvisation",
        title: "Чистая импровизация",
        description: "Пройти приключение с тегом «Спонтанное» без единого пропуска карточек",
        icon: "sparkles",
        bonusXP: 20
    )

    public static let pricelessEvening = AchievementDefinition(
        id: "priceless_evening",
        title: "Бесценный вечер",
        description: "Пройти приключение с бюджетом 0 ₸",
        icon: "heart.fill",
        bonusXP: 20
    )

    public static let noSecrets = AchievementDefinition(
        id: "no_secrets",
        title: "Никаких секретов",
        description: "Завершить приключение из категории «Глубокое»",
        icon: "bubble.left.and.bubble.right.fill",
        bonusXP: 20
    )

    public static let chapterClosed = AchievementDefinition(
        id: "chapter_closed",
        title: "Глава закрыта",
        description: "Полностью пройти все 4 точки любой главы",
        icon: "flag.checkered",
        bonusXP: 20
    )

    public static let perfectWave = AchievementDefinition(
        id: "perfect_wave",
        title: "Идеальная волна",
        description: "Завершить 3 приключения подряд, выполнив все 12 карточек",
        icon: "water.waves",
        bonusXP: 20
    )

    public static let allPredefined: [AchievementDefinition] = [
        .firstStep,
        .cozyMasters,
        .windOfChange,
        .pureImprovisation,
        .pricelessEvening,
        .noSecrets,
        .chapterClosed,
        .perfectWave
    ]
}

// MARK: - SwiftData: Профиль пары (CoupleProfile)

/// Персистентный профиль пары в SwiftData.
@Model
public final class CoupleProfile {
    @Attribute(.unique) public var id: String
    public var partnerOneName: String
    public var partnerTwoName: String
    public var totalXP: Int
    public var unlockedAchievementIds: [String]
    public var selectedScratchThemeId: String
    public var excludedBoundaryRawValues: [String]
    public var hasCompletedOnboarding: Bool
    public var updatedAt: Date

    public init(
        id: String = "local_couple",
        partnerOneName: String = "Партнёр 1",
        partnerTwoName: String = "Партнёр 2",
        totalXP: Int = 0,
        unlockedAchievementIds: [String] = [],
        selectedScratchThemeId: String = "classic_silver",
        excludedBoundaryRawValues: [String] = [],
        hasCompletedOnboarding: Bool = false
    ) {
        self.id = id
        self.partnerOneName = partnerOneName
        self.partnerTwoName = partnerTwoName
        self.totalXP = totalXP
        self.unlockedAchievementIds = unlockedAchievementIds
        self.selectedScratchThemeId = selectedScratchThemeId
        self.excludedBoundaryRawValues = excludedBoundaryRawValues
        self.hasCompletedOnboarding = hasCompletedOnboarding
        self.updatedAt = .now
    }

    // MARK: - Вычисляемые свойства прогресса

    /// Текущий уровень пары
    public var currentLevel: LevelMilestone {
        LevelMilestone.milestone(for: totalXP)
    }

    /// Следующий целевой уровень (nil, если взят максимальный 5 уровень)
    public var nextLevel: LevelMilestone? {
        LevelMilestone.nextMilestone(for: totalXP)
    }

    /// Прогресс внутри текущего уровня от 0.0 до 1.0
    public var xpProgressInCurrentLevel: Double {
        guard let next = nextLevel else { return 1.0 }
        let currentThreshold = currentLevel.thresholdXP
        let nextThreshold = next.thresholdXP
        let range = Double(nextThreshold - currentThreshold)
        guard range > 0 else { return 1.0 }
        let currentProgress = Double(totalXP - currentThreshold)
        return min(max(currentProgress / range, 0.0), 1.0)
    }

    /// Оставшееся количество XP до следующего уровня
    public var xpRemainingToNextLevel: Int {
        guard let next = nextLevel else { return 0 }
        return max(next.thresholdXP - totalXP, 0)
    }

    /// Выбранная рубашка для скретч-карточки
    public var selectedTheme: ScratchCardTheme {
        ScratchCardTheme.theme(by: selectedScratchThemeId)
    }

    /// Список исключённых границ комфорта
    public var excludedBoundaries: [BoundaryTag] {
        get {
            excludedBoundaryRawValues.compactMap { BoundaryTag(rawValue: $0) }
        }
        set {
            excludedBoundaryRawValues = newValue.map(\.rawValue)
            updatedAt = .now
        }
    }

    /// Множество исключённых границ комфорта
    public var excludedBoundariesSet: Set<BoundaryTag> {
        Set(excludedBoundaries)
    }

    /// Проверка: разблокирована ли данная тема
    public func isThemeUnlocked(_ theme: ScratchCardTheme) -> Bool {
        totalXP >= theme.requiredXP
    }

    /// Проверка: открыт ли значок
    public func isAchievementUnlocked(_ id: String) -> Bool {
        unlockedAchievementIds.contains(id)
    }

    /// Очередь хода (Pass & Play): нечётные шаги (1 и 3) — Партнёр 1, чётные (2 и 4) — Партнёр 2
    public func activePartnerName(for stepIndex: Int) -> String {
        (stepIndex % 2 == 1) ? partnerOneName : partnerTwoName
    }

    /// Имя партнёра-напарника (для подсказок)
    public func otherPartnerName(for stepIndex: Int) -> String {
        (stepIndex % 2 == 1) ? partnerTwoName : partnerOneName
    }

    /// Добавление очков опыта
    public func addXP(_ amount: Int) {
        guard amount > 0 else { return }
        totalXP += amount
        updatedAt = .now
    }

    /// Открытие достижения (возвращает true, если значок открыт впервые)
    @discardableResult
    public func unlockAchievement(id: String, bonusXP: Int = 20) -> Bool {
        guard !unlockedAchievementIds.contains(id) else { return false }
        unlockedAchievementIds.append(id)
        totalXP += bonusXP
        updatedAt = .now
        return true
    }
}

// MARK: - SwiftData: Сессия активного приключения (AdventureSessionRecord)

/// Персистентная сессия прохождения приключения.
/// Позволяет не терять прогресс вечера при выходе из приложения или блокировке экрана.
@Model
public final class AdventureSessionRecord {
    @Attribute(.unique) public var sessionId: String
    public var adventureId: String
    public var startedAt: Date
    public var completedAt: Date?
    public var currentStepIndex: Int // 1...4
    public var isCurrentCardScratched: Bool
    public var isUsingFallbackOnCurrentStep: Bool
    /// Статусы этапов: "pending", "completed", "skipped"
    public var stepStatuses: [String]
    public var earnedXP: Int
    public var isFinished: Bool

    public init(adventureId: String, stepCount: Int = 4) {
        self.sessionId = UUID().uuidString
        self.adventureId = adventureId
        self.startedAt = .now
        self.completedAt = nil
        self.currentStepIndex = 1
        self.isCurrentCardScratched = false
        self.isUsingFallbackOnCurrentStep = false
        self.stepStatuses = Array(repeating: "pending", count: stepCount)
        self.earnedXP = 0
        self.isFinished = false
    }

    // MARK: - Хелперы управления этапом

    /// Статус этапа по 1-based номеру шага (1..4)
    public func status(for stepIndex: Int) -> String {
        let index = stepIndex - 1
        guard stepIndex >= 1 && index < stepStatuses.count else { return "pending" }
        return stepStatuses[index]
    }

    /// Проверка, выполнен ли шаг
    public func isStepCompleted(at stepIndex: Int) -> Bool {
        status(for: stepIndex) == "completed"
    }

    /// Проверка, пропущен ли шаг
    public func isStepSkipped(at stepIndex: Int) -> Bool {
        status(for: stepIndex) == "skipped"
    }

    /// Количество завершённых шагов
    public var completedStepsCount: Int {
        stepStatuses.filter { $0 == "completed" }.count
    }

    /// Количество пропущенных шагов
    public var skippedStepsCount: Int {
        stepStatuses.filter { $0 == "skipped" }.count
    }

    /// Выполнены ли все шаги без единого пропуска
    public var isAllCardsCompletedWithoutSkip: Bool {
        !stepStatuses.isEmpty && stepStatuses.allSatisfy { $0 == "completed" }
    }

    /// Отметка статуса текущего шага
    public func markCurrentStep(completed: Bool, xp: Int) {
        let index = currentStepIndex - 1
        if index >= 0 && index < stepStatuses.count {
            stepStatuses[index] = completed ? "completed" : "skipped"
        }
        if completed {
            earnedXP += xp
        }
    }

    /// Завершение текущего шага (+10 XP) и переход к следующему
    public func completeCurrentStep(xpReward: Int = 10) {
        let index = currentStepIndex - 1
        if index >= 0 && index < stepStatuses.count {
            stepStatuses[index] = "completed"
        }
        earnedXP += xpReward
        advanceToNextStepOrFinish()
    }

    /// Пропуск текущего шага (0 XP) и переход к следующему
    public func skipCurrentStep() {
        let index = currentStepIndex - 1
        if index >= 0 && index < stepStatuses.count {
            stepStatuses[index] = "skipped"
        }
        advanceToNextStepOrFinish()
    }

    /// Переключение на сюжетный фолбэк для текущего шага
    public func switchToFallback() {
        isUsingFallbackOnCurrentStep = true
    }

    /// Отметка карточки как стёртой
    public func markScratched() {
        isCurrentCardScratched = true
    }

    /// Переход к следующему шагу или финализация сессии
    private func advanceToNextStepOrFinish() {
        if currentStepIndex < stepStatuses.count {
            currentStepIndex += 1
            isCurrentCardScratched = false
            isUsingFallbackOnCurrentStep = false
        } else {
            finishSession()
        }
    }

    /// Финализация приключения (+25 XP бонуса финала)
    public func finishSession(finishBonusXP: Int = 25) {
        guard !isFinished else { return }
        isFinished = true
        completedAt = .now
        earnedXP += finishBonusXP
    }
}
