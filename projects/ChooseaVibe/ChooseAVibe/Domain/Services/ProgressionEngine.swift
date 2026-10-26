import Foundation

// MARK: - Модель уровня пары

/// Информация об уровне пары и открываемых наградах
public struct CoupleLevel: Equatable, Identifiable, Sendable {
    /// Порядковый номер уровня (1...5)
    public let levelNumber: Int
    /// Название уровня на русском языке
    public let title: String
    /// Необходимый суммарный порог XP для достижения уровня
    public let requiredXP: Int
    /// Идентификатор открываемой рубашки скретч-карточки
    public let scratchThemeId: String
    /// Название открываемой рубашки
    public let scratchThemeTitle: String
    /// Атмосферное описание ранга пары
    public let descriptionText: String

    public var id: Int { levelNumber }
    public var level: Int { levelNumber }

    /// Необходимый порог для перехода на следующий уровень
    public var nextLevelXP: Int? {
        ProgressionEngine.nextLevelThreshold(for: requiredXP)
    }

    /// Процент прогресса внутри уровня от 0.0 до 1.0
    public func progressPercentage(currentXP: Int) -> Double {
        ProgressionEngine.progressToNextLevel(for: currentXP)
    }

    public init(
        levelNumber: Int,
        title: String,
        requiredXP: Int,
        scratchThemeId: String,
        scratchThemeTitle: String,
        descriptionText: String
    ) {
        self.levelNumber = levelNumber
        self.title = title
        self.requiredXP = requiredXP
        self.scratchThemeId = scratchThemeId
        self.scratchThemeTitle = scratchThemeTitle
        self.descriptionText = descriptionText
    }
}

// MARK: - Расширения модели достижений

extension AchievementDefinition {
    public var descriptionText: String { description }
    public var iconName: String { icon }
    public var xpReward: Int { bonusXP }

    public static var pureImprov: AchievementDefinition { pureImprovisation }
}

// MARK: - Движок прогрессии и достижений

/// Движок расчёта опыта, уровней, разблокировки глав и значков
public final class ProgressionEngine: Sendable {

    public static let shared = ProgressionEngine()

    public init() {}

    // MARK: - 1. Константы уровней

    /// Все 5 доступных уровней пары
    public static let allLevels: [CoupleLevel] = [
        CoupleLevel(
            levelNumber: 1,
            title: "Любопытный дуэт",
            requiredXP: 0,
            scratchThemeId: "classic_silver",
            scratchThemeTitle: "Классическое серебро",
            descriptionText: "Первые совместные шаги в мире свиданий-приключений"
        ),
        CoupleLevel(
            levelNumber: 2,
            title: "Искатели моментов",
            requiredXP: 60,
            scratchThemeId: "cozy_kraft",
            scratchThemeTitle: "Уютный крафт",
            descriptionText: "Коллекционируете тёплые воспоминания и новые вкусы"
        ),
        CoupleLevel(
            levelNumber: 3,
            title: "Сообщники по вечерам",
            requiredXP: 180,
            scratchThemeId: "rose_gold",
            scratchThemeTitle: "Розовое золото",
            descriptionText: "Понимаете настроение друг друга с полувзгляда"
        ),
        CoupleLevel(
            levelNumber: 4,
            title: "Хранители историй",
            requiredXP: 330,
            scratchThemeId: "starry_midnight",
            scratchThemeTitle: "Звёздная полночь",
            descriptionText: "Глубокая искренность, доверие и багаж личных традиций"
        ),
        CoupleLevel(
            levelNumber: 5,
            title: "Легенды приключений",
            requiredXP: 500,
            scratchThemeId: "shining_holo",
            scratchThemeTitle: "Сияющий голографик",
            descriptionText: "Высшая сыгранность — каждый вечер становится маленьким фильмом"
        )
    ]

    // MARK: - 2. Константы достижений (8 значков MVP)

    public static let firstStep = AchievementDefinition.firstStep
    public static let cozyMasters = AchievementDefinition.cozyMasters
    public static let windOfChange = AchievementDefinition.windOfChange
    public static let pureImprov = AchievementDefinition.pureImprovisation
    public static let pricelessEvening = AchievementDefinition.pricelessEvening
    public static let noSecrets = AchievementDefinition.noSecrets
    public static let chapterClosed = AchievementDefinition.chapterClosed
    public static let perfectWave = AchievementDefinition.perfectWave

