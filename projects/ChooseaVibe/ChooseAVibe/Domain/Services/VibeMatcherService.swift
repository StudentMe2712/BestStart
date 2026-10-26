import Foundation

// MARK: - Критерии подбора вайба

/// Параметры выбора параметров вечера для алгоритма Weighted Soft-Matching
public struct VibeCriteria: Codable, Hashable, Sendable {
    /// Выбранные настроения вечера (1 или 2 вайба)
    public var vibes: Set<VibeTag>
    /// Желаемая продолжительность (опционально)
    public var duration: DurationTier?
    /// Допустимый бюджет в тенге (опционально)
    public var budget: BudgetTier?
    /// Предпочтительная локация (Дома, Вне дома, Неважно)
    public var location: LocationType

    public init(
        vibes: Set<VibeTag> = [],
        duration: DurationTier? = nil,
        budget: BudgetTier? = nil,
        location: LocationType = .any
    ) {
        self.vibes = vibes
        self.duration = duration
        self.budget = budget
        self.location = location
    }
}

// MARK: - Результат подбора

/// Результат работы алгоритма подбора приключения
public struct MatchResult: Identifiable, Hashable, Sendable {
    public var id: String { adventure.id }
    /// Подобранное приключение
    public let adventure: AdventureDefinition
    /// Суммарный набранный балл по шкале мягкого скоринга
    public let score: Int
    /// Флаг фолбэка: true, если точное совпадение не найдено или все подходящие квесты уже пройдены
    public let isFallbackMatch: Bool

    public init(adventure: AdventureDefinition, score: Int, isFallbackMatch: Bool) {
        self.adventure = adventure
        self.score = score
        self.isFallbackMatch = isFallbackMatch
    }
}

// MARK: - Сервис подбора приключений (Weighted Soft-Matching)

/// Сервис подбора сценария свидания по алгоритму Weighted Soft-Matching без тупиков (Zero Dead-Ends)
public final class VibeMatcherService: Sendable {

    public static let shared = VibeMatcherService()

    public init() {}

    /// Основная функция подбора приключения по спецификации
    /// - Parameters:
    ///   - criteria: Выбранные парой критерии вечера (вайб, длительность, бюджет, локация)
    ///   - adventures: Доступный пул приключений из каталога
    ///   - completedIds: Набор идентификаторов уже завершённых приключений
    ///   - excludedBoundaries: Набор тегов личных границ (стоп-лист пары)
    ///   - currentChapterId: Идентификатор текущей открытой главы пары
    /// - Returns: `MatchResult` с выбранным сценарием, скором и статусом фолбэка
    public func matchAdventure(
        criteria: VibeCriteria,
        adventures: [AdventureDefinition],
        completedIds: Set<String>,
        excludedBoundaries: Set<BoundaryTag>,
        currentChapterId: String
    ) -> MatchResult {
        guard !adventures.isEmpty else {
            fatalError("Каталог приключений пуст. Невозможно подобрать квест.")
        }

        // 1. Оценка каждого приключения: жёсткие фильтры + весовой скоринг
        var candidates: [ScoredCandidate] = []
        for adv in adventures {
            let passesHard = evaluateHardConstraints(
                adventure: adv,
                criteria: criteria,
                excludedBoundaries: excludedBoundaries
            )
            let (score, vibeMatches) = calculateScore(
                adventure: adv,
                criteria: criteria,
                currentChapterId: currentChapterId
            )
            let isCompleted = completedIds.contains(adv.id)

            candidates.append(ScoredCandidate(
                adventure: adv,
                score: score,
                passesHardConstraints: passesHard,
                vibeMatches: vibeMatches,
                isCompleted: isCompleted
            ))
        }

        // 2. Детерминированная сортировка:
        //    - по набранному баллу (убывание)
        //    - приоритет непройденным квестам
        //    - приоритет текущей открытой главе
        //    - оригинальный порядок в главе
        let sortedCandidates = candidates.sorted { lhs, rhs in
            if lhs.score != rhs.score {
                return lhs.score > rhs.score
            }
            if lhs.isCompleted != rhs.isCompleted {
                return !lhs.isCompleted && rhs.isCompleted
            }
            let lhsInCurrent = (lhs.adventure.chapterId == currentChapterId)
            let rhsInCurrent = (rhs.adventure.chapterId == currentChapterId)
            if lhsInCurrent != rhsInCurrent {
                return lhsInCurrent && !rhsInCurrent
            }
            if lhs.adventure.orderInChapter != rhs.adventure.orderInChapter {
                return lhs.adventure.orderInChapter < rhs.adventure.orderInChapter
            }
            return lhs.adventure.id < rhs.adventure.id
        }

        // 3. Ступени выбора без тупиков (Zero Dead-Ends matching)

        // Ступень 1: Идеальное совпадение
        // Проходит жёсткие фильтры, ещё не пройдено, и есть совпадение по вайбу (если вайбы заданы)
        let exactMatch = sortedCandidates.first { candidate in
            guard candidate.passesHardConstraints && !candidate.isCompleted else { return false }
            if criteria.vibes.isEmpty { return true }
            return candidate.vibeMatches
        }
        if let match = exactMatch {
            return MatchResult(
                adventure: match.adventure,
                score: match.score,
                isFallbackMatch: false
            )
        }

        // Ступень 2: Фолбэк среди непройденных приключений с соблюдением жёстких фильтров
        let uncompletedFallback = sortedCandidates.first { candidate in
            candidate.passesHardConstraints && !candidate.isCompleted
        }
        if let match = uncompletedFallback {
            return MatchResult(
                adventure: match.adventure,
                score: match.score,
                isFallbackMatch: true
            )
        }

        // Ступень 3: Все подходящие непройденные квесты завершены — повторное прохождение лучшего
        let completedFallback = sortedCandidates.first { candidate in
            candidate.passesHardConstraints && candidate.isCompleted
        }
        if let match = completedFallback {
            return MatchResult(
                adventure: match.adventure,
                score: match.score,
                isFallbackMatch: true
            )
        }

        // Ступень 4: Если строгие границы отсекли абсолютно всё — выбираем лучший непройденный
        let relaxedUncompleted = sortedCandidates.first { candidate in
            !candidate.isCompleted
        }
        if let match = relaxedUncompleted {
            return MatchResult(
                adventure: match.adventure,
                score: match.score,
                isFallbackMatch: true
            )
        }

        // Ступень 5: Абсолютный фолбэк — квест с наибольшим баллом из всего каталога
        let absoluteFallback = sortedCandidates[0]
        return MatchResult(
            adventure: absoluteFallback.adventure,
            score: absoluteFallback.score,
            isFallbackMatch: true
        )
    }

