//
//  ProgressionEngineTests.swift
//  ChooseAVibeTests
//
//  Набор модульных тестов для движка прогрессии (ProgressionEngine).
//  Проверяет: идемпотентность начисления XP, реплей-коэффициент, пропуск этапов,
//  бонус финала, 5 уровней, правила разблокировки глав и 8 значков достижений.
//

import XCTest
#if canImport(ChooseAVibeCore)
@testable import ChooseAVibeCore
#endif
#if canImport(ChooseAVibe)
@testable import ChooseAVibe
#endif

final class ProgressionEngineTests: XCTestCase {

    private var engine: ProgressionEngine!

    override func setUp() {
        super.setUp()
        engine = ProgressionEngine.shared
    }

    override func tearDown() {
        engine = nil
        super.tearDown()
    }

    // MARK: - 1. Step Completion Idempotency

    /// Вызов completeStep повторно для того же шага возвращает 0 XP (защита от повторного начисления).
    func testStepCompletionIdempotency() {
        let session = AdventureSessionRecord(adventureId: "adv_01", stepCount: 4)

        XCTAssertEqual(session.earnedXP, 0, "Начальный опыт сессии должен быть 0 XP")
        XCTAssertEqual(session.status(for: 1), "pending", "Начальный статус 1-го шага должен быть 'pending'")

        // 1-й вызов для 1-го шага: начисление +10 XP
        let gainedXP1 = engine.completeStep(session: session, stepIndex: 1, isReplay: false)
        XCTAssertEqual(gainedXP1, 10, "Первое завершение шага должно возвращать ровно 10 XP")
        XCTAssertEqual(session.earnedXP, 10, "Суммарный заработанный XP сессии должен стать 10")
        XCTAssertEqual(session.status(for: 1), "completed", "Статус этапа должен стать 'completed'")
        XCTAssertTrue(session.isStepCompleted(at: 1))

        // Повторный (идемпотентный) вызов для того же 1-го шага: 0 XP
        let repeatedXP1 = engine.completeStep(session: session, stepIndex: 1, isReplay: false)
        XCTAssertEqual(repeatedXP1, 0, "Повторный вызов completeStep обязан возвращать 0 XP")
        XCTAssertEqual(session.earnedXP, 10, "Суммарный XP не должен измениться при повторном завершении")
        XCTAssertEqual(session.status(for: 1), "completed")

        // Завершение 2-го шага: ещё +10 XP (итого 20 XP)
        let gainedXP2 = engine.completeStep(session: session, stepIndex: 2, isReplay: false)
        XCTAssertEqual(gainedXP2, 10)
        XCTAssertEqual(session.earnedXP, 20)
        XCTAssertEqual(session.status(for: 2), "completed")

        // Повторный вызов для 2-го шага: 0 XP
        let repeatedXP2 = engine.completeStep(session: session, stepIndex: 2, isReplay: false)
        XCTAssertEqual(repeatedXP2, 0)
        XCTAssertEqual(session.earnedXP, 20)
    }

    // MARK: - 2. Step Replay 50% XP

    /// При повторном прохождении уже завершённого квеста шаг даёт +5 XP вместо +10 XP.
    func testStepReplay50PercentXP() {
        let replaySession = AdventureSessionRecord(adventureId: "adv_01", stepCount: 4)

        // 1-й шаг на реплее: +5 XP
        let step1XP = engine.completeStep(session: replaySession, stepIndex: 1, isReplay: true)
        XCTAssertEqual(step1XP, 5, "Повторное прохождение этапа должно начислять 50% опыта (+5 XP)")
        XCTAssertEqual(replaySession.earnedXP, 5)

        // 2-й шаг на реплее: ещё +5 XP
        let step2XP = engine.completeStep(session: replaySession, stepIndex: 2, isReplay: true)
        XCTAssertEqual(step2XP, 5, "Второй шаг реплея должен также давать +5 XP")
        XCTAssertEqual(replaySession.earnedXP, 10)

        // Идемпотентность на реплее: повторный клик по завершённому шагу даёт 0 XP
        let repeatedReplayXP = engine.completeStep(session: replaySession, stepIndex: 1, isReplay: true)
        XCTAssertEqual(repeatedReplayXP, 0, "Повторный вызов на реплее также обязан возвращать 0 XP")
        XCTAssertEqual(replaySession.earnedXP, 10)
    }

