//
//  PeerSyncTests.swift
//  ChooseAVibeTests
//
//  Набор модульных тестов XCTest для протокола синхронизации режима «Два iPhone» (PeerSyncService):
//  1. Сериализация и десериализация всех типов пакетов PeerSyncPacket (Round-trip JSON)
//  2. Валидация и генерация 4-значного кода сопряжения (pairCode)
//  3. Проверка правил чередования ролей партнёров (нечётные 1 и 3 — Партнёр 1, чётные 2 и 4 — Партнёр 2)
//  4. Обработка событий передачи хода и завершения сессии с начислением опыта
//  5. Фиксация порога авто-раскрытия карточки на 45% стирания
//

import XCTest
#if canImport(ChooseAVibeCore)
@testable import ChooseAVibeCore
#endif
#if canImport(ChooseAVibe)
@testable import ChooseAVibe
#endif

final class PeerSyncTests: XCTestCase {

    private var jsonEncoder: JSONEncoder!
    private var jsonDecoder: JSONDecoder!

    override func setUp() {
        super.setUp()
        jsonEncoder = JSONEncoder()
        jsonDecoder = JSONDecoder()
    }

    override func tearDown() {
        jsonEncoder = nil
        jsonDecoder = nil
        super.tearDown()
    }

    // MARK: - 1. Сериализация и десериализация PeerSyncPacket

    /// Проверка round-trip сериализации пакета pairInvite
    func testSerializationPairInvite() throws {
        let original = PeerSyncPacket.pairInvite(pairCode: "7492")
        let data = try jsonEncoder.encode(original)
        let decoded = try jsonDecoder.decode(PeerSyncPacket.self, from: data)

        XCTAssertEqual(decoded, original, "Пакет pairInvite должен корректно восстанавливаться из JSON")
        if case .pairInvite(let code) = decoded {
            XCTAssertEqual(code, "7492")
        } else {
            XCTFail("Ожидался пакет pairInvite")
        }
    }

    /// Проверка round-trip сериализации пакета pairAccept
    func testSerializationPairAccept() throws {
        let original = PeerSyncPacket.pairAccept(partnerName: "Алиса")
        let data = try jsonEncoder.encode(original)
        let decoded = try jsonDecoder.decode(PeerSyncPacket.self, from: data)

        XCTAssertEqual(decoded, original, "Пакет pairAccept должен сохранять имя партнёра")
        if case .pairAccept(let name) = decoded {
            XCTAssertEqual(name, "Алиса")
        } else {
            XCTFail("Ожидался пакет pairAccept")
        }
    }

    /// Проверка round-trip сериализации пакета sessionSync
    func testSerializationSessionSync() throws {
        let original = PeerSyncPacket.sessionSync(
            adventureId: "adv_candle_quest",
            stepIndex: 3,
            isScratched: true,
            isUsingFallback: false
        )
        let data = try jsonEncoder.encode(original)
        let decoded = try jsonDecoder.decode(PeerSyncPacket.self, from: data)

        XCTAssertEqual(decoded, original)
        if case .sessionSync(let id, let step, let scratched, let fallback) = decoded {
            XCTAssertEqual(id, "adv_candle_quest")
            XCTAssertEqual(step, 3)
            XCTAssertTrue(scratched)
            XCTAssertFalse(fallback)
        } else {
            XCTFail("Ожидался пакет sessionSync")
        }
    }

    /// Проверка round-trip сериализации пакета scratchProgress
    func testSerializationScratchProgress() throws {
        let original = PeerSyncPacket.scratchProgress(percent: 0.52)
        let data = try jsonEncoder.encode(original)
        let decoded = try jsonDecoder.decode(PeerSyncPacket.self, from: data)

        XCTAssertEqual(decoded, original)
        if case .scratchProgress(let percent) = decoded {
            XCTAssertEqual(percent, 0.52, accuracy: 0.0001)
        } else {
            XCTFail("Ожидался пакет scratchProgress")
        }
    }

    /// Проверка round-trip сериализации пакета turnHandover
    func testSerializationTurnHandover() throws {
        let original = PeerSyncPacket.turnHandover(nextStepIndex: 2, activePartner: "Максим")
        let data = try jsonEncoder.encode(original)
        let decoded = try jsonDecoder.decode(PeerSyncPacket.self, from: data)

        XCTAssertEqual(decoded, original)
        if case .turnHandover(let nextStep, let partner) = decoded {
            XCTAssertEqual(nextStep, 2)
            XCTAssertEqual(partner, "Максим")
        } else {
            XCTFail("Ожидался пакет turnHandover")
        }
    }

