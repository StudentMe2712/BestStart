//
//  PeerSyncService.swift
//  ChooseAVibe
//
//  Сервис P2P-синхронизации двух iPhone в режиме реального времени
//  согласно Разделу 7 спецификации («Готовность к режиму Два iPhone v2.0»).
//  Поддерживает локальное сопряжение через MultipeerConnectivity (Wi-Fi / Bluetooth)
//  без подключения к интернету по 4-значному коду, а также подготовлен к синхронизации через CloudKit.
//

import Foundation
import Combine
#if canImport(MultipeerConnectivity)
import MultipeerConnectivity
#endif
#if canImport(UIKit)
import UIKit
#endif

// MARK: - Состояния синхронизации (SyncState)

/// Состояние сопряжения и сетевой сессии между двумя устройствами.
public enum SyncState: Equatable, Sendable {
    /// Режим ожидания действий пользователя
    case idle
    /// Режим хоста (ожидание подключения партнёра с 4-значным кодом)
    case hosting(pairCode: String)
    /// Режим поиска сессии партнёра по коду
    case searching
    /// Успешное соединение с партнёром
    case connected(peerName: String)

    /// Текстовое описание состояния на русском языке
    public var displayStatus: String {
        switch self {
        case .idle:
            return "Ожидание сопряжения"
        case .hosting(let code):
            return "Код для партнёра: \(code). Ожидание подключения..."
        case .searching:
            return "Поиск устройства партнёра..."
        case .connected(let name):
            return "Подключено к устройству: \(name)"
        }
    }

    /// Флаг активного установленного соединения
    public var isConnected: Bool {
        if case .connected = self { return true }
        return false
    }
}

// MARK: - Роли партнёров (PartnerRole)

/// Идентификатор роли в паре для распределения очерёдности ходов
public enum PartnerRole: String, Codable, CaseIterable, Sendable {
    case partner1 = "Партнёр 1"
    case partner2 = "Партнёр 2"

    /// Противоположная роль
    public var opposite: PartnerRole {
        switch self {
        case .partner1: return .partner2
        case .partner2: return .partner1
        }
    }
}

// MARK: - Пакеты синхронизации (PeerSyncPacket)

/// Единый протокол сообщений синхронизации между двумя iPhone (P2P и CloudKit).
public enum PeerSyncPacket: Codable, Equatable, Sendable {
    /// Приглашение к сопряжению с 4-значным пин-кодом
    case pairInvite(pairCode: String)
    /// Подтверждение сопряжения с передачей имени партнёра
    case pairAccept(partnerName: String)
    /// Синхронизация текущего состояния приключения
    case sessionSync(adventureId: String, stepIndex: Int, isScratched: Bool, isUsingFallback: Bool)
    /// Прогресс стирания скретч-слоя в реальном времени (0.0 ... 1.0)
    case scratchProgress(percent: Double)
    /// Передача очереди хода следующему партнёру
    case turnHandover(nextStepIndex: Int, activePartner: String)
    /// Успешное завершение приключения и начисление суммарного опыта
    case finishAdventure(earnedXP: Int)

    // MARK: - Пользовательское кодирование для кроссплатформенной совместимости

    private enum CodingKeys: String, CodingKey {
        case type
        case pairCode
        case partnerName
        case adventureId
        case stepIndex
        case isScratched
        case isUsingFallback
        case percent
        case nextStepIndex
        case activePartner
        case earnedXP
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let type = try container.decode(String.self, forKey: .type)