    // MARK: - 3. Skip Step Gives Zero XP

    /// Пропуск шага даёт 0 XP и помечает статус как "skipped".
    func testSkipStepGivesZeroXP() {
        let session = AdventureSessionRecord(adventureId: "adv_02", stepCount: 4)

        XCTAssertEqual(session.earnedXP, 0)
        XCTAssertEqual(session.status(for: 1), "pending")

        // Пропускаем 1-й этап
        engine.skipStep(session: session, stepIndex: 1)
        XCTAssertEqual(session.status(for: 1), "skipped", "Статус пропущенного шага должен быть 'skipped'")
        XCTAssertTrue(session.isStepSkipped(at: 1))
        XCTAssertEqual(session.earnedXP, 0, "За пропуск этапа начисляется строго 0 XP")

        // 2-й этап выполняем штатно (+10 XP)
        let step2XP = engine.completeStep(session: session, stepIndex: 2, isReplay: false)
        XCTAssertEqual(step2XP, 10)
        XCTAssertEqual(session.earnedXP, 10)

        // 3-й этап пропускаем
        engine.skipStep(session: session, stepIndex: 3)
        XCTAssertEqual(session.status(for: 3), "skipped")
        XCTAssertEqual(session.earnedXP, 10, "После второго пропуска суммарный XP не должен возрасти")

        // Проверяем счётчики
        XCTAssertEqual(session.completedStepsCount, 1)
        XCTAssertEqual(session.skippedStepsCount, 2)
        XCTAssertFalse(session.isAllCardsCompletedWithoutSkip)
    }

    // MARK: - 4. Adventure Finish Bonus

    /// Завершение 4-го этапа даёт +25 XP (или +15 XP при повторе), повторный вызов финала даёт 0 XP.
    func testAdventureFinishBonus() {
        // 1. Первичное прохождение (Regular)
        let session = AdventureSessionRecord(adventureId: "adv_03", stepCount: 4)
        for step in 1...4 {
            engine.completeStep(session: session, stepIndex: step, isReplay: false)
        }
        XCTAssertEqual(session.earnedXP, 40, "4 выполненных этапа дают 40 XP")
        XCTAssertFalse(session.isFinished)

        // Завершение приключения (+25 XP)
        let finishBonus = engine.finishAdventure(session: session, isReplay: false)
        XCTAssertEqual(finishBonus, 25, "Бонус за первичное завершение приключения составляет +25 XP")
        XCTAssertEqual(session.earnedXP, 65, "Итого за идеальное первичное приключение: 40 + 25 = 65 XP")
        XCTAssertTrue(session.isFinished)
        XCTAssertNotNil(session.completedAt)

        // Повторный вызов финала: идемпотентность (0 XP)
        let repeatedFinishBonus = engine.finishAdventure(session: session, isReplay: false)
        XCTAssertEqual(repeatedFinishBonus, 0, "Повторный вызов финала обязан возвращать 0 XP")
        XCTAssertEqual(session.earnedXP, 65, "XP не должен увеличиваться при повторном завершении")

        // 2. Повторное прохождение (Replay)
        let replaySession = AdventureSessionRecord(adventureId: "adv_03", stepCount: 4)
        for step in 1...4 {
            engine.completeStep(session: replaySession, stepIndex: step, isReplay: true)
        }
        XCTAssertEqual(replaySession.earnedXP, 20, "4 этапа на реплее дают 20 XP (4 * 5)")

        // Финал реплея (+15 XP)
        let replayFinishBonus = engine.finishAdventure(session: replaySession, isReplay: true)
        XCTAssertEqual(replayFinishBonus, 15, "Бонус за повторное завершение приключения составляет +15 XP")
        XCTAssertEqual(replaySession.earnedXP, 35, "Итого за реплей: 20 + 15 = 35 XP")
        XCTAssertTrue(replaySession.isFinished)

        // Повторный вызов финала на реплее: 0 XP
        let repeatedReplayFinish = engine.finishAdventure(session: replaySession, isReplay: true)
        XCTAssertEqual(repeatedReplayFinish, 0)
        XCTAssertEqual(replaySession.earnedXP, 35)
    }

    // MARK: - 5. Level Milestones Verification

