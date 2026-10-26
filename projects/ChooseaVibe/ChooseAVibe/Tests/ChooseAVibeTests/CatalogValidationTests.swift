//
//  CatalogValidationTests.swift
//  ChooseAVibeTests
//
//  Набор модульных тестов для валидации целостности каталога приключений (adventures_catalog_ru.json).
//  Проверяет: десериализацию JSON, ровно 12 квестов в 3 главах, 48 карточек (primary + fallback),
//  бюджетные тиры в тенге (0, 2000, 5000 ₸) и 8 значков достижений по 20 XP.
//

import XCTest
#if canImport(ChooseAVibeCore)
@testable import ChooseAVibeCore
#endif
#if canImport(ChooseAVibe)
@testable import ChooseAVibe
#endif

final class CatalogValidationTests: XCTestCase {

    private var catalogContainer: CatalogContainer!

    override func setUpWithError() throws {
        try super.setUpWithError()
        catalogContainer = try loadCatalogContainer()
    }

    override func tearDown() {
        catalogContainer = nil
        super.tearDown()
    }

    // MARK: - 1. Catalog Loads Successfully

    /// Проверка успешной десериализации JSON каталога (без исключений и повреждений данных).
    func testCatalogLoadsSuccessfully() throws {
        XCTAssertNotNil(catalogContainer, "Контейнер каталога не должен быть nil после загрузки")
        XCTAssertFalse(catalogContainer.chapters.isEmpty, "Список глав в каталоге не должен быть пустым")
        XCTAssertFalse(catalogContainer.adventures.isEmpty, "Список приключений в каталоге не должен быть пустым")
        XCTAssertFalse(catalogContainer.achievements.isEmpty, "Список достижений в каталоге не должен быть пустым")

        // Проверка через CatalogRepository
        let repo = CatalogRepository(autoLoad: false)
        XCTAssertNoThrow(try repo.loadCatalog(), "CatalogRepository должен успешно парсить каталог")
        XCTAssertTrue(repo.isLoaded, "Флаг isLoaded репозитория должен быть true")
        XCTAssertNil(repo.lastError, "В репозитории не должно быть ошибок парсинга")
        XCTAssertEqual(repo.getAllAdventures().count, 12, "Репозиторий должен содержать ровно 12 приключений")
        XCTAssertEqual(repo.getAllChapters().count, 3, "Репозиторий должен содержать ровно 3 главы")
        XCTAssertEqual(repo.getAllAchievements().count, 8, "Репозиторий должен содержать ровно 8 значков")
    }

    // MARK: - 2. Exactly 12 Adventures Across 3 Chapters

    /// Ровно 12 приключений, разбитых на 3 главы по 4 квеста (порядок 1...4 без пропусков).
    func testExactly12AdventuresAcross3Chapters() {
        let adventures = catalogContainer.adventures
        let chapters = catalogContainer.chapters

        // Ровно 12 приключений
        XCTAssertEqual(adventures.count, 12, "Каталог MVP обязан содержать ровно 12 приключений")

        // Ровно 3 главы
        XCTAssertEqual(chapters.count, 3, "Каталог MVP обязан содержать ровно 3 главы")

        // Уникальность идентификаторов квестов
        let adventureIds = adventures.map(\.id)
        let uniqueAdventureIds = Set(adventureIds)
        XCTAssertEqual(uniqueAdventureIds.count, 12, "Все ID приключений должны быть уникальными")

        // Главы упорядочены 1, 2, 3
        let sortedChapters = chapters.sorted { $0.order < $1.order }
        XCTAssertEqual(sortedChapters.map(\.order), [1, 2, 3], "Главы должны иметь номера 1, 2, 3")

        let expectedChapterIds = ["ch_01", "ch_02", "ch_03"]
        XCTAssertEqual(sortedChapters.map(\.id), expectedChapterIds, "ID глав должны быть ch_01, ch_02, ch_03")

        // В каждой главе ровно 4 приключения
        for chapter in sortedChapters {
            let chapterAdventures = adventures.filter { $0.chapterId == chapter.id }
            XCTAssertEqual(
                chapterAdventures.count,
                4,
                "Глава \(chapter.id) («\(chapter.title)») должна содержать ровно 4 приключения"
            )

            // Проверка порядковых номеров внутри главы: [1, 2, 3, 4]
            let orders = chapterAdventures.map(\.orderInChapter).sorted()
            XCTAssertEqual(
                orders,
                [1, 2, 3, 4],
                "В главе \(chapter.id) номера квестов должны строго идти от 1 до 4"
            )

            // Поле adventureIds в определении главы должно совпадать с фактическими квестами
            XCTAssertEqual(
                chapter.adventureIds.count,
                4,
                "Массив adventureIds главы \(chapter.id) должен содержать 4 элемента"
            )
            let actualIds = Set(chapterAdventures.map(\.id))
            let declaredIds = Set(chapter.adventureIds)
            XCTAssertEqual(
                actualIds,
                declaredIds,
                "Объявленные в главе квесты должны строго совпадать с объектами каталога"
            )

            // Требования открытия глав
            if chapter.order == 1 {
                XCTAssertEqual(chapter.requiredCompletedCount, 0, "Глава 1 должна открываться бесплатно (0 квестов)")
            } else {
                XCTAssertEqual(chapter.requiredCompletedCount, 3, "Главы 2 и 3 требуют по 3 пройденных квеста")
            }
        }
    }