        switch type {
        case "pairInvite":
            let code = try container.decode(String.self, forKey: .pairCode)
            self = .pairInvite(pairCode: code)

        case "pairAccept":
            let name = try container.decode(String.self, forKey: .partnerName)
            self = .pairAccept(partnerName: name)

        case "sessionSync":
            let advId = try container.decode(String.self, forKey: .adventureId)
            let step = try container.decode(Int.self, forKey: .stepIndex)
            let isScratched = try container.decode(Bool.self, forKey: .isScratched)
            let isFallback = try container.decode(Bool.self, forKey: .isUsingFallback)
            self = .sessionSync(adventureId: advId, stepIndex: step, isScratched: isScratched, isUsingFallback: isFallback)

        case "scratchProgress":
            let percent = try container.decode(Double.self, forKey: .percent)
            self = .scratchProgress(percent: percent)

        case "turnHandover":
            let nextStep = try container.decode(Int.self, forKey: .nextStepIndex)
            let partner = try container.decode(String.self, forKey: .activePartner)
            self = .turnHandover(nextStepIndex: nextStep, activePartner: partner)

        case "finishAdventure":
            let xp = try container.decode(Int.self, forKey: .earnedXP)
            self = .finishAdventure(earnedXP: xp)

        default:
            throw DecodingError.dataCorruptedError(
                forKey: .type,
                in: container,
                debugDescription: "Неизвестный тип пакета синхронизации: \(type)"
            )
        }
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)

        switch self {
        case .pairInvite(let pairCode):
            try container.encode("pairInvite", forKey: .type)
            try container.encode(pairCode, forKey: .pairCode)

        case .pairAccept(let partnerName):
            try container.encode("pairAccept", forKey: .type)
            try container.encode(partnerName, forKey: .partnerName)

        case .sessionSync(let adventureId, let stepIndex, let isScratched, let isUsingFallback):
            try container.encode("sessionSync", forKey: .type)
            try container.encode(adventureId, forKey: .adventureId)
            try container.encode(stepIndex, forKey: .stepIndex)
            try container.encode(isScratched, forKey: .isScratched)
            try container.encode(isUsingFallback, forKey: .isUsingFallback)

        case .scratchProgress(let percent):
            try container.encode("scratchProgress", forKey: .type)
            try container.encode(percent, forKey: .percent)

        case .turnHandover(let nextStepIndex, let activePartner):
            try container.encode("turnHandover", forKey: .type)
            try container.encode(nextStepIndex, forKey: .nextStepIndex)
            try container.encode(activePartner, forKey: .activePartner)

        case .finishAdventure(let earnedXP):
            try container.encode("finishAdventure", forKey: .type)
            try container.encode(earnedXP, forKey: .earnedXP)
        }
    }
}

// MARK: - Протокол облачного транспорта (CloudKit Ready)

/// Интерфейс удалённого транспорта сообщений для удалённой игры (CloudKit / WebSocket)
public protocol CloudKitSyncTransportProtocol: AnyObject, Sendable {
    func sendPacket(_ packet: PeerSyncPacket, sessionId: String) async throws
    func startObserving(sessionId: String, onPacketReceived: @escaping @Sendable (PeerSyncPacket) -> Void) async throws
    func stopObserving() async
}

/// Базовая реализация заглушки CloudKit с готовностью к внедрению в v2.0
public final class MockCloudKitSyncTransport: CloudKitSyncTransportProtocol, @unchecked Sendable {
    public init() {}

    public func sendPacket(_ packet: PeerSyncPacket, sessionId: String) async throws {
        // Резерв для сериализации в CKRecord и отправки в CKDatabase
    }

    public func startObserving(sessionId: String, onPacketReceived: @escaping @Sendable (PeerSyncPacket) -> Void) async throws {
        // Резерв для оформления CKQuerySubscription и обработки remote notifications
    }

    public func stopObserving() async {
        // Остановка подписки
    }
}

// MARK: - Сервис синхронизации (PeerSyncService)

/// Сервис реального времени для синхронизации сессии приключения между двумя iPhone.
/// Обеспечивает прямое P2P-соединение по Wi-Fi и Bluetooth без необходимости выхода в интернет.
@MainActor
public final class PeerSyncService: ObservableObject {

    // MARK: - Константы протокола

    /// Уникальный идентификатор службы Bonjour для MultipeerConnectivity (строго 1-15 символов, lowercase ASCII)
    public static let serviceType = "choose-vibe"