    /// Полный список всех 8 значков MVP
    public static let allAchievements: [AchievementDefinition] = [
        firstStep,
        cozyMasters,
        windOfChange,
        pureImprov,
        pricelessEvening,
        noSecrets,
        chapterClosed,
        perfectWave
    ]

    // MARK: - 3. Идемпотентное начисление опыта (XP)

    /// Завершение этапа карточки
    /// - Parameters:
    ///   - session: Запись активной сессии
    ///   - stepIndex: Номер этапа (1...4 или 0...3)
    ///   - isReplay: Флаг повторного прохождения (дает 50% XP)
    /// - Returns: Количество начисленного опыта (+10 XP или +5 XP при повторе; 0 если уже был завершён)
    @discardableResult
    public func completeStep(session: AdventureSessionRecord, stepIndex: Int, isReplay: Bool = false) -> Int {
        guard let normalizedIndex = normalizeStepIndex(stepIndex, totalCount: session.stepStatuses.count) else {
            return 0
        }

        // Идемпотентность: если этап уже помечен завершённым, повторно опыт не начисляется
        if session.stepStatuses[normalizedIndex] == "completed" {
            return 0
        }

        let gainedXP = isReplay ? 5 : 10
        session.stepStatuses[normalizedIndex] = "completed"
        session.earnedXP += gainedXP
        return gainedXP
    }

    /// Пропуск этапа карточки без начисления очков
    /// - Parameters:
    ///   - session: Запись активной сессии
    ///   - stepIndex: Номер этапа
    public func skipStep(session: AdventureSessionRecord, stepIndex: Int) {
        guard let normalizedIndex = normalizeStepIndex(stepIndex, totalCount: session.stepStatuses.count) else {
            return
        }

        // Пропуск фиксирует статус "skipped", 0 XP
        session.stepStatuses[normalizedIndex] = "skipped"
    }

    /// Завершение приключения (финал 4-го этапа)
    /// - Parameters:
    ///   - session: Запись активной сессии
    ///   - isReplay: Флаг повторного прохождения (дает +15 XP вместо +25 XP)
    /// - Returns: Количество начисленного бонуса (+25 XP или +15 XP; 0 если приключение уже было завершено)
    @discardableResult
    public func finishAdventure(session: AdventureSessionRecord, isReplay: Bool = false) -> Int {
        // Идемпотентность: финал начисляется ровно 1 раз
        if session.isFinished {
            return 0
        }

        let finishBonus = isReplay ? 15 : 25
        session.isFinished = true
        session.completedAt = Date()
        session.earnedXP += finishBonus
        return finishBonus
    }

    // MARK: - 4. Расчёт уровня пары

    /// Текущий уровень пары на основе суммарного XP
    public static func currentLevel(for xp: Int) -> CoupleLevel {
        let safeXP = max(0, xp)
        let matched = allLevels.last { safeXP >= $0.requiredXP }
        return matched ?? allLevels[0]
    }

    /// Инстанс-метод для удобного вызова из представлений
    public func level(for xp: Int) -> CoupleLevel {
        Self.currentLevel(for: xp)
    }

    /// Следующий уровень пары (nil, если достигнут максимальный 5-й уровень)
    public static func nextLevel(for xp: Int) -> CoupleLevel? {
        let current = currentLevel(for: xp)
        guard current.levelNumber < allLevels.count else { return nil }
        return allLevels[current.levelNumber]
    }

    /// Необходимый порог XP для следующего уровня (nil для 5-го уровня)
    public static func nextLevelThreshold(for xp: Int) -> Int? {
        nextLevel(for: xp)?.requiredXP
    }

    /// Сколько очков опыта осталось набрать до следующего уровня (0 для 5-го уровня)
    public static func xpToNextLevel(for xp: Int) -> Int {
        guard let next = nextLevel(for: xp) else { return 0 }
        return max(0, next.requiredXP - xp)
    }

    /// Относительный прогресс заполнения шкалы текущего уровня (от 0.0 до 1.0)
    public static func progressToNextLevel(for xp: Int) -> Double {
        let current = currentLevel(for: xp)
        guard let next = nextLevel(for: xp) else { return 1.0 }

        let currentFloor = current.requiredXP
        let nextCeiling = next.requiredXP
        let span = nextCeiling - currentFloor
        guard span > 0 else { return 1.0 }

        let currentGained = max(0, xp - currentFloor)
        let ratio = Double(currentGained) / Double(span)
        return min(max(ratio, 0.0), 1.0)
    }

    // MARK: - 5. Разблокировка глав

