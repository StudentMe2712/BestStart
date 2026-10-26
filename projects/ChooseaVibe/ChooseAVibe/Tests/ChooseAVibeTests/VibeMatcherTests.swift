//
//  VibeMatcherTests.swift
//  ChooseAVibeTests
//
//  Набор модульных тестов для алгоритма подбора приключений (Weighted Soft-Matching).
//  Проверяет: Zero Dead-Ends, жёсткие фильтры, весовой скоринг и фолбэк-флаг.
//

import XCTest
#if canImport(ChooseAVibeCore)
@testable import ChooseAVibeCore
#endif
#if canImport(ChooseAVibe)
@testable import ChooseAVibe
#endif

final class VibeMatcherTests: XCTestCase {

    private var matcher: VibeMatcherService!
    private var allAdventures: [AdventureDefinition] = []

    override func setUp() {
        super.setUp()
        matcher = VibeMatcherService.shared
        allAdventures = loadTestAdventures()
    }

    override func tearDown() {
        matcher = nil
        allAdventures = []
        super.tearDown()
    }

    // MARK: - 1. Zero Dead-Ends on All Combinations

    /// Тестирование алгоритма Weighted Soft-Matching без тупиков:
    /// Перебор различных комбинаций вайбов, длительности, бюджетов, локаций и списков завершённых квестов.
    /// Ни одна комбинация не должна возвращать nil или вызывать crash.
    func testZeroDeadEndsOnAllCombinations() {
        XCTAssertFalse(allAdventures.isEmpty, "Каталог приключений должен быть загружен для тестирования")

        // Составляем наборы вайбов: пустой, одиночные и пары
        var vibeCombinations: [Set<VibeTag>] = [[]]
        for vibe in VibeTag.allCases {
            vibeCombinations.append([vibe])
        }
        let allVibes = VibeTag.allCases
        for i in 0..<allVibes.count {
            for j in (i + 1)..<allVibes.count {
                vibeCombinations.append([allVibes[i], allVibes[j]])
            }
        }

        let durations: [DurationTier?] = DurationTier.allCases.map { Optional($0) } + [nil]
        let budgets: [BudgetTier?] = BudgetTier.allCases.map { Optional($0) } + [nil]
        let locations = LocationType.allCases

        let allAdventureIds = Set(allAdventures.map(\.id))
        let partialCompletedIds: Set<String> = Set(allAdventures.prefix(6).map(\.id))

        // Проверяем 3 сценария прогресса:
        // A: Новая пара (0 завершённых)
        // B: Пара на середине пути (6 завершённых)
        // C: Пара прошла все 12 квестов (все завершены — должен сработать фолбэк повторного прохождения)
        let completionScenarios: [Set<String>] = [
            [],
            partialCompletedIds,
            allAdventureIds
        ]

        var totalCombinationsTested = 0

        for completed in completionScenarios {
            for vibes in vibeCombinations {
                for duration in durations {
                    for budget in budgets {
                        for location in locations {
                            let criteria = VibeCriteria(
                                vibes: vibes,
                                duration: duration,
                                budget: budget,
                                location: location
                            )

                            let result = matcher.matchAdventure(
                                criteria: criteria,
                                adventures: allAdventures,
                                completedIds: completed,
                                excludedBoundaries: [],
                                currentChapterId: "ch_01"
                            )

                            // Главный инвариант Zero Dead-Ends:
                            // Всегда возвращается валидный MatchResult с непустым квестом
                            XCTAssertFalse(result.adventure.id.isEmpty, "Результат должен содержать непустой id приключения")
                            XCTAssertFalse(result.adventure.title.isEmpty, "Результат должен содержать приключение с непустым названием")
                            XCTAssertGreaterThanOrEqual(result.score, 0, "Скор не должен быть отрицательным")

                            // Если все квесты уже пройдены, алгоритм обязан выставить флаг фолбэка
                            if completed == allAdventureIds {
                                XCTAssertTrue(result.isFallbackMatch, "При завершении всех квестов должен срабатывать фолбэк")
                            }

                            totalCombinationsTested += 1
                        }
                    }
                }
            }
        }

        XCTAssertGreaterThan(totalCombinationsTested, 500, "Должно быть протестировано обширное пространство параметров")
    }