    /// Проверка round-trip сериализации пакета finishAdventure
    func testSerializationFinishAdventure() throws {
        let original = PeerSyncPacket.finishAdventure(earnedXP: 50)
        let data = try jsonEncoder.encode(original)
        let decoded = try jsonDecoder.decode(PeerSyncPacket.self, from: data)

        XCTAssertEqual(decoded, original)
        if case .finishAdventure(let xp) = decoded {
            XCTAssertEqual(xp, 50)
        } else {
            XCTFail("Ожидался пакет finishAdventure")
        }
    }

    // MARK: - 2. Валидация 4-значного кода сопряжения (pairCode)

    /// Проверка корректности проверки 4-значных кодов
    func testPairCodeValidation() {
        // Корректные 4-значные коды
        XCTAssertTrue(PeerSyncService.isValidPairCode("1234"), "Код из 4 цифр должен быть валидным")
        XCTAssertTrue(PeerSyncService.isValidPairCode("0000"), "0000 является валидным 4-значным кодом")
        XCTAssertTrue(PeerSyncService.isValidPairCode("9999"), "9999 является валидным 4-значным кодом")
        XCTAssertTrue(PeerSyncService.isValidPairCode("4815"), "Произвольный 4-значный код валиден")

        // Некорректные по длине коды
        XCTAssertFalse(PeerSyncService.isValidPairCode(""), "Пустая строка не должна быть валидной")
        XCTAssertFalse(PeerSyncService.isValidPairCode("1"), "1 цифра не валидна")
        XCTAssertFalse(PeerSyncService.isValidPairCode("12"), "2 цифры не валидны")
        XCTAssertFalse(PeerSyncService.isValidPairCode("123"), "3 цифры не валидны")
        XCTAssertFalse(PeerSyncService.isValidPairCode("12345"), "5 цифр не валидны")
        XCTAssertFalse(PeerSyncService.isValidPairCode("123456"), "6 цифр не валидны")

        // Некорректные символы
        XCTAssertFalse(PeerSyncService.isValidPairCode("12a4"), "Буквы внутри кода запрещены")
        XCTAssertFalse(PeerSyncService.isValidPairCode("abcd"), "Только буквенный код запрещён")
        XCTAssertFalse(PeerSyncService.isValidPairCode("12 4"), "Пробелы внутри кода запрещены")
        XCTAssertFalse(PeerSyncService.isValidPairCode(" 123"), "Пробел в начале запрещён")
        XCTAssertFalse(PeerSyncService.isValidPairCode("123 "), "Пробел в конце запрещён")
        XCTAssertFalse(PeerSyncService.isValidPairCode("12-4"), "Спецсимволы запрещены")
    }

    /// Проверка генератора кодов сопряжения
    func testPairCodeGeneration() {
        for _ in 0..<20 {
            let generated = PeerSyncService.generatePairCode()
            XCTAssertEqual(generated.count, 4, "Сгенерированный код обязан содержать ровно 4 символа")
            XCTAssertTrue(PeerSyncService.isValidPairCode(generated), "Сгенерированный код должен проходить валидацию")

            let numericValue = Int(generated)
            XCTAssertNotNil(numericValue)
            XCTAssertGreaterThanOrEqual(numericValue!, 1000, "Код должен быть в диапазоне от 1000")
            XCTAssertLessThanOrEqual(numericValue!, 9999, "Код должен быть в диапазоне до 9999")
        }
    }

    // MARK: - 3. Проверка чередования очерёдности ходов (Section 7 Spec)