    /// Проверка, открыта ли конкретная глава
    /// - Глава 1: доступна сразу
    /// - Глава 2: открывается после 3 любых пройденных приключений из Главы 1
    /// - Глава 3: открывается после 3 любых пройденных приключений из Главы 2
    public static func isChapterUnlocked(
        chapterNumber: Int,
        completedAdventureIds: Set<String>,
        allAdventures: [AdventureDefinition]
    ) -> Bool {
        switch chapterNumber {
        case 1:
            return true
        case 2:
            let completedInChapter1 = completedCount(
                inChapterNumber: 1,
                completedAdventureIds: completedAdventureIds,
                allAdventures: allAdventures
            )
            return completedInChapter1 >= 3
        case 3:
            let completedInChapter2 = completedCount(
                inChapterNumber: 2,
                completedAdventureIds: completedAdventureIds,
                allAdventures: allAdventures
            )
            return completedInChapter2 >= 3
        default:
            return false
        }
    }

    /// Проверка открытия главы по строковому идентификатору главы ("chapter_1", "chapter_2", "chapter_3")
    public static func isChapterUnlocked(
        chapterId: String,
        completedAdventureIds: Set<String>,
        allAdventures: [AdventureDefinition]
    ) -> Bool {
        let num = chapterNumber(from: chapterId)
        return isChapterUnlocked(
            chapterNumber: num,
            completedAdventureIds: completedAdventureIds,
            allAdventures: allAdventures
        )
    }

    /// Количество завершённых приключений в конкретной главе
    public static func completedCount(
        inChapterNumber chapterNum: Int,
        completedAdventureIds: Set<String>,
        allAdventures: [AdventureDefinition]
    ) -> Int {
        allAdventures.filter { adv in
            chapterNumber(from: adv.chapterId) == chapterNum && completedAdventureIds.contains(adv.id)
        }.count
    }

    /// Список идентификаторов всех открытых глав
    public static func unlockedChapterIds(
        completedAdventureIds: Set<String>,
        allAdventures: [AdventureDefinition]
    ) -> Set<String> {
        var unlocked: Set<String> = []
        let uniqueChapterIds = Set(allAdventures.map(\.chapterId))

        for chId in uniqueChapterIds {
            if isChapterUnlocked(
                chapterId: chId,
                completedAdventureIds: completedAdventureIds,
                allAdventures: allAdventures
            ) {
                unlocked.insert(chId)
            }
        }

        if unlocked.isEmpty {
            unlocked.insert("chapter_1")
        }
        return unlocked
    }

    // MARK: - 6. Проверка и начисление 8 достижений (+20 XP каждое)

    /// Проверка выполнения условий всех 8 значков с возвратом тех, условия которых выполнены
    public static func checkAchievements(
        currentAdventure: AdventureDefinition?,
        currentSession: AdventureSessionRecord?,
        allAdventures: [AdventureDefinition],
        completedAdventureIds: Set<String>,
        finishedSessions: [AdventureSessionRecord]
    ) -> [AchievementDefinition] {
        var eligible: [AchievementDefinition] = []

        let adventuresMap = Dictionary(uniqueKeysWithValues: allAdventures.map { ($0.id, $0) })
        let completedAdventures = completedAdventureIds.compactMap { adventuresMap[$0] }

        // 1. «Первый шаг» — Завершить своё самое первое приключение
        let totalFinished = finishedSessions.filter(\.isFinished).count
        if completedAdventureIds.count >= 1 || totalFinished >= 1 {
            eligible.append(firstStep)
        }

        // 2. «Мастера уюта» — Пройти 3 домашних приключения
        let homeCount = completedAdventures.filter { $0.location == .home }.count
        if homeCount >= 3 {
            eligible.append(cozyMasters)
        }

        // 3. «Ветер перемен» — Завершить первое приключение вне дома
        let outsideCount = completedAdventures.filter { $0.location == .outside }.count
        if outsideCount >= 1 {
            eligible.append(windOfChange)
        }

        // 4. «Чистая импровизация» — Пройти приключение с тегом «Спонтанное» без единого пропуска
        let hasFlawlessSpontaneous = finishedSessions.contains { session in
            guard session.isFinished else { return false }
            guard let adv = adventuresMap[session.adventureId] else { return false }
            guard adv.vibes.contains(.spontaneous) else { return false }
            let hadSkips = session.stepStatuses.contains("skipped")
            let completedSteps = session.stepStatuses.filter { $0 == "completed" }.count
            return !hadSkips && completedSteps == session.stepStatuses.count
        }
        if hasFlawlessSpontaneous {
            eligible.append(pureImprov)
        }

        // 5. «Бесценный вечер» — Пройти приключение с бюджетом 0 ₸ (.free)
        let freeCount = completedAdventures.filter { $0.budget == .free || $0.budget.rawValue == 0 }.count
        if freeCount >= 1 {
            eligible.append(pricelessEvening)
        }

        // 6. «Никаких секретов» — Завершить приключение из категории «Глубокое»
        let deepCount = completedAdventures.filter { $0.vibes.contains(.deep) }.count
        if deepCount >= 1 {
            eligible.append(noSecrets)
        }

        // 7. «Глава закрыта» — Полностью пройти все 4 точки любой главы
        let chapters = Dictionary(grouping: allAdventures, by: \.chapterId)
        let hasAnyFullChapter = chapters.values.contains { chapterCards in
            guard chapterCards.count >= 4 else { return false }
            return chapterCards.allSatisfy { completedAdventureIds.contains($0.id) }
        }
        if hasAnyFullChapter {
            eligible.append(chapterClosed)
        }

        // 8. «Идеальная волна» — Завершить 3 приключения подряд, выполнив все 12 карточек
        if hasPerfectWaveStreak(sessions: finishedSessions) {
            eligible.append(perfectWave)
        }

        return eligible
    }