    /// Синглтон для глобального доступа в приложении
    public static let shared = PeerSyncService()

    // MARK: - Опубликованные свойства состояния

    /// Текущее состояние сетевого сопряжения
    @Published public private(set) var syncState: SyncState = .idle

    /// Последний успешно принятый сетевой пакет
    @Published public private(set) var lastReceivedPacket: PeerSyncPacket?

    /// Текущий прогресс стирания скретч-карты партнёром (0.0 ... 1.0)
    @Published public private(set) var scratchProgress: Double = 0.0

    /// Имя подключённого устройства партнёра
    @Published public private(set) var connectedPeerName: String?

    /// Флаг полного открытия карточки партнёром (по достижении 45% порога)
    @Published public private(set) var isPartnerCardRevealed: Bool = false

    /// Имя первого партнёра (по умолчанию "Партнёр 1")
    @Published public var partnerOneName: String = "Партнёр 1"

    /// Имя второго партнёра (по умолчанию "Партнёр 2")
    @Published public var partnerTwoName: String = "Партнёр 2"

    /// Роль локального устройства в паре
    @Published public var localRole: PartnerRole = .partner1

    /// Текущий активный номер шага квеста (1...4)
    @Published public private(set) var currentStepIndex: Int = 1

    /// Флаг: текущий ход принадлежит локальному пользователю
    @Published public private(set) var isMyTurn: Bool = true

    /// Флаг завершения всего квеста
    @Published public private(set) var isFinished: Bool = false

    /// Суммарный заработанный опыт в сессии
    @Published public private(set) var earnedXP: Int = 0

    /// Текущий код сопряжения (для хостинга или поиска)
    @Published public private(set) var currentPairCode: String?

    /// Идентификатор текущего синхронизируемого приключения
    @Published public private(set) var currentAdventureId: String?

    /// Используется ли сюжетный запасной вариант (fallback) на текущем шаге
    @Published public private(set) var isUsingFallback: Bool = false

    /// Последнее сообщение об ошибке сетевого обмена
    @Published public private(set) var lastErrorMessage: String?

    // MARK: - Сетевые сущности MultipeerConnectivity

    #if canImport(MultipeerConnectivity)
    private var peerId: MCPeerID?
    private var mcSession: MCSession?
    private var advertiser: MCNearbyServiceAdvertiser?
    private var browser: MCNearbyServiceBrowser?
    private var transportHandler: MultipeerTransportBridge?
    #endif

    /// Дополнительный транспорт (CloudKit / WebSocket)
    public var cloudKitTransport: CloudKitSyncTransportProtocol?

    // MARK: - Инициализация

    public init(
        partnerOneName: String = "Партнёр 1",
        partnerTwoName: String = "Партнёр 2",
        localRole: PartnerRole = .partner1,
        cloudKitTransport: CloudKitSyncTransportProtocol? = nil
    ) {
        self.partnerOneName = partnerOneName
        self.partnerTwoName = partnerTwoName
        self.localRole = localRole
        self.cloudKitTransport = cloudKitTransport
        self.isMyTurn = Self.isTurn(for: 1, role: localRole)
    }

    // MARK: - Управление очерёдностью ходов (Section 7 Spec)

    /// Проверка, является ли шаг ходом первого партнёра (нечётные 1 и 3 — Партнёр 1)
    public static func isPartner1Turn(stepIndex: Int) -> Bool {
        return stepIndex % 2 != 0
    }

    /// Проверка, является ли шаг ходом второго партнёра (чётные 2 и 4 — Партнёр 2)
    public static func isPartner2Turn(stepIndex: Int) -> Bool {
        return stepIndex % 2 == 0
    }

    /// Проверка, чей сейчас ход для конкретной роли устройства
    public static func isTurn(for stepIndex: Int, role: PartnerRole) -> Bool {
        switch role {
        case .partner1:
            return isPartner1Turn(stepIndex: stepIndex)
        case .partner2:
            return isPartner2Turn(stepIndex: stepIndex)
        }
    }