    /// Нечётные шаги (1 и 3) — ход Партнёра 1, чётные (2 и 4) — ход Партнёра 2
    func testTurnAlternationRules() {
        let partner1 = "Елена"
        let partner2 = "Дмитрий"

        // Шаг 1: Нечётный -> Партнёр 1
        XCTAssertTrue(PeerSyncService.isPartner1Turn(stepIndex: 1), "Шаг 1 обязан принадлежать Партнёру 1")
        XCTAssertFalse(PeerSyncService.isPartner2Turn(stepIndex: 1))
        XCTAssertEqual(
            PeerSyncService.activePartnerForStep(stepIndex: 1, partner1: partner1, partner2: partner2),
            partner1
        )
        XCTAssertTrue(PeerSyncService.isTurn(for: 1, role: .partner1))
        XCTAssertFalse(PeerSyncService.isTurn(for: 1, role: .partner2))

        // Шаг 2: Чётный -> Партнёр 2
        XCTAssertFalse(PeerSyncService.isPartner1Turn(stepIndex: 2))
        XCTAssertTrue(PeerSyncService.isPartner2Turn(stepIndex: 2), "Шаг 2 обязан принадлежать Партнёру 2")
        XCTAssertEqual(
            PeerSyncService.activePartnerForStep(stepIndex: 2, partner1: partner1, partner2: partner2),
            partner2
        )
        XCTAssertFalse(PeerSyncService.isTurn(for: 2, role: .partner1))
        XCTAssertTrue(PeerSyncService.isTurn(for: 2, role: .partner2))

        // Шаг 3: Нечётный -> Партнёр 1
        XCTAssertTrue(PeerSyncService.isPartner1Turn(stepIndex: 3), "Шаг 3 обязан принадлежать Партнёру 1")
        XCTAssertFalse(PeerSyncService.isPartner2Turn(stepIndex: 3))
        XCTAssertEqual(
            PeerSyncService.activePartnerForStep(stepIndex: 3, partner1: partner1, partner2: partner2),
            partner1
        )
        XCTAssertTrue(PeerSyncService.isTurn(for: 3, role: .partner1))
        XCTAssertFalse(PeerSyncService.isTurn(for: 3, role: .partner2))

        // Шаг 4: Чётный -> Партнёр 2
        XCTAssertFalse(PeerSyncService.isPartner1Turn(stepIndex: 4))
        XCTAssertTrue(PeerSyncService.isPartner2Turn(stepIndex: 4), "Шаг 4 обязан принадлежать Партнёру 2")
        XCTAssertEqual(
            PeerSyncService.activePartnerForStep(stepIndex: 4, partner1: partner1, partner2: partner2),
            partner2
        )
        XCTAssertFalse(PeerSyncService.isTurn(for: 4, role: .partner1))
        XCTAssertTrue(PeerSyncService.isTurn(for: 4, role: .partner2))
    }

    // MARK: - 4. Передача хода (turnHandover) и состояние сервиса

    /// Проверка передачи хода между партнёрами и корректного обновления флага isMyTurn
    @MainActor
    func testTurnHandoverWorkflow() {
        let service = PeerSyncService(
            partnerOneName: "Алиса",
            partnerTwoName: "Максим",
            localRole: .partner1
        )

        // Шаг 1: локальный пользователь — Партнёр 1, его ход
        XCTAssertEqual(service.currentStepIndex, 1)
        XCTAssertTrue(service.isMyTurn, "На 1 шаге для Партнёра 1 должен быть активен его ход")
        XCTAssertEqual(service.activePartnerName, "Алиса")

        // Партнёр 1 стирает карточку
        service.simulateIncomingPacketForTesting(.scratchProgress(percent: 0.35))
        XCTAssertEqual(service.scratchProgress, 0.35, accuracy: 0.001)
        XCTAssertFalse(service.isPartnerCardRevealed)

        // Карточка открыта (порог >= 45%)
        service.simulateIncomingPacketForTesting(.scratchProgress(percent: 0.48))
        XCTAssertTrue(service.isPartnerCardRevealed, "При достижении 45% карточка должна считаться открытой")

        // Передача хода на Шаг 2 (ход Максима)
        service.simulateIncomingPacketForTesting(.turnHandover(nextStepIndex: 2, activePartner: "Максим"))

        XCTAssertEqual(service.currentStepIndex, 2)
        XCTAssertFalse(service.isMyTurn, "На шаге 2 у Партнёра 1 ход должен стать неактивен")
        XCTAssertEqual(service.activePartnerName, "Максим")
        XCTAssertEqual(service.scratchProgress, 0.0, "Прогресс стирания должен сброситься при передаче хода")
        XCTAssertFalse(service.isPartnerCardRevealed, "Флаг открытия должен сброситься для нового этапа")

        // Передача хода на Шаг 3 (возврат хода Алисе)
        service.simulateIncomingPacketForTesting(.turnHandover(nextStepIndex: 3, activePartner: "Алиса"))

        XCTAssertEqual(service.currentStepIndex, 3)
        XCTAssertTrue(service.isMyTurn, "На шаге 3 ход возвращается Партнёру 1")
        XCTAssertEqual(service.activePartnerName, "Алиса")
    }