    /// Идемпотентная оценка и начисление очков за новые значки в профиль пары
    @discardableResult
    public static func evaluateAndAwardAchievements(
        profile: CoupleProfile,
        currentSession: AdventureSessionRecord,
        currentAdventure: AdventureDefinition,
        allAdventures: [AdventureDefinition],
        pastFinishedSessions: [AdventureSessionRecord]
    ) -> [AchievementDefinition] {
        var allFinished = pastFinishedSessions
        if !allFinished.contains(where: { $0.sessionId == currentSession.sessionId }) {
            allFinished.append(currentSession)
        }

        var completedIds = Set(allFinished.map(\.adventureId))
        completedIds.insert(currentAdventure.id)

        let eligible = checkAchievements(
            currentAdventure: currentAdventure,
            currentSession: currentSession,
            allAdventures: allAdventures,
            completedAdventureIds: completedIds,
            finishedSessions: allFinished
        )

        let alreadyUnlocked = Set(profile.unlockedAchievementIds)
        var newlyUnlocked: [AchievementDefinition] = []

        for ach in eligible {
            if !alreadyUnlocked.contains(ach.id) {
                profile.unlockedAchievementIds.append(ach.id)
                profile.totalXP += ach.bonusXP
                newlyUnlocked.append(ach)
            }
        }

        profile.updatedAt = Date()
        return newlyUnlocked
    }

    // MARK: - Вспомогательные приватные методы

    /// Безопасная нормализация индекса этапа (поддержка 1-based и 0-based индексов)
    private func normalizeStepIndex(_ index: Int, totalCount: Int) -> Int? {
        guard totalCount > 0 else { return nil }
        if index >= 1 && index <= totalCount {
            return index - 1
        }
        if index >= 0 && index < totalCount {
            return index
        }
        return nil
    }

    /// Парсинг номера главы из идентификатора ("chapter_1", "ch_01", "1")
    private static func chapterNumber(from chapterId: String) -> Int {
        let cleaned = chapterId.replacingOccurrences(of: "chapter_", with: "")
            .replacingOccurrences(of: "ch_", with: "")
            .replacingOccurrences(of: "chapter", with: "")
            .replacingOccurrences(of: "0", with: "")
        if let num = Int(cleaned) {
            return num
        }
        return 1
    }

    /// Проверка цепочки из 3 сессий подряд с идеальным выполнением всех шагов (12/12 карточек)
    private static func hasPerfectWaveStreak(sessions: [AdventureSessionRecord]) -> Bool {
        let finished = sessions.filter(\.isFinished)
        guard finished.count >= 3 else { return false }

        var currentStreak = 0
        for s in finished {
            let hadSkips = s.stepStatuses.contains("skipped")
            let completedSteps = s.stepStatuses.filter { $0 == "completed" }.count
            let isFlawless = !hadSkips && completedSteps >= 4

            if isFlawless {
                currentStreak += 1
                if currentStreak >= 3 {
                    return true
                }
            } else {
                currentStreak = 0
            }
        }
        return false
    }
}