    /// Получение отображаемого имени активного ведущего партнёра для заданного шага
    public static func activePartnerForStep(
        stepIndex: Int,
        partner1: String = "Партнёр 1",
        partner2: String = "Партнёр 2"
    ) -> String {
        return isPartner1Turn(stepIndex: stepIndex) ? partner1 : partner2
    }

    /// Получение имени текущего активного партнёра на основе текущих настроек
    public var activePartnerName: String {
        Self.activePartnerForStep(
            stepIndex: currentStepIndex,
            partner1: partnerOneName,
            partner2: partnerTwoName
        )
    }

    // MARK: - Валидация и генерация 4-значного кода

    /// Проверка валидности 4-значного кода сопряжения (строго 4 цифры 0-9)
    public static func isValidPairCode(_ code: String) -> Bool {
        guard code.count == 4 else { return false }
        return code.allSatisfy { $0.isNumber }
    }

    /// Генерация случайного 4-значного кода сопряжения (1000...9999)
    public static func generatePairCode() -> String {
        let codeNumber = Int.random(in: 1000...9999)
        return String(format: "%04d", codeNumber)
    }

    // MARK: - Конфигурация профиля

    /// Настройка имён и локальной роли пары
    public func configurePartners(partnerOne: String, partnerTwo: String, localRole: PartnerRole) {
        self.partnerOneName = partnerOne
        self.partnerTwoName = partnerTwo
        self.localRole = localRole
        self.isMyTurn = Self.isTurn(for: currentStepIndex, role: localRole)
    }

    // MARK: - Запуск P2P: Режим Хоста (startHosting)

    /// Запуск режима ожидания партнёра в качестве точки раздачи (хоста).
    /// - Parameters:
    ///   - pairCode: 4-значный код сопряжения (если nil, генерируется автоматически)
    ///   - partnerName: Отображаемое имя локального устройства
    public func startHosting(pairCode: String? = nil, partnerName: String? = nil) {
        stopSync()

        let effectiveCode = pairCode ?? Self.generatePairCode()
        guard Self.isValidPairCode(effectiveCode) else {
            self.lastErrorMessage = "Некорректный код сопряжения. Требуется 4 цифры."
            return
        }

        self.currentPairCode = effectiveCode
        self.localRole = .partner1
        if let partnerName = partnerName {
            self.partnerOneName = partnerName
        }
        self.syncState = .hosting(pairCode: effectiveCode)
        self.isMyTurn = Self.isTurn(for: currentStepIndex, role: .partner1)

        #if canImport(MultipeerConnectivity)
        setupMultipeerStack(displayName: self.partnerOneName)

        guard let mcSession = self.mcSession, let peerId = self.peerId else { return }

        let discoveryInfo = ["pairCode": effectiveCode]
        let adv = MCNearbyServiceAdvertiser(
            peer: peerId,
            discoveryInfo: discoveryInfo,
            serviceType: Self.serviceType
        )

        let bridge = MultipeerTransportBridge(service: self, session: mcSession)
        self.transportHandler = bridge
        adv.delegate = bridge
        self.advertiser = adv
        adv.startAdvertisingPeer()
        #endif
    }

    // MARK: - Запуск P2P: Режим Поиска (startSearching)

