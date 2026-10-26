//
//  CatalogRepository.swift
//  ChooseAVibe
//
//  Сервис загрузки, парсинга и кэширования каталога приключений, глав и достижений.
//  Поддерживает работу с Bundle.main, Bundle.module, локальными путями (Preview/Unit-тесты).
//

import Foundation
import Combine

// MARK: - Протокол репозитория каталога

/// Интерфейс доступа к статическому контенту приложения
public protocol CatalogRepositoryProtocol: AnyObject, Sendable {
    func getAllChapters() -> [ChapterDefinition]
    func getAllAdventures() -> [AdventureDefinition]
    func getAllAchievements() -> [AchievementDefinition]
    func getAdventure(by id: String) -> AdventureDefinition?
    func getChapter(by id: String) -> ChapterDefinition?
    func getAchievement(by id: String) -> AchievementDefinition?
    func getAdventures(for chapterId: String) -> [AdventureDefinition]
}

// MARK: - Ошибки каталога

public enum CatalogError: LocalizedError, Sendable {
    case fileNotFound(String)
    case decodingFailed(String)
    case emptyCatalog

    public var errorDescription: String? {
        switch self {
        case .fileNotFound(let path):
            return "Файл каталога не найден: \(path)"
        case .decodingFailed(let reason):
            return "Ошибка декодирования каталога: \(reason)"
        case .emptyCatalog:
            return "Каталог пуст или содержит некорректные данные"
        }
    }
}

// MARK: - Реализация CatalogRepository

/// Основной репозиторий статического каталога приложения.
@MainActor
public final class CatalogRepository: ObservableObject, CatalogRepositoryProtocol {
    /// Общий singleton-экземпляр
    public static let shared = CatalogRepository()

    // MARK: - Опубликованные кэшированные данные

    @Published public private(set) var chapters: [ChapterDefinition] = []
    @Published public private(set) var adventures: [AdventureDefinition] = []
    @Published public private(set) var achievements: [AchievementDefinition] = []
    @Published public private(set) var isLoaded: Bool = false
    @Published public private(set) var lastError: CatalogError?

    // MARK: - Внутренние словари быстрого доступа O(1)

    private var adventuresMap: [String: AdventureDefinition] = [:]
    private var chaptersMap: [String: ChapterDefinition] = [:]
    private var achievementsMap: [String: AchievementDefinition] = [:]

    // MARK: - Инициализация

    public init(autoLoad: Bool = true) {
        if autoLoad {
            loadCatalogSafe()
        }
    }

    /// Безопасная загрузка без проброса исключения (записывает ошибку в lastError)
    public func loadCatalogSafe() {
        do {
            try loadCatalog()
        } catch let error as CatalogError {
            self.lastError = error
            #if DEBUG
            print("⚠️ [CatalogRepository] Ошибка загрузки каталога: \(error.localizedDescription)")
            #endif
        } catch {
            self.lastError = .decodingFailed(error.localizedDescription)
            #if DEBUG
            print("⚠️ [CatalogRepository] Непредвиденная ошибка: \(error)")
            #endif
        }
    }

    /// Полная загрузка и парсинг каталога
    public func loadCatalog() throws {
        let data = try fetchCatalogData()
        let decoder = JSONDecoder()

        do {
            let container = try decoder.decode(CatalogContainer.self, from: data)

            guard !container.chapters.isEmpty, !container.adventures.isEmpty else {
                throw CatalogError.emptyCatalog
            }

            // Обновляем списки
            self.chapters = container.chapters.sorted { $0.order < $1.order }
            self.adventures = container.adventures.sorted { $0.orderInChapter < $1.orderInChapter }
            self.achievements = container.achievements

            // Обновляем быстрые словари O(1)
            self.chaptersMap = Dictionary(uniqueKeysWithValues: chapters.map { ($0.id, $0) })
            self.adventuresMap = Dictionary(uniqueKeysWithValues: adventures.map { ($0.id, $0) })
            self.achievementsMap = Dictionary(uniqueKeysWithValues: achievements.map { ($0.id, $0) })

            self.isLoaded = true
            self.lastError = nil
        } catch let decodingError as DecodingError {
            let reason = describeDecodingError(decodingError)
            throw CatalogError.decodingFailed(reason)
        } catch {
            throw CatalogError.decodingFailed(error.localizedDescription)
        }
    }

    // MARK: - Методы CatalogRepositoryProtocol

    /// Получить приключение по его ID
    public func getAdventure(by id: String) -> AdventureDefinition? {
        adventuresMap[id]
    }