    // MARK: - 5. Обработка завершения сессии (finishAdventure)

    /// Проверка обработки пакета завершения сессии и начисления суммарного опыта
    @MainActor
    func testSessionFinishEventHandling() {
        let service = PeerSyncService(
            partnerOneName: "Алиса",
            partnerTwoName: "Максим",
            localRole: .partner2
        )

        XCTAssertFalse(service.isFinished, "Исходно сессия не должна быть завершена")
        XCTAssertEqual(service.earnedXP, 0, "Начальный опыт должен быть равен 0")

        // Приём пакета finishAdventure со значением 40 XP
        service.simulateIncomingPacketForTesting(.finishAdventure(earnedXP: 40))

        XCTAssertTrue(service.isFinished, "Сессия должна перейти в завершённое состояние")
        XCTAssertEqual(service.earnedXP, 40, "Заработанный опыт должен обновиться до 40 XP")
        XCTAssertEqual(service.lastReceivedPacket, .finishAdventure(earnedXP: 40))

        // Сброс сессии
        service.resetSession()
        XCTAssertFalse(service.isFinished)
        XCTAssertEqual(service.earnedXP, 0)
        XCTAssertEqual(service.currentStepIndex, 1)
    }

    // MARK: - 6. Сопряжение и состояния (SyncState Handshake)

    /// Проверка переходов состояний сопряжения: idle -> hosting -> connected -> idle
    @MainActor
    func testSyncStateHostingAndConnected() {
        let service = PeerSyncService(
            partnerOneName: "Алиса",
            partnerTwoName: "Максим",
            localRole: .partner1
        )

        XCTAssertEqual(service.syncState, .idle)
        XCTAssertFalse(service.syncState.isConnected)

        // Запуск режима хоста с кодом 5821
        service.startHosting(pairCode: "5821", partnerName: "Алиса")
        XCTAssertEqual(service.syncState, .hosting(pairCode: "5821"))
        XCTAssertEqual(service.currentPairCode, "5821")
        XCTAssertTrue(service.syncState.displayStatus.contains("5821"))

        // Приём подтверждения сопряжения pairAccept от Максима
        service.simulateIncomingPacketForTesting(.pairAccept(partnerName: "Максим"))
        XCTAssertEqual(service.syncState, .connected(peerName: "Максим"))
        XCTAssertEqual(service.connectedPeerName, "Максим")
        XCTAssertTrue(service.syncState.isConnected)

        // Остановка синхронизации
        service.stopSync()
        XCTAssertEqual(service.syncState, .idle)
        XCTAssertNil(service.connectedPeerName)
        XCTAssertNil(service.currentPairCode)
    }

    /// Проверка режима поиска (searching) с невалидным кодом
    @MainActor
    func testSearchingWithInvalidCode() {
        let service = PeerSyncService()
        service.startSearching(targetCode: "123") // 3 цифры — невалидно

        XCTAssertEqual(service.syncState, .idle, "При невалидном коде сервис не должен переходить в режим поиска")
        XCTAssertNotNil(service.lastErrorMessage)
    }

    // MARK: - 7. Проверка адаптера CloudKit (CloudKit Ready)

    /// Проверка готовности заглушки CloudKit к удалённой синхронизации без сбоев
    func testCloudKitMockTransportReady() async throws {
        let cloudTransport = MockCloudKitSyncTransport()
        let packet = PeerSyncPacket.sessionSync(
            adventureId: "adv_test_cloud",
            stepIndex: 2,
            isScratched: false,
            isUsingFallback: false
        )

        // Вызов асинхронной отправки через CloudKit заглушку
        try await cloudTransport.sendPacket(packet, sessionId: "session_123")
        await cloudTransport.stopObserving()

        XCTAssertTrue(true, "CloudKit транспортный адаптер успешно выполнил операции")
    }
}