    // MARK: - 3. All Cards Have Valid Primary and Fallback Variants

    /// Ровно 4 карточки на квест (всего 48 карточек), у каждой есть непустые title,
    /// instruction и estimatedMinutes как для primary, так и для fallback.
    func testAllCardsHaveValidPrimaryAndFallback() {
        let adventures = catalogContainer.adventures
        var totalCardsCount = 0
        var allCardIds: Set<String> = []

        for adventure in adventures {
            XCTAssertEqual(
                adventure.cards.count,
                4,
                "Приключение \(adventure.id) («\(adventure.title)») должно содержать ровно 4 карточки"
            )

            // Проверка шагов 1...4
            let stepIndices = adventure.cards.map(\.stepIndex).sorted()
            XCTAssertEqual(
                stepIndices,
                [1, 2, 3, 4],
                "Карточки приключения \(adventure.id) должны иметь stepIndex от 1 до 4"
            )

            for card in adventure.cards {
                totalCardsCount += 1

                // Уникальность ID карточки
                XCTAssertFalse(card.id.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                XCTAssertFalse(
                    allCardIds.contains(card.id),
                    "ID карточки \(card.id) дублируется в каталоге"
                )
                allCardIds.insert(card.id)

                // 1. Валидация Primary-карточки
                let primary = card.primary
                let cleanPrimaryTitle = primary.title.trimmingCharacters(in: .whitespacesAndNewlines)
                let cleanPrimaryInstruction = primary.instruction.trimmingCharacters(in: .whitespacesAndNewlines)

                XCTAssertFalse(
                    cleanPrimaryTitle.isEmpty,
                    "Карточка \(card.id) содержит пустой title у primary"
                )
                XCTAssertFalse(
                    cleanPrimaryInstruction.isEmpty,
                    "Карточка \(card.id) содержит пустую instruction у primary"
                )
                XCTAssertGreaterThan(
                    primary.estimatedMinutes,
                    0,
                    "Карточка \(card.id) primary estimatedMinutes должно быть > 0"
                )

                // 2. Валидация Fallback-карточки
                let fallback = card.fallback
                let cleanFallbackTitle = fallback.title.trimmingCharacters(in: .whitespacesAndNewlines)
                let cleanFallbackInstruction = fallback.instruction.trimmingCharacters(in: .whitespacesAndNewlines)

                XCTAssertFalse(
                    cleanFallbackTitle.isEmpty,
                    "Карточка \(card.id) содержит пустой title у fallback"
                )
                XCTAssertFalse(
                    cleanFallbackInstruction.isEmpty,
                    "Карточка \(card.id) содержит пустую instruction у fallback"
                )
                XCTAssertGreaterThan(
                    fallback.estimatedMinutes,
                    0,
                    "Карточка \(card.id) fallback estimatedMinutes должно быть > 0"
                )

                // 3. Fallback и Primary не должны быть идентичными (реальная альтернатива)
                XCTAssertNotEqual(
                    cleanPrimaryTitle,
                    cleanFallbackTitle,
                    "Карточка \(card.id): fallback заголовок совпадает с primary"
                )

                // 4. Проверка метода variant(useFallback:)
                XCTAssertEqual(card.variant(useFallback: false).title, primary.title)
                XCTAssertEqual(card.variant(useFallback: true).title, fallback.title)
            }
        }

        // Ровно 48 карточек (12 * 4)
        XCTAssertEqual(
            totalCardsCount,
            48,
            "Суммарное количество карточек во всём каталоге должно быть ровно 48"
        )
        XCTAssertEqual(
            allCardIds.count,
            48,
            "Все 48 карточек обязаны иметь уникальные идентификаторы"
        )
    }

    // MARK: - 4. Budgets Are in Valid KZ Tiers

    /// Все бюджеты соответствуют 0, 2000, 5000 ₸.
    func testBudgetsAreInKZTiers() {
        let adventures = catalogContainer.adventures
        let validKZTiers: Set<Int> = [0, 2000, 5000]

        for adventure in adventures {
            let budgetValue = adventure.budget.rawValue
            XCTAssertTrue(
                validKZTiers.contains(budgetValue),
                "Приключение \(adventure.id) имеет некорректный бюджет \(budgetValue) ₸. Допустимы только 0, 2000, 5000 ₸."
            )

            // Проверка enum соответствия
            switch adventure.budget {
            case .free:
                XCTAssertEqual(budgetValue, 0)
                XCTAssertEqual(adventure.budget.shortTitle, "0 ₸")
            case .low:
                XCTAssertEqual(budgetValue, 2000)
                XCTAssertEqual(adventure.budget.shortTitle, "2 000 ₸")
            case .medium:
                XCTAssertEqual(budgetValue, 5000)
                XCTAssertEqual(adventure.budget.shortTitle, "5 000 ₸")
            case .unlimited:
                XCTFail("В MVP каталоге не должно быть безлимитных квестов (.unlimited)")
            }
        }
    }

    // MARK: - 5. All 8 MVP Achievements Present

    /// В каталоге присутствуют все 8 значков MVP с наградой 20 XP.
    func testAll8AchievementsPresent() {
        let achievements = catalogContainer.achievements

        // Ровно 8 значков
        XCTAssertEqual(achievements.count, 8, "В каталоге должно быть ровно 8 значков достижений")

        let expectedAchievementIds: Set<String> = [
            "first_step",
            "cozy_masters",
            "wind_of_change",
            "pure_improvisation",
            "priceless_evening",
            "no_secrets",
            "chapter_closed",
            "perfect_wave"
        ]

        let actualAchievementIds = Set(achievements.map(\.id))
        XCTAssertEqual(
            actualAchievementIds,
            expectedAchievementIds,
            "Каталог должен содержать точный список 8 MVP значков"
        )

        for achievement in achievements {
            // Награда строго 20 XP
            XCTAssertEqual(
                achievement.bonusXP,
                20,
                "Значок \(achievement.id) обязан приносить ровно +20 XP"
            )
            XCTAssertEqual(
                achievement.xpReward,
                20,
                "Алиас xpReward значка \(achievement.id) обязан быть 20 XP"
            )

            // Непустые поля заголовка, описания и системной иконки
            let cleanTitle = achievement.title.trimmingCharacters(in: .whitespacesAndNewlines)
            let cleanDescription = achievement.description.trimmingCharacters(in: .whitespacesAndNewlines)
            let cleanIcon = achievement.icon.trimmingCharacters(in: .whitespacesAndNewlines)

            XCTAssertFalse(cleanTitle.isEmpty, "Значок \(achievement.id) имеет пустой title")
            XCTAssertFalse(cleanDescription.isEmpty, "Значок \(achievement.id) имеет пустое description")
            XCTAssertFalse(cleanIcon.isEmpty, "Значок \(achievement.id) имеет пустой icon (SF Symbol)")
        }
    }

    // MARK: - Вспомогательный метод загрузки данных

    private func loadCatalogContainer() throws -> CatalogContainer {
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
            domain: "CatalogValidationTests",
            code: 404,
            userInfo: [NSLocalizedDescriptionKey: "Файл каталога adventures_catalog_ru.json не найден"]
        )
    }
}