    // MARK: - 2. Boundary Tag Hard Filter

    /// При исключении boundary tag квесты с этим тегом не попадают в первичный выбор.
    func testBoundaryTagHardFilter() {
        let tagToExclude = BoundaryTag.dancingOrSinging

        // Создаём контролируемый набор из двух приключений:
        // advWithBoundary — идеально совпадает по вайбу, но содержит запрещённый тег
        // advClean — не идеален по вайбу, но свободен от стоп-тега
        let cardWithBoundary = ChallengeCardDefinition(
            id: "card_boundary",
            stepIndex: 1,
            primary: ChallengeCardVariant(title: "Танцы", instruction: "Танцуем вместе", estimatedMinutes: 10),
            fallback: ChallengeCardVariant(title: "Пение", instruction: "Поём вместе", estimatedMinutes: 10),
            boundaryTags: [tagToExclude]
        )
        let cardClean = ChallengeCardDefinition(
            id: "card_clean",
            stepIndex: 1,
            primary: ChallengeCardVariant(title: "Разговор", instruction: "Говорим по душам", estimatedMinutes: 10),
            fallback: ChallengeCardVariant(title: "Чай", instruction: "Пьём чай", estimatedMinutes: 10),
            boundaryTags: []
        )

        let advWithBoundary = AdventureDefinition(
            id: "adv_boundary",
            chapterId: "ch_01",
            orderInChapter: 1,
            title: "Музыкальный вечер",
            subtitle: "Танцы",
            preparationNote: "Музыка",
            vibes: [.playful],
            duration: .medium,
            budget: .free,
            location: .home,
            cards: [cardWithBoundary]
        )

        let advClean = AdventureDefinition(
            id: "adv_clean",
            chapterId: "ch_01",
            orderInChapter: 2,
            title: "Тихий вечер",
            subtitle: "Уют",
            preparationNote: "Плед",
            vibes: [.cozy],
            duration: .medium,
            budget: .free,
            location: .home,
            cards: [cardClean]
        )

        let pool = [advWithBoundary, advClean]

        // Запрашиваем игривый вайб (совпадает с advWithBoundary), но исключаем dancingOrSinging
        let criteria = VibeCriteria(
            vibes: [.playful],
            duration: .medium,
            budget: .free,
            location: .home
        )

        let result = matcher.matchAdventure(
            criteria: criteria,
            adventures: pool,
            completedIds: [],
            excludedBoundaries: [tagToExclude],
            currentChapterId: "ch_01"
        )

        // advWithBoundary отсекается жёстким фильтром, поэтому выбирается advClean
        XCTAssertEqual(result.adventure.id, advClean.id, "Квест с исключённым boundary tag не должен выбираться")
        XCTAssertFalse(
            result.adventure.cards.flatMap(\.boundaryTags).contains(tagToExclude),
            "В выбранном приключении не должно быть исключённого тега"
        )

        // Дополнительно: проверка на реальном каталоге
        let catalogResults = matcher.matchAdventure(
            criteria: VibeCriteria(vibes: [.playful], location: .any),
            adventures: allAdventures,
            completedIds: [],
            excludedBoundaries: [.dancingOrSinging, .strangersInteraction],
            currentChapterId: "ch_01"
        )
        for card in catalogResults.adventure.cards {
            XCTAssertFalse(card.boundaryTags.contains(.dancingOrSinging))
            XCTAssertFalse(card.boundaryTags.contains(.strangersInteraction))
        }
    }

    // MARK: - 3. Location Hard Filter