    /// Проверка 5 уровней:
    /// 0 XP -> Любопытный дуэт
    /// 60 XP -> Искатели моментов
    /// 180 XP -> Сообщники по вечерам
    /// 330 XP -> Хранители историй
    /// 500 XP -> Легенды приключений
    func testLevelMilestones() {
        XCTAssertEqual(ProgressionEngine.allLevels.count, 5, "В системе должно быть ровно 5 уровней")

        // Проверка уровней и порогов
        let testCases: [(xp: Int, expectedLevel: Int, expectedTitle: String, expectedTheme: String)] = [
            (0, 1, "Любопытный дуэт", "classic_silver"),
            (30, 1, "Любопытный дуэт", "classic_silver"),
            (59, 1, "Любопытный дуэт", "classic_silver"),
            (60, 2, "Искатели моментов", "cozy_kraft"),
            (120, 2, "Искатели моментов", "cozy_kraft"),
            (179, 2, "Искатели моментов", "cozy_kraft"),
            (180, 3, "Сообщники по вечерам", "rose_gold"),
            (250, 3, "Сообщники по вечерам", "rose_gold"),
            (329, 3, "Сообщники по вечерам", "rose_gold"),
            (330, 4, "Хранители историй", "starry_midnight"),
            (400, 4, "Хранители историй", "starry_midnight"),
            (499, 4, "Хранители историй", "starry_midnight"),
            (500, 5, "Легенды приключений", "shining_holo"),
            (750, 5, "Легенды приключений", "shining_holo"),
            (1000, 5, "Легенды приключений", "shining_holo")
        ]

        for tc in testCases {
            let level = ProgressionEngine.currentLevel(for: tc.xp)
            XCTAssertEqual(
                level.levelNumber,
                tc.expectedLevel,
                "Для \(tc.xp) XP ожидается уровень \(tc.expectedLevel), получен \(level.levelNumber)"
            )
            XCTAssertEqual(
                level.title,
                tc.expectedTitle,
                "Для \(tc.xp) XP ожидается титул '\(tc.expectedTitle)'"
            )
            XCTAssertEqual(
                level.scratchThemeId,
                tc.expectedTheme,
                "Для \(tc.xp) XP ожидается рубашка '\(tc.expectedTheme)'"
            )

            // Проверка через инстанс-метод
            let instanceLevel = engine.level(for: tc.xp)
            XCTAssertEqual(instanceLevel, level)

            // Проверка через LevelMilestone в ProgressModels
            let milestone = LevelMilestone.milestone(for: tc.xp)
            XCTAssertEqual(milestone.level, tc.expectedLevel)
            XCTAssertEqual(milestone.title, tc.expectedTitle)
        }

        // Проверка расчёта следующего уровня и остатка XP
        // При 0 XP: следующий 2 уровень (60 XP), осталось 60 XP
        XCTAssertEqual(ProgressionEngine.nextLevelThreshold(for: 0), 60)
        XCTAssertEqual(ProgressionEngine.xpToNextLevel(for: 0), 60)
        XCTAssertEqual(ProgressionEngine.progressToNextLevel(for: 0), 0.0, accuracy: 0.001)

        // При 30 XP (половина пути от 0 до 60): прогресс 0.5
        XCTAssertEqual(ProgressionEngine.xpToNextLevel(for: 30), 30)
        XCTAssertEqual(ProgressionEngine.progressToNextLevel(for: 30), 0.5, accuracy: 0.001)

        // При 60 XP: следующий 3 уровень (180 XP), осталось 120 XP
        XCTAssertEqual(ProgressionEngine.nextLevelThreshold(for: 60), 180)
        XCTAssertEqual(ProgressionEngine.xpToNextLevel(for: 60), 120)
        XCTAssertEqual(ProgressionEngine.progressToNextLevel(for: 60), 0.0, accuracy: 0.001)

        // При 500 XP (максимальный уровень): следующего нет
        XCTAssertNil(ProgressionEngine.nextLevel(for: 500))
        XCTAssertNil(ProgressionEngine.nextLevelThreshold(for: 500))
        XCTAssertEqual(ProgressionEngine.xpToNextLevel(for: 500), 0)
        XCTAssertEqual(ProgressionEngine.progressToNextLevel(for: 500), 1.0, accuracy: 0.001)
    }

    // MARK: - 6. Chapter Unlocking Logic