    /// Получить главу по её ID
    public func getChapter(by id: String) -> ChapterDefinition? {
        chaptersMap[id]
    }

    /// Получить значок-достижение по его ID
    public func getAchievement(by id: String) -> AchievementDefinition? {
        achievementsMap[id]
    }

    /// Получить все приключения, отсортированные по порядку в главе
    public func getAllAdventures() -> [AdventureDefinition] {
        adventures
    }

    /// Получить все главы, отсортированные по порядку
    public func getAllChapters() -> [ChapterDefinition] {
        chapters
    }

    /// Получить все доступные достижения
    public func getAllAchievements() -> [AchievementDefinition] {
        achievements
    }

    /// Получить все приключения конкретной главы
    public func getAdventures(for chapterId: String) -> [AdventureDefinition] {
        adventures
            .filter { $0.chapterId == chapterId }
            .sorted { $0.orderInChapter < $1.orderInChapter }
    }

    // MARK: - Удобные алиасы
    public func adventure(by id: String) -> AdventureDefinition? {
        getAdventure(by: id)
    }

    public func chapter(by id: String) -> ChapterDefinition? {
        getChapter(by: id)
    }

    public func adventures(for chapterId: String) -> [AdventureDefinition] {
        getAdventures(for: chapterId)
    }

    // MARK: - Поиск источника данных (Bundle / Filesystem)

    private func fetchCatalogData() throws -> Data {
        let fileName = "adventures_catalog_ru"
        let fileExtension = "json"

        // 1. Попытка загрузить из Bundle.main
        if let bundleUrl = Bundle.main.url(forResource: fileName, withExtension: fileExtension),
           let data = try? Data(contentsOf: bundleUrl) {
            return data
        }

        // 2. Попытка загрузить из Bundle.module (если библиотека подключена через Swift Package Manager)
        #if SWIFT_PACKAGE
        if let moduleUrl = Bundle.module.url(forResource: fileName, withExtension: fileExtension),
           let data = try? Data(contentsOf: moduleUrl) {
            return data
        }
        #endif

        // 3. Попытка загрузить из переменной окружения
        if let envPath = ProcessInfo.processInfo.environment["CHOOSE_A_VIBE_CATALOG_PATH"],
           FileManager.default.fileExists(atPath: envPath),
           let data = try? Data(contentsOf: URL(fileURLWithPath: envPath)) {
            return data
        }

        // 4. Локальные относительные пути для тестов, CLI и SwiftUI Previews
        let candidatePaths = [
            "Resources/\(fileName).\(fileExtension)",
            "ChooseAVibe/Resources/\(fileName).\(fileExtension)",
            "../Resources/\(fileName).\(fileExtension)",
            "../../Resources/\(fileName).\(fileExtension)",
            "C:/Users/Mila/Desktop/BestStart/projects/ChooseaVibe/ChooseAVibe/Resources/\(fileName).\(fileExtension)"
        ]

        for path in candidatePaths {
            if FileManager.default.fileExists(atPath: path),
               let data = try? Data(contentsOf: URL(fileURLWithPath: path)) {
                return data
            }
        }

        throw CatalogError.fileNotFound("\(fileName).\(fileExtension)")
    }

    // MARK: - Детализация ошибок DecodingError

    private func describeDecodingError(_ error: DecodingError) -> String {
        switch error {
        case .typeMismatch(let type, let context):
            return "Несоответствие типа \(type) по пути: \(context.codingPath.map(\.stringValue).joined(separator: ".")) (\(context.debugDescription))"
        case .valueNotFound(let type, let context):
            return "Отсутствует значение типа \(type) по пути: \(context.codingPath.map(\.stringValue).joined(separator: "."))"
        case .keyNotFound(let key, let context):
            return "Отсутствует ключ '\(key.stringValue)' по пути: \(context.codingPath.map(\.stringValue).joined(separator: "."))"
        case .dataCorrupted(let context):
            return "Повреждены данные JSON по пути: \(context.codingPath.map(\.stringValue).joined(separator: ".")) (\(context.debugDescription))"
        @unknown default:
            return error.localizedDescription
        }
    }
}

// MARK: - Экземпляр для SwiftUI Previews

extension CatalogRepository {
    /// Быстрый репозиторий для SwiftUI Previews и Unit-тестов
    public static var preview: CatalogRepository {
        let repo = CatalogRepository(autoLoad: true)
        return repo
    }
}