    /// Выбор "Дома" строго исключает локацию "Вне дома" (и наоборот).
    func testLocationHardFilter() {
        let homeAdv = AdventureDefinition(
            id: "adv_home",
            chapterId: "ch_01",
            orderInChapter: 1,
            title: "Домашнее тепло",
            subtitle: "Дома",
            preparationNote: "",
            vibes: [.romantic],
            duration: .medium,
            budget: .free,
            location: .home,
            cards: []
        )
        let outsideAdv = AdventureDefinition(
            id: "adv_outside",
            chapterId: "ch_01",
            orderInChapter: 2,
            title: "Уличная прогулка",
            subtitle: "Вне дома",
            preparationNote: "",
            vibes: [.romantic],
            duration: .medium,
            budget: .free,
            location: .outside,
            cards: []
        )
        let anyAdv = AdventureDefinition(
            id: "adv_any",
            chapterId: "ch_01",
            orderInChapter: 3,
            title: "Универсальный вечер",
            subtitle: "Неважно",
            preparationNote: "",
            vibes: [.romantic],
            duration: .medium,
            budget: .free,
            location: .any,
            cards: []
        )

        let pool = [homeAdv, outsideAdv, anyAdv]

        // 1. Запрос локации «Дома» (.home)
        let homeCriteria = VibeCriteria(vibes: [.romantic], location: .home)
        let homeResult = matcher.matchAdventure(
            criteria: homeCriteria,
            adventures: pool,
            completedIds: [],
            excludedBoundaries: [],
            currentChapterId: "ch_01"
        )
        XCTAssertNotEqual(homeResult.adventure.location, .outside, "Выбор 'Дома' строго исключает 'Вне дома'")
        XCTAssertTrue(homeResult.adventure.location == .home || homeResult.adventure.location == .any)

        // 2. Запрос локации «Вне дома» (.outside)
        let outsideCriteria = VibeCriteria(vibes: [.romantic], location: .outside)
        let outsideResult = matcher.matchAdventure(
            criteria: outsideCriteria,
            adventures: pool,
            completedIds: [],
            excludedBoundaries: [],
            currentChapterId: "ch_01"
        )
        XCTAssertNotEqual(outsideResult.adventure.location, .home, "Выбор 'Вне дома' строго исключает 'Дома'")
        XCTAssertTrue(outsideResult.adventure.location == .outside || outsideResult.adventure.location == .any)

        // 3. Проверка на реальном каталоге
        let realHomeResult = matcher.matchAdventure(
            criteria: VibeCriteria(vibes: [], location: .home),
            adventures: allAdventures,
            completedIds: [],
            excludedBoundaries: [],
            currentChapterId: "ch_01"
        )
        XCTAssertNotEqual(realHomeResult.adventure.location, .outside)

        let realOutsideResult = matcher.matchAdventure(
            criteria: VibeCriteria(vibes: [], location: .outside),
            adventures: allAdventures,
            completedIds: [],
            excludedBoundaries: [],
            currentChapterId: "ch_01"
        )
        XCTAssertNotEqual(realOutsideResult.adventure.location, .home)
    }

    // MARK: - 4. Free Budget Hard Filter