    /// Глава 1 открыта сразу; Глава 2 открывается после 3 квестов Главы 1; Глава 3 — после 3 квестов Главы 2.
    func testChapterUnlockingLogic() {
        // Создаём пул приключений на 3 главы по 4 квеста
        let mockAdventures: [AdventureDefinition] = [
            // Глава 1: adv_01...adv_04
            createMockAdventure(id: "adv_01", chapterId: "ch_01"),
            createMockAdventure(id: "adv_02", chapterId: "ch_01"),
            createMockAdventure(id: "adv_03", chapterId: "ch_01"),
            createMockAdventure(id: "adv_04", chapterId: "ch_01"),
            // Глава 2: adv_05...adv_08
            createMockAdventure(id: "adv_05", chapterId: "ch_02"),
            createMockAdventure(id: "adv_06", chapterId: "ch_02"),
            createMockAdventure(id: "adv_07", chapterId: "ch_02"),
            createMockAdventure(id: "adv_08", chapterId: "ch_02"),
            // Глава 3: adv_09...adv_12
            createMockAdventure(id: "adv_09", chapterId: "ch_03"),
            createMockAdventure(id: "adv_10", chapterId: "ch_03"),
            createMockAdventure(id: "adv_11", chapterId: "ch_03"),
            createMockAdventure(id: "adv_12", chapterId: "ch_03")
        ]

        // 1. Старт: ни одного квеста не пройдено
        var completed: Set<String> = []
        XCTAssertTrue(
            ProgressionEngine.isChapterUnlocked(chapterNumber: 1, completedAdventureIds: completed, allAdventures: mockAdventures),
            "Глава 1 должна быть открыта сразу"
        )
        XCTAssertFalse(
            ProgressionEngine.isChapterUnlocked(chapterNumber: 2, completedAdventureIds: completed, allAdventures: mockAdventures),
            "Глава 2 должна быть заблокирована на старте"
        )
        XCTAssertFalse(
            ProgressionEngine.isChapterUnlocked(chapterNumber: 3, completedAdventureIds: completed, allAdventures: mockAdventures),
            "Глава 3 должна быть заблокирована на старте"
        )

        // 2. Пройдено 1 приключение из Главы 1 -> Глава 2 всё ещё закрыта
        completed.insert("adv_01")
        XCTAssertFalse(
            ProgressionEngine.isChapterUnlocked(chapterNumber: 2, completedAdventureIds: completed, allAdventures: mockAdventures),
            "Глава 2 закрыта после 1 квеста Главы 1"
        )

        // 3. Пройдено 2 приключения из Главы 1 -> Глава 2 всё ещё закрыта
        completed.insert("adv_02")
        XCTAssertFalse(
            ProgressionEngine.isChapterUnlocked(chapterNumber: 2, completedAdventureIds: completed, allAdventures: mockAdventures),
            "Глава 2 закрыта после 2 квестов Главы 1"
        )

        // 4. Пройдено 3 приключения из Главы 1 -> Глава 2 РАЗБЛОКИРОВАНА, Глава 3 закрыта
        completed.insert("adv_03")
        XCTAssertTrue(
            ProgressionEngine.isChapterUnlocked(chapterNumber: 2, completedAdventureIds: completed, allAdventures: mockAdventures),
            "Глава 2 должна открыться после 3 пройденных квестов Главы 1"
        )
        XCTAssertFalse(
            ProgressionEngine.isChapterUnlocked(chapterNumber: 3, completedAdventureIds: completed, allAdventures: mockAdventures),
            "Глава 3 всё ещё закрыта"
        )

        // 5. Пройдено 4-е приключение Главы 1 + 2 приключения Главы 2 -> Глава 3 закрыта
        completed.insert("adv_04")
        completed.insert("adv_05")
        completed.insert("adv_06")
        XCTAssertEqual(
            ProgressionEngine.completedCount(inChapterNumber: 2, completedAdventureIds: completed, allAdventures: mockAdventures),
            2
        )
        XCTAssertFalse(
            ProgressionEngine.isChapterUnlocked(chapterNumber: 3, completedAdventureIds: completed, allAdventures: mockAdventures),
            "Глава 3 закрыта после 2 квестов Главы 2"
        )

        // 6. Пройдено 3 приключения из Главы 2 -> Глава 3 РАЗБЛОКИРОВАНА
        completed.insert("adv_07")
        XCTAssertEqual(
            ProgressionEngine.completedCount(inChapterNumber: 2, completedAdventureIds: completed, allAdventures: mockAdventures),
            3
        )
        XCTAssertTrue(
            ProgressionEngine.isChapterUnlocked(chapterNumber: 3, completedAdventureIds: completed, allAdventures: mockAdventures),
            "Глава 3 должна открыться после 3 пройденных квестов Главы 2"
        )

        // Проверка метода unlockedChapterIds
        let unlockedIds = ProgressionEngine.unlockedChapterIds(completedAdventureIds: completed, allAdventures: mockAdventures)
        XCTAssertTrue(unlockedIds.contains("ch_01"))
        XCTAssertTrue(unlockedIds.contains("ch_02"))
        XCTAssertTrue(unlockedIds.contains("ch_03"))

        // Проверка строкового интерфейса
        XCTAssertTrue(ProgressionEngine.isChapterUnlocked(chapterId: "ch_01", completedAdventureIds: [], allAdventures: mockAdventures))
        XCTAssertFalse(ProgressionEngine.isChapterUnlocked(chapterId: "ch_02", completedAdventureIds: [], allAdventures: mockAdventures))
    }