    /// Статическая утилита для быстрого вызова
    public static func matchAdventure(
        criteria: VibeCriteria,
        adventures: [AdventureDefinition],
        completedIds: Set<String>,
        excludedBoundaries: Set<BoundaryTag>,
        currentChapterId: String
    ) -> MatchResult {
        VibeMatcherService.shared.matchAdventure(
            criteria: criteria,
            adventures: adventures,
            completedIds: completedIds,
            excludedBoundaries: excludedBoundaries,
            currentChapterId: currentChapterId
        )
    }

    // MARK: - Жёсткие фильтры (Hard Constraints)

    /// Проверка жёстких ограничений
    /// - Исключение карточек с boundaryTags из excludedBoundaries
    /// - Если локация "Дома", исключить строго "Вне дома" (и наоборот)
    /// - Если бюджет 0 ₸ (.free), исключить квесты с обязательными покупками (budget > 0)
    private func evaluateHardConstraints(
        adventure: AdventureDefinition,
        criteria: VibeCriteria,
        excludedBoundaries: Set<BoundaryTag>
    ) -> Bool {
        // 1. Личные границы
        if !excludedBoundaries.isEmpty {
            for card in adventure.cards {
                let cardBoundaries = Set(card.boundaryTags)
                if !cardBoundaries.isDisjoint(with: excludedBoundaries) {
                    return false
                }
            }
        }

        // 2. Локация: строгое отсечение несовместимых мест
        switch criteria.location {
        case .home:
            if adventure.location == .outside {
                return false
            }
        case .outside:
            if adventure.location == .home {
                return false
            }
        case .any:
            break
        }

        // 3. Бюджет: при бесплатном вечере исключаются любые квесты с ненулевым бюджетом
        if let budget = criteria.budget, budget == .free {
            if adventure.budget.rawValue > 0 {
                return false
            }
        }

        return true
    }

    // MARK: - Мягкий весовой скоринг (Soft Scoring)

    /// Подсчёт баллов мягкого скоринга:
    /// - +50 за совпадение основного вайба
    /// - +25 за совпадение дополнительного вайба
    /// - +20 за точное совпадение времени (+10 за соседний интервал)
    /// - +15 за попадание в выбранный бюджет
    /// - +10 за принадлежность к текущей открытой главе
    private func calculateScore(
        adventure: AdventureDefinition,
        criteria: VibeCriteria,
        currentChapterId: String
    ) -> (score: Int, vibeMatches: Bool) {
        var score = 0
        var vibeMatches = false

        // 1. Вайбы (Настроение)
        if let primaryVibe = adventure.vibes.first {
            if criteria.vibes.contains(primaryVibe) {
                score += 50
                vibeMatches = true
            }
        }

        for secondaryVibe in adventure.vibes.dropFirst() {
            if criteria.vibes.contains(secondaryVibe) {
                score += 25
                vibeMatches = true
            }
        }

        // 2. Длительность: +20 точное, +10 соседний интервал
        if let criteriaDuration = criteria.duration {
            if adventure.duration == criteriaDuration {
                score += 20
            } else if areAdjacentDurations(adventure.duration, criteriaDuration) {
                score += 10
            }
        }

        // 3. Бюджет: +15 при непревышении выбранного порога
        if let criteriaBudget = criteria.budget {
            if adventure.budget.rawValue <= criteriaBudget.rawValue {
                score += 15
            }
        }

        // 4. Глава: +10 за текущую открытую главу для естественного движения по карте
        if adventure.chapterId == currentChapterId {
            score += 10
        }

        return (score, vibeMatches)
    }

    /// Определение смежности интервалов времени:
    /// 15–30 мин <-> 30–60 мин <-> 60–120 мин
    private func areAdjacentDurations(_ a: DurationTier, _ b: DurationTier) -> Bool {
        switch (a, b) {
        case (.short, .medium), (.medium, .short):
            return true
        case (.medium, .long), (.long, .medium):
            return true
        default:
            return false
        }
    }
}

// MARK: - Внутренняя структура для ранжирования

private struct ScoredCandidate {
    let adventure: AdventureDefinition
    let score: Int
    let passesHardConstraints: Bool
    let vibeMatches: Bool
    let isCompleted: Bool
}