    /// Выбор бюджета 0 ₸ (.free) исключает приключения с бюджетом > 0.
    func testFreeBudgetHardFilter() {
        let freeAdv = AdventureDefinition(
            id: "adv_free",
            chapterId: "ch_01",
            orderInChapter: 1,
            title: "Бесплатное тепло",
            subtitle: "0 тенге",
            preparationNote: "",
            vibes: [.cozy],
            duration: .short,
            budget: .free,
            location: .home,
            cards: []
        )
        let expensiveAdv = AdventureDefinition(
            id: "adv_expensive",
            chapterId: "ch_01",
            orderInChapter: 2,
            title: "Шикарный ужин",
            subtitle: "5000 тенге",
            preparationNote: "",
            vibes: [.cozy], // Идеальное совпадение вайба
            duration: .short,
            budget: .medium, // 5 000 ₸
            location: .home,
            cards: []
        )

        let pool = [expensiveAdv, freeAdv]

        let criteria = VibeCriteria(
            vibes: [.cozy],
            duration: .short,
            budget: .free,
            location: .home
        )

        let result = matcher.matchAdventure(
            criteria: criteria,
            adventures: pool,
            completedIds: [],
            excludedBoundaries: [],
            currentChapterId: "ch_01"
        )

        XCTAssertEqual(result.adventure.id, freeAdv.id, "При бюджете 0 ₸ платный квест должен быть исключён")
        XCTAssertEqual(result.adventure.budget, .free)
        XCTAssertEqual(result.adventure.budget.rawValue, 0)

        // Проверка на реальном каталоге
        let realFreeResult = matcher.matchAdventure(
            criteria: VibeCriteria(vibes: [.romantic], budget: .free, location: .any),
            adventures: allAdventures,
            completedIds: [],
            excludedBoundaries: [],
            currentChapterId: "ch_01"
        )
        XCTAssertEqual(realFreeResult.adventure.budget.rawValue, 0, "Каталожный квест при бюджете .free обязан иметь 0 ₸")
    }

    // MARK: - 5. Soft Scoring Weights Verification

    /// Проверка весов мягкого скоринга:
    /// +50 основной вайб
    /// +25 вторичный вайб
    /// +20 точное время (+10 соседнее время)
    /// +15 попадание в бюджет
    /// +10 текущая открытая глава
    func testSoftScoringWeights() {
        // Базовый квест для калибровки весов
        let testAdv = AdventureDefinition(
            id: "adv_target",
            chapterId: "ch_target",
            orderInChapter: 1,
            title: "Тестовое приключение",
            subtitle: "",
            preparationNote: "",
            vibes: [.romantic, .cozy], // primary = romantic, secondary = cozy
            duration: .medium,         // 30–60 мин
            budget: .low,              // 2 000 ₸
            location: .home,
            cards: []
        )

        let pool = [testAdv]

        // 1. Только основной вайб (+50)
        let primaryCriteria = VibeCriteria(vibes: [.romantic])
        let primaryResult = matcher.matchAdventure(
            criteria: primaryCriteria,
            adventures: pool,
            completedIds: [],
            excludedBoundaries: [],
            currentChapterId: "ch_other"
        )
        XCTAssertEqual(primaryResult.score, 50, "Совпадение по первичному вайбу должно давать ровно +50")

        // 2. Только вторичный вайб (+25)
        let secondaryCriteria = VibeCriteria(vibes: [.cozy])
        let secondaryResult = matcher.matchAdventure(
            criteria: secondaryCriteria,
            adventures: pool,
            completedIds: [],
            excludedBoundaries: [],
            currentChapterId: "ch_other"
        )
        XCTAssertEqual(secondaryResult.score, 25, "Совпадение по вторичному вайбу должно давать ровно +25")

        // 3. Оба вайба (+50 + 25 = +75)
        let bothVibesCriteria = VibeCriteria(vibes: [.romantic, .cozy])
        let bothVibesResult = matcher.matchAdventure(
            criteria: bothVibesCriteria,
            adventures: pool,
            completedIds: [],
            excludedBoundaries: [],
            currentChapterId: "ch_other"
        )
        XCTAssertEqual(bothVibesResult.score, 75, "Совпадение по обоим вайбам должно давать +75")

        // 4. Точное совпадение времени (+20) -> 75 + 20 = 95
        let exactDurationCriteria = VibeCriteria(vibes: [.romantic, .cozy], duration: .medium)
        let exactDurationResult = matcher.matchAdventure(
            criteria: exactDurationCriteria,
            adventures: pool,
            completedIds: [],
            excludedBoundaries: [],
            currentChapterId: "ch_other"
        )
        XCTAssertEqual(exactDurationResult.score, 95, "Точное совпадение времени добавляет +20 (итого 95)")

        // 5. Соседнее время (+10) -> 75 + 10 = 85 (.short соседствует с .medium)
        let adjacentDurationCriteria = VibeCriteria(vibes: [.romantic, .cozy], duration: .short)
        let adjacentDurationResult = matcher.matchAdventure(
            criteria: adjacentDurationCriteria,
            adventures: pool,
            completedIds: [],
            excludedBoundaries: [],
            currentChapterId: "ch_other"
        )
        XCTAssertEqual(adjacentDurationResult.score, 85, "Соседний интервал времени добавляет +10 (итого 85)")

        // 6. Попадание в бюджет (+15) -> 95 + 15 = 110 (бюджет квеста .low <= выбранного .medium)
        let budgetCriteria = VibeCriteria(vibes: [.romantic, .cozy], duration: .medium, budget: .medium)
        let budgetResult = matcher.matchAdventure(
            criteria: budgetCriteria,
            adventures: pool,
            completedIds: [],
            excludedBoundaries: [],
            currentChapterId: "ch_other"
        )
        XCTAssertEqual(budgetResult.score, 110, "Попадание в бюджет добавляет +15 (итого 110)")

        // 7. Принадлежность к текущей главе (+10) -> 110 + 10 = 120 (максимальный скор)
        let fullCriteria = VibeCriteria(vibes: [.romantic, .cozy], duration: .medium, budget: .medium)
        let fullResult = matcher.matchAdventure(
            criteria: fullCriteria,
            adventures: pool,
            completedIds: [],
            excludedBoundaries: [],
            currentChapterId: "ch_target" // совпадает с chapterId квеста
        )
        XCTAssertEqual(fullResult.score, 120, "Максимальный скор со всеми совпадениями должен быть ровно 120 (50+25+20+15+10)")
    }