    /// Запуск режима поиска хоста партнёра по введённому 4-значному коду.
    /// - Parameters:
    ///   - targetCode: 4-значный код, отображённый на первом телефоне
    ///   - partnerName: Отображаемое имя локального устройства (Партнёр 2)
    public func startSearching(targetCode: String, partnerName: String? = nil) {
        stopSync()

        guard Self.isValidPairCode(targetCode) else {
            self.lastErrorMessage = "Код сопряжения должен состоять ровно из 4 цифр."
            return
        }

        self.currentPairCode = targetCode
        self.localRole = .partner2
        if let partnerName = partnerName {
            self.partnerTwoName = partnerName
        }
        self.syncState = .searching
        self.isMyTurn = Self.isTurn(for: currentStepIndex, role: .partner2)

        #if canImport(MultipeerConnectivity)
        setupMultipeerStack(displayName: self.partnerTwoName)

        guard let mcSession = self.mcSession, let peerId = self.peerId else { return }

        let brows = MCNearbyServiceBrowser(peer: peerId, serviceType: Self.serviceType)
        let bridge = MultipeerTransportBridge(service: self, session: mcSession)
        self.transportHandler = bridge
        brows.delegate = bridge
        self.browser = brows
        brows.startBrowsingForPeers()
        #endif
    }

    // MARK: - Остановка синхронизации (stopSync)

    /// Завершение активного P2P соединения и очистка сетевых ресурсов
    public func stopSync() {
        #if canImport(MultipeerConnectivity)
        advertiser?.stopAdvertisingPeer()
        advertiser?.delegate = nil
        advertiser = nil

        browser?.stopBrowsingForPeers()
        browser?.delegate = nil
        browser = nil

        mcSession?.disconnect()
        mcSession?.delegate = nil
        mcSession = nil
        transportHandler = nil
        peerId = nil
        #endif

        self.syncState = .idle
        self.connectedPeerName = nil
        self.currentPairCode = nil
    }

    // MARK: - Отправка пакетов синхронизации (send)

    /// Отправка пакета синхронизации подключённому партнёру по P2P
    public func send(packet: PeerSyncPacket) {
        #if canImport(MultipeerConnectivity)
        guard let session = mcSession, !session.connectedPeers.isEmpty else {
            // Если физический P2P не подключён, пытаемся отправить через облачный транспорт
            if let cloud = cloudKitTransport, let advId = currentAdventureId {
                Task {
                    try? await cloud.sendPacket(packet, sessionId: advId)
                }
            }
            return
        }

        do {
            let data = try JSONEncoder().encode(packet)
            try session.send(data, toPeers: session.connectedPeers, with: .reliable)
        } catch {
            self.lastErrorMessage = "Ошибка передачи пакета: \(error.localizedDescription)"
        }
        #endif
    }

    // MARK: - Высокоуровневые методы передачи событий

    /// Передача прогресса стирания карточки партнёру в реальном времени
    public func sendScratchProgress(_ percent: Double) {
        let clamped = max(0.0, min(1.0, percent))
        self.scratchProgress = clamped
        send(packet: .scratchProgress(percent: clamped))
    }

    /// Синхронизация общего состояния сессии (выбранное приключение, номер шага, статус)
    public func syncSession(adventureId: String, stepIndex: Int, isScratched: Bool, isUsingFallback: Bool) {
        self.currentAdventureId = adventureId
        self.currentStepIndex = stepIndex
        self.isUsingFallback = isUsingFallback
        self.isMyTurn = Self.isTurn(for: stepIndex, role: localRole)

        send(packet: .sessionSync(
            adventureId: adventureId,
            stepIndex: stepIndex,
            isScratched: isScratched,
            isUsingFallback: isUsingFallback
        ))
    }

    /// Передача права хода второму партнёру с вызовом тактильного импульса
    public func handoverTurn(nextStepIndex: Int, activePartner: String? = nil) {
        let nextPartner = activePartner ?? Self.activePartnerForStep(
            stepIndex: nextStepIndex,
            partner1: partnerOneName,
            partner2: partnerTwoName
        )

        self.currentStepIndex = nextStepIndex
        self.scratchProgress = 0.0
        self.isPartnerCardRevealed = false
        self.isMyTurn = Self.isTurn(for: nextStepIndex, role: localRole)

        triggerSuccessHaptic()
        send(packet: .turnHandover(nextStepIndex: nextStepIndex, activePartner: nextPartner))
    }