    // MARK: - 7. Achievements Evaluation (8 MVP Badges)

    /// Детерминированная проверка условий 8 значков:
    /// «Первый шаг», «Мастера уюта», «Ветер перемен», «Чистая импровизация»,
    /// «Бесценный вечер», «Никаких секретов», «Глава закрыта», «Идеальная волна».
    func testAchievementsEvaluation() {
        XCTAssertEqual(ProgressionEngine.allAchievements.count, 8, "В MVP должно быть ровно 8 значков")

        // Подготавливаем тестовые определения приключений
        let homeFreeAdv = createCustomAdventure(
            id: "adv_home_free",
            chapterId: "ch_01",
            vibes: [.cozy],
            budget: .free,
            location: .home
        )
        let homeAdv2 = createCustomAdventure(
            id: "adv_home_2",
            chapterId: "ch_01",
            vibes: [.cozy],
            budget: .low,
            location: .home
        )
        let homeAdv3 = createCustomAdventure(
            id: "adv_home_3",
            chapterId: "ch_01",
            vibes: [.cozy],
            budget: .low,
            location: .home
        )
        let homeAdv4 = createCustomAdventure(
            id: "adv_home_4",
            chapterId: "ch_01",
            vibes: [.romantic],
            budget: .low,
            location: .home
        )
        let outsideAdv = createCustomAdventure(
            id: "adv_outside",
            chapterId: "ch_02",
            vibes: [.spontaneous],
            budget: .low,
            location: .outside
        )
        let deepAdv = createCustomAdventure(
            id: "adv_deep",
            chapterId: "ch_03",
            vibes: [.deep],
            budget: .low,
            location: .home
        )

        let allCatalog = [homeFreeAdv, homeAdv2, homeAdv3, homeAdv4, outsideAdv, deepAdv]

        // --- Значок 1: «Первый шаг» (first_step) ---
        let session1 = createFinishedSession(adventureId: homeFreeAdv.id, stepCount: 4, skips: 0)
        let badges1 = ProgressionEngine.checkAchievements(
            currentAdventure: homeFreeAdv,
            currentSession: session1,
            allAdventures: allCatalog,
            completedAdventureIds: [homeFreeAdv.id],
            finishedSessions: [session1]
        )
        XCTAssertTrue(badges1.contains(where: { $0.id == "first_step" }), "Должен открыться значок 'Первый шаг'")

        // --- Значок 5: «Бесценный вечер» (priceless_evening) ---
        XCTAssertTrue(
            badges1.contains(where: { $0.id == "priceless_evening" }),
            "Квест с бюджетом 0 ₸ должен открывать значок 'Бесценный вечер'"
        )

        // --- Значок 3: «Ветер перемен» (wind_of_change) ---
        let outsideSession = createFinishedSession(adventureId: outsideAdv.id, stepCount: 4, skips: 0)
        let badgesOutside = ProgressionEngine.checkAchievements(
            currentAdventure: outsideAdv,
            currentSession: outsideSession,
            allAdventures: allCatalog,
            completedAdventureIds: [outsideAdv.id],
            finishedSessions: [outsideSession]
        )
        XCTAssertTrue(badgesOutside.contains(where: { $0.id == "wind_of_change" }), "Значок 'Ветер перемен' за квест вне дома")

        // --- Значок 4: «Чистая импровизация» (pure_improvisation) ---
        // outsideAdv содержит тег .spontaneous; session без пропусков (skips: 0)
        XCTAssertTrue(
            badgesOutside.contains(where: { $0.id == "pure_improvisation" }),
            "Спонтанный квест без единого пропуска карточек должен открывать 'Чистая импровизация'"
        )

        // Проверка негативного кейса для «Чистая импровизация» (был пропуск карточки)
        let spontaneousWithSkip = createFinishedSession(adventureId: outsideAdv.id, stepCount: 4, skips: 1)
        let badgesWithSkip = ProgressionEngine.checkAchievements(
            currentAdventure: outsideAdv,
            currentSession: spontaneousWithSkip,
            allAdventures: allCatalog,
            completedAdventureIds: [outsideAdv.id],
            finishedSessions: [spontaneousWithSkip]
        )
        XCTAssertFalse(
            badgesWithSkip.contains(where: { $0.id == "pure_improvisation" }),
            "При наличии пропуска карточек 'Чистая импровизация' НЕ должна открываться"
        )

        // --- Значок 6: «Никаких секретов» (no_secrets) ---
        let deepSession = createFinishedSession(adventureId: deepAdv.id, stepCount: 4, skips: 0)
        let badgesDeep = ProgressionEngine.checkAchievements(
            currentAdventure: deepAdv,
            currentSession: deepSession,
            allAdventures: allCatalog,
            completedAdventureIds: [deepAdv.id],
            finishedSessions: [deepSession]
        )
        XCTAssertTrue(badgesDeep.contains(where: { $0.id == "no_secrets" }), "Квест с вайбом .deep открывает 'Никаких секретов'")

        // --- Значок 2: «Мастера уюта» (cozy_masters) ---
        // 3 домашних квеста
        let homeSessions = [
            createFinishedSession(adventureId: homeFreeAdv.id, stepCount: 4, skips: 0),
            createFinishedSession(adventureId: homeAdv2.id, stepCount: 4, skips: 0),
            createFinishedSession(adventureId: homeAdv3.id, stepCount: 4, skips: 0)
        ]
        let homeIds: Set<String> = [homeFreeAdv.id, homeAdv2.id, homeAdv3.id]
        let badgesHome = ProgressionEngine.checkAchievements(
            currentAdventure: homeAdv3,
            currentSession: homeSessions[2],
            allAdventures: allCatalog,
            completedAdventureIds: homeIds,
            finishedSessions: homeSessions
        )
        XCTAssertTrue(badgesHome.contains(where: { $0.id == "cozy_masters" }), "3 домашних квеста открывают 'Мастера уюта'")

        // --- Значок 7: «Глава закрыта» (chapter_closed) ---
        // 4 квеста одной главы ch_01
        var chapter1Ids = homeIds
        chapter1Ids.insert(homeAdv4.id)
        let allChapter1Sessions = homeSessions + [createFinishedSession(adventureId: homeAdv4.id, stepCount: 4, skips: 0)]
        let badgesChapterClosed = ProgressionEngine.checkAchievements(
            currentAdventure: homeAdv4,
            currentSession: allChapter1Sessions.last,
            allAdventures: allCatalog,
            completedAdventureIds: chapter1Ids,
            finishedSessions: allChapter1Sessions
        )
        XCTAssertTrue(
            badgesChapterClosed.contains(where: { $0.id == "chapter_closed" }),
            "Все 4 пройденных квеста главы открывают значок 'Глава закрыта'"
        )

        // --- Значок 8: «Идеальная волна» (perfect_wave) ---
        // 3 сессии подряд без пропусков (12 карточек подряд)
        let waveSessions = [
            createFinishedSession(adventureId: homeFreeAdv.id, stepCount: 4, skips: 0),
            createFinishedSession(adventureId: homeAdv2.id, stepCount: 4, skips: 0),
            createFinishedSession(adventureId: homeAdv3.id, stepCount: 4, skips: 0)
        ]
        let badgesWave = ProgressionEngine.checkAchievements(
            currentAdventure: homeAdv3,
            currentSession: waveSessions[2],
            allAdventures: allCatalog,
            completedAdventureIds: [homeFreeAdv.id, homeAdv2.id, homeAdv3.id],
            finishedSessions: waveSessions
        )
        XCTAssertTrue(
            badgesWave.contains(where: { $0.id == "perfect_wave" }),
            "3 идеальных приключения подряд открывают значок 'Идеальная волна'"
        )

        // Проверка негативного кейса для «Идеальная волна»: средняя сессия имела пропуск
        let brokenWaveSessions = [
            createFinishedSession(adventureId: homeFreeAdv.id, stepCount: 4, skips: 0),
            createFinishedSession(adventureId: homeAdv2.id, stepCount: 4, skips: 1), // с пропуском
            createFinishedSession(adventureId: homeAdv3.id, stepCount: 4, skips: 0)
        ]
        let badgesBrokenWave = ProgressionEngine.checkAchievements(
            currentAdventure: homeAdv3,
            currentSession: brokenWaveSessions[2],
            allAdventures: allCatalog,
            completedAdventureIds: [homeFreeAdv.id, homeAdv2.id, homeAdv3.id],
            finishedSessions: brokenWaveSessions
        )
        XCTAssertFalse(
            badgesBrokenWave.contains(where: { $0.id == "perfect_wave" }),
            "При прерывании серии идеальных прохождений значок 'Идеальная волна' НЕ должен начисляться"
        )

        // --- Интеграционный тест evaluateAndAwardAchievements (профиль, +20 XP, идемпотентность) ---
        let profile = CoupleProfile(totalXP: 0, unlockedAchievementIds: [])
        let newlyUnlocked = ProgressionEngine.evaluateAndAwardAchievements(
            profile: profile,
            currentSession: session1,
            currentAdventure: homeFreeAdv,
            allAdventures: allCatalog,
            pastFinishedSessions: []
        )
        XCTAssertFalse(newlyUnlocked.isEmpty)
        for badge in newlyUnlocked {
            XCTAssertEqual(badge.bonusXP, 20, "Каждый значок обязан давать +20 XP")
            XCTAssertTrue(profile.unlockedAchievementIds.contains(badge.id))
        }
        let totalXpAfterAward = profile.totalXP
        XCTAssertEqual(totalXpAfterAward, newlyUnlocked.count * 20)

        // Повторный вызов не должен дублировать значки и не добавлять XP
        let duplicateCall = ProgressionEngine.evaluateAndAwardAchievements(
            profile: profile,
            currentSession: session1,
            currentAdventure: homeFreeAdv,
            allAdventures: allCatalog,
            pastFinishedSessions: [session1]
        )
        XCTAssertTrue(duplicateCall.isEmpty, "Повторная оценка не должна возвращать уже открытые значки")
        XCTAssertEqual(profile.totalXP, totalXpAfterAward, "XP профиля не должен изменяться повторно")
    }