    // MARK: - 6. Fallback Match Flag

    /// Если точного совпадения нет, возвращается лучший доступный вариант с флагом isFallbackMatch == true.
    func testFallbackMatchFlag() {
        let advPlayful = AdventureDefinition(
            id: "adv_playful",
            chapterId: "ch_01",
            orderInChapter: 1,
            title: "Игривый квест",
            subtitle: "",
            preparationNote: "",
            vibes: [.playful],
            duration: .short,
            budget: .free,
            location: .home,
            cards: []
        )
        let advCozy = AdventureDefinition(
            id: "adv_cozy",
            chapterId: "ch_01",
            orderInChapter: 2,
            title: "Уютный квест",
            subtitle: "",
            preparationNote: "",
            vibes: [.cozy],
            duration: .short,
            budget: .free,
            location: .home,
            cards: []
        )

        let pool = [advPlayful, advCozy]

        // 1. Точное совпадение: запрашиваем .playful, advPlayful непройден -> isFallbackMatch == false
        let exactCriteria = VibeCriteria(vibes: [.playful], location: .home)
        let exactResult = matcher.matchAdventure(
            criteria: exactCriteria,
            adventures: pool,
            completedIds: [],
            excludedBoundaries: [],
            currentChapterId: "ch_01"
        )
        XCTAssertEqual(exactResult.adventure.id, advPlayful.id)
        XCTAssertFalse(exactResult.isFallbackMatch, "При идеальном совпадении isFallbackMatch обязан быть false")

        // 2. Фолбэк: advPlayful уже завершён, остаётся только advCozy (вайб не совпадает)
        let fallbackResult = matcher.matchAdventure(
            criteria: exactCriteria,
            adventures: pool,
            completedIds: [advPlayful.id], // advPlayful пройден
            excludedBoundaries: [],
            currentChapterId: "ch_01"
        )
        XCTAssertEqual(fallbackResult.adventure.id, advCozy.id)
        XCTAssertTrue(fallbackResult.isFallbackMatch, "При отсутствии непройденного совпадения по вайбу должен быть fallback (true)")

        // 3. Фолбэк по отсутствию вайба в каталоге
        let deepCriteria = VibeCriteria(vibes: [.deep], location: .home)
        let deepResult = matcher.matchAdventure(
            criteria: deepCriteria,
            adventures: pool,
            completedIds: [],
            excludedBoundaries: [],
            currentChapterId: "ch_01"
        )
        XCTAssertTrue(deepResult.isFallbackMatch, "Если запрашиваемый вайб отсутствует в пуле, должен быть fallback (true)")

        // 4. Фолбэк при завершении абсолютно всех квестов
        let allCompletedResult = matcher.matchAdventure(
            criteria: exactCriteria,
            adventures: pool,
            completedIds: [advPlayful.id, advCozy.id],
            excludedBoundaries: [],
            currentChapterId: "ch_01"
        )
        XCTAssertTrue(allCompletedResult.isFallbackMatch, "При повторном прохождении всех завершённых квестов должен быть fallback (true)")
    }