    /// Завершение всего приключения с начислением опыта
    public func finishAdventure(earnedXP: Int) {
        self.isFinished = true
        self.earnedXP = earnedXP
        triggerSuccessHaptic()
        send(packet: .finishAdventure(earnedXP: earnedXP))
    }

    // MARK: - Обработка входящих пакетов (handleIncomingPacket)

    /// Централизованная обработка принятого пакета (вызывается сетевым мостом или тестами)
    public func handleIncomingPacket(_ packet: PeerSyncPacket) {
        self.lastReceivedPacket = packet

        switch packet {
        case .pairInvite(let pairCode):
            // Если получен инвайт с совпадающим кодом, подтверждаем сопряжение
            if self.currentPairCode == pairCode {
                let myName = localRole == .partner1 ? partnerOneName : partnerTwoName
                send(packet: .pairAccept(partnerName: myName))
            }

        case .pairAccept(let partnerName):
            // Подтверждение сопряжения: переводим состояние в connected
            self.connectedPeerName = partnerName
            self.syncState = .connected(peerName: partnerName)
            triggerSuccessHaptic()

        case .sessionSync(let adventureId, let stepIndex, isScratched: _, let isFallback):
            self.currentAdventureId = adventureId
            self.currentStepIndex = stepIndex
            self.isUsingFallback = isFallback
            self.scratchProgress = 0.0
            self.isPartnerCardRevealed = false
            self.isMyTurn = Self.isTurn(for: stepIndex, role: localRole)

        case .scratchProgress(let percent):
            self.scratchProgress = percent
            // Порог авто-раскрытия 45%: фиксируем открытие карточки партнёром
            if percent >= 0.45 && !isPartnerCardRevealed {
                self.isPartnerCardRevealed = true
                triggerSuccessHaptic()
            }

        case .turnHandover(let nextStepIndex, _):
            self.currentStepIndex = nextStepIndex
            self.scratchProgress = 0.0
            self.isPartnerCardRevealed = false
            self.isMyTurn = Self.isTurn(for: nextStepIndex, role: localRole)
            triggerSuccessHaptic()

        case .finishAdventure(let xp):
            self.isFinished = true
            self.earnedXP = xp
            triggerSuccessHaptic()
        }
    }

    /// Сброс параметров сессии к исходным значениям
    public func resetSession() {
        self.currentStepIndex = 1
        self.scratchProgress = 0.0
        self.isPartnerCardRevealed = false
        self.isFinished = false
        self.earnedXP = 0
        self.isUsingFallback = false
        self.lastReceivedPacket = nil
        self.lastErrorMessage = nil
        self.isMyTurn = Self.isTurn(for: 1, role: localRole)
    }

    // MARK: - Симуляция пакетов для Unit-тестов

    /// Метод внедрения пакета для детерминированного модульного тестирования без физических устройств
    public func simulateIncomingPacketForTesting(_ packet: PeerSyncPacket) {
        handleIncomingPacket(packet)
    }

    // MARK: - Вспомогательные методы Multipeer

    #if canImport(MultipeerConnectivity)
    private func setupMultipeerStack(displayName: String) {
        let pId = MCPeerID(displayName: displayName)
        self.peerId = pId
        let sess = MCSession(peer: pId, securityIdentity: nil, encryptionPreference: .required)
        self.mcSession = sess
    }

    internal func handlePeerConnected(name: String) {
        self.connectedPeerName = name
        self.syncState = .connected(peerName: name)
        triggerSuccessHaptic()
    }

    internal func handlePeerDisconnected(name: String) {
        if self.connectedPeerName == name {
            self.connectedPeerName = nil
            self.syncState = .idle
            self.lastErrorMessage = "Связь с устройством \(name) потеряна."
        }
    }
    #endif

    // MARK: - Виброотклик

    private func triggerSuccessHaptic() {
        #if canImport(UIKit)
        let generator = UINotificationFeedbackGenerator()
        generator.prepare()
        generator.notificationOccurred(.success)
        #endif
    }
}