    // MARK: - Вспомогательные методы создания мок-данных

    private func createMockAdventure(id: String, chapterId: String) -> AdventureDefinition {
        AdventureDefinition(
            id: id,
            chapterId: chapterId,
            orderInChapter: 1,
            title: "Квест \(id)",
            subtitle: "",
            preparationNote: "",
            vibes: [.cozy],
            duration: .medium,
            budget: .free,
            location: .home,
            cards: []
        )
    }

    private func createCustomAdventure(
        id: String,
        chapterId: String,
        vibes: [VibeTag],
        budget: BudgetTier,
        location: LocationType
    ) -> AdventureDefinition {
        AdventureDefinition(
            id: id,
            chapterId: chapterId,
            orderInChapter: 1,
            title: "Приключение \(id)",
            subtitle: "",
            preparationNote: "",
            vibes: vibes,
            duration: .medium,
            budget: budget,
            location: location,
            cards: []
        )
    }

    private func createFinishedSession(adventureId: String, stepCount: Int = 4, skips: Int = 0) -> AdventureSessionRecord {
        let session = AdventureSessionRecord(adventureId: adventureId, stepCount: stepCount)
        for step in 1...stepCount {
            if step <= skips {
                session.stepStatuses[step - 1] = "skipped"
            } else {
                session.stepStatuses[step - 1] = "completed"
                session.earnedXP += 10
            }
        }
        session.isFinished = true
        session.completedAt = Date()
        session.earnedXP += 25
        return session
    }
}