    // MARK: - Вспомогательная загрузка тестовых данных

    private func loadTestAdventures() -> [AdventureDefinition] {
        if let container = try? TestCatalogHelper.loadCatalog() {
            return container.adventures
        }
        // Запасной генератор при изолированном запуске без бандла
        return (1...12).map { i in
            AdventureDefinition(
                id: "adv_\(String(format: "%02d", i))",
                chapterId: "ch_\(String(format: "%02d", ((i - 1) / 4) + 1))",
                orderInChapter: ((i - 1) % 4) + 1,
                title: "Приключение #\(i)",
                subtitle: "Подзаголовок #\(i)",
                preparationNote: "",
                vibes: [VibeTag.allCases[(i - 1) % VibeTag.allCases.count]],
                duration: DurationTier.allCases[(i - 1) % DurationTier.allCases.count],
                budget: (i % 3 == 0) ? .medium : (i % 2 == 0 ? .low : .free),
                location: (i % 2 == 0) ? .outside : .home,
                cards: []
            )
        }
    }
}

// MARK: - TestCatalogHelper

enum TestCatalogHelper {
    static func loadCatalog() throws -> CatalogContainer {
        let fileName = "adventures_catalog_ru"
        let fileExtension = "json"

        #if SWIFT_PACKAGE
        if let moduleUrl = Bundle.module.url(forResource: fileName, withExtension: fileExtension),
           let data = try? Data(contentsOf: moduleUrl) {
            return try JSONDecoder().decode(CatalogContainer.self, from: data)
        }
        #endif

        if let bundleUrl = Bundle.main.url(forResource: fileName, withExtension: fileExtension),
           let data = try? Data(contentsOf: bundleUrl) {
            return try JSONDecoder().decode(CatalogContainer.self, from: data)
        }

        if let envPath = ProcessInfo.processInfo.environment["CHOOSE_A_VIBE_CATALOG_PATH"],
           FileManager.default.fileExists(atPath: envPath),
           let data = try? Data(contentsOf: URL(fileURLWithPath: envPath)) {
            return try JSONDecoder().decode(CatalogContainer.self, from: data)
        }

        let candidatePaths = [
            "Resources/\(fileName).\(fileExtension)",
            "ChooseAVibe/Resources/\(fileName).\(fileExtension)",
            "../Resources/\(fileName).\(fileExtension)",
            "../../Resources/\(fileName).\(fileExtension)",
            "../../../ChooseAVibe/Resources/\(fileName).\(fileExtension)",
            "C:/Users/Mila/Desktop/BestStart/projects/ChooseaVibe/ChooseAVibe/Resources/\(fileName).\(fileExtension)"
        ]

        for path in candidatePaths {
            if FileManager.default.fileExists(atPath: path),
               let data = try? Data(contentsOf: URL(fileURLWithPath: path)) {
                return try JSONDecoder().decode(CatalogContainer.self, from: data)
            }
        }

        throw NSError(
            domain: "TestCatalogHelper",
            code: 404,
            userInfo: [NSLocalizedDescriptionKey: "adventures_catalog_ru.json не найден"]
        )
    }
}