// MARK: - MultipeerTransportBridge (Мост делегатов MultipeerConnectivity)

#if canImport(MultipeerConnectivity)
/// Внутренний обработчик фоновых делегатов MultipeerConnectivity с безопасной изоляцией в MainActor
final class MultipeerTransportBridge: NSObject, MCSessionDelegate, MCNearbyServiceAdvertiserDelegate, MCNearbyServiceBrowserDelegate {

    private weak var service: PeerSyncService?
    private weak var session: MCSession?

    init(service: PeerSyncService, session: MCSession) {
        self.service = service
        self.session = session
        super.init()
        session.delegate = self
    }

    // MARK: - MCSessionDelegate

    func session(_ session: MCSession, peer peerID: MCPeerID, didChange state: MCSessionState) {
        Task { @MainActor [weak self] in
            guard let self = self, let service = self.service else { return }
            switch state {
            case .connected:
                service.handlePeerConnected(name: peerID.displayName)
            case .notConnected:
                service.handlePeerDisconnected(name: peerID.displayName)
            case .connecting:
                break
            @unknown default:
                break
            }
        }
    }

    func session(_ session: MCSession, didReceive data: Data, fromPeer peerID: MCPeerID) {
        do {
            let packet = try JSONDecoder().decode(PeerSyncPacket.self, from: data)
            Task { @MainActor [weak self] in
                self?.service?.handleIncomingPacket(packet)
            }
        } catch {
            Task { @MainActor [weak self] in
                self?.service?.send(packet: .scratchProgress(percent: 0.0))
            }
        }
    }

    func session(_ session: MCSession, didReceive stream: InputStream, withName streamName: String, fromPeer peerID: MCPeerID) {}
    func session(_ session: MCSession, didStartReceivingResourceWithName resourceName: String, fromPeer peerID: MCPeerID, with progress: Progress) {}
    func session(_ session: MCSession, didFinishReceivingResourceWithName resourceName: String, fromPeer peerID: MCPeerID, at localURL: URL?, withError error: Error?) {}

    // MARK: - MCNearbyServiceAdvertiserDelegate

    func advertiser(_ advertiser: MCNearbyServiceAdvertiser, didReceiveInvitationFromPeer peerID: MCPeerID, withContext context: Data?, invitationHandler: @escaping (Bool, MCSession?) -> Void) {
        Task { @MainActor [weak self] in
            guard let self = self, let session = self.session, let service = self.service else {
                invitationHandler(false, nil)
                return
            }

            // Принимаем приглашение, если мы ожидаем сопряжения в режиме хоста
            if case .hosting = service.syncState {
                invitationHandler(true, session)
            } else {
                invitationHandler(false, nil)
            }
        }
    }

    func advertiser(_ advertiser: MCNearbyServiceAdvertiser, didNotStartAdvertisingPeer error: Error) {
        Task { @MainActor [weak self] in
            self?.service?.stopSync()
        }
    }

    // MARK: - MCNearbyServiceBrowserDelegate

    func browser(_ browser: MCNearbyServiceBrowser, foundPeer peerID: MCPeerID, withDiscoveryInfo info: [String : String]?) {
        Task { @MainActor [weak self] in
            guard let self = self, let session = self.session, let service = self.service else { return }

            // Если ищем конкретный код, проверяем discoveryInfo
            if let targetCode = service.currentPairCode {
                if let peerCode = info?["pairCode"], peerCode == targetCode {
                    browser.invitePeer(peerID, to: session, withContext: nil, timeout: 20)
                }
            } else {
                browser.invitePeer(peerID, to: session, withContext: nil, timeout: 20)
            }
        }
    }

    func browser(_ browser: MCNearbyServiceBrowser, lostPeer peerID: MCPeerID) {}

    func browser(_ browser: MCNearbyServiceBrowser, didNotStartBrowsingForPeers error: Error) {
        Task { @MainActor [weak self] in
            self?.service?.stopSync()
        }
    }
}
#endif
