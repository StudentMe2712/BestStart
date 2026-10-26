import XCTest
#if SWIFT_PACKAGE
@testable import ChooseAVibeCore
#else
@testable import ChooseAVibe
#endif

final class ChooseAVibeTests: XCTestCase {
    func testVibeTagCasesAndProperties() {
        XCTAssertEqual(VibeTag.allCases.count, 5)
        XCTAssertEqual(VibeTag.romantic.rawValue, "Романтика")
        XCTAssertEqual(VibeTag.romantic.iconName, "heart.fill")
        XCTAssertEqual(VibeTag.playful.rawValue, "Игривое")
        XCTAssertEqual(VibeTag.playful.iconName, "sparkles")
    }

    func testProgressionEngineLevels() {
        let levels = ProgressionEngine.allLevels
        XCTAssertEqual(levels.count, 5)
        XCTAssertEqual(levels[0].requiredXP, 0)
        XCTAssertEqual(levels[4].requiredXP, 500)
    }

    func testCatalogRepositoryInit() {
        let repository = CatalogRepository(autoLoad: false)
        XCTAssertFalse(repository.isLoaded)
        XCTAssertTrue(repository.chapters.isEmpty)
        XCTAssertTrue(repository.adventures.isEmpty)
    }
}
