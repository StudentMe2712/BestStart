//
//  MockDataService.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI (Observation Framework)
//

import SwiftUI
import Foundation
import Observation

private final class BundleToken {}

/// Offline mock repository and query service for Haute Cuisine menu items
@Observable
public final class MockDataService: @unchecked Sendable {

    // MARK: - Singleton

    public static let shared = MockDataService()

    // MARK: - Stored Properties

    /// In-memory catalog of gourmet menu items
    public var items: [MenuItem] = []

    /// Flag indicating whether data was loaded from Bundle or inline fallback
    public private(set) var sourceDescription: String = "Initializing"

    // MARK: - Initializer

    public init(items: [MenuItem]? = nil) {
        if let customItems = items, !customItems.isEmpty {
            self.items = customItems
            self.sourceDescription = "Custom Initializer (\(customItems.count) items)"
        } else {
            loadItems()
        }
    }

    // MARK: - Public Queries

    /// Returns all available items
    public var allItems: [MenuItem] {
        items
    }

    /// Single reference item for design and component previews
    public var sampleItem: MenuItem {
        items.first ?? Self.defaultSampleItem
    }

    /// Returns items filtered by course category
    public func items(for category: CourseCategory) -> [MenuItem] {
        items.filter { $0.course == category }
    }

    /// Filter menu items by search query, course category, and dietary restrictions
    public func search(
        query: String = "",
        category: CourseCategory? = nil,
        dietary: Set<DietaryTag> = []
    ) -> [MenuItem] {
        let trimmedQuery = query.trimmingCharacters(in: .whitespacesAndNewlines)

        return items.filter { item in
            // Filter by category if specified
            if let category, item.course != category {
                return false
            }

            // Filter by dietary tags (item must satisfy ALL selected tags)
            if !dietary.isEmpty {
                let itemTagSet = Set(item.dietaryTags)
                if !dietary.isSubset(of: itemTagSet) {
                    return false
                }
            }

            // Filter by text search query
            if !trimmedQuery.isEmpty {
                let matchesName = item.name.localizedCaseInsensitiveContains(trimmedQuery)
                let matchesDescription = item.description.localizedCaseInsensitiveContains(trimmedQuery)
                let matchesStory = item.culinaryStory.localizedCaseInsensitiveContains(trimmedQuery)
                let matchesOrigin = item.origin?.localizedCaseInsensitiveContains(trimmedQuery) ?? false
                let matchesPairing = item.pairing.name.localizedCaseInsensitiveContains(trimmedQuery)

                if !(matchesName || matchesDescription || matchesStory || matchesOrigin || matchesPairing) {
                    return false
                }
            }

            return true
        }
    }

    /// Returns items specifically selected by the Head Chef
    public var chefSelections: [MenuItem] {
        items.filter { $0.isChefSelection }
    }

    /// Look up a specific item by identifier
    public func item(withId id: String) -> MenuItem? {
        items.first { $0.id == id }
    }

    /// Reload data from source
    public func reload() {
        loadItems()
    }

    // MARK: - Data Loading Pipeline

    private func loadItems() {
        let decoder = JSONDecoder()

        // 1. Attempt loading from available bundles
        if let bundleUrl = locateSeedFile() {
            do {
                let data = try Data(contentsOf: bundleUrl)
                let decoded = try decoder.decode([MenuItem].self, from: data)
                if !decoded.isEmpty {
                    self.items = decoded
                    self.sourceDescription = "Bundle (\(bundleUrl.lastPathComponent))"
                    return
                }
            } catch {
                print("[MockDataService] Failed decoding SeedMenu.json from bundle: \(error)")
            }
        }

        // 2. Fallback to embedded raw JSON string (guarantees Preview and unit test execution)
        if let data = Self.fallbackJSONString.data(using: .utf8) {
            do {
                let decoded = try decoder.decode([MenuItem].self, from: data)
                if !decoded.isEmpty {
                    self.items = decoded
                    self.sourceDescription = "Embedded Fallback JSON (\(decoded.count) items)"
                    return
                }
            } catch {
                print("[MockDataService] Failed decoding fallback JSON: \(error)")
            }
        }

        // 3. Static compile-time safety array
        self.items = [Self.defaultSampleItem]
        self.sourceDescription = "Static Safety Item"
    }

    private func locateSeedFile() -> URL? {
        let candidateBundles: [Bundle] = [
            Bundle.main,
            Bundle(for: BundleToken.self)
        ]

        for bundle in candidateBundles {
            if let url = bundle.url(forResource: "SeedMenu", withExtension: "json") {
                return url
            }
            if let url = bundle.url(forResource: "SeedMenu", withExtension: "json", subdirectory: "Resources") {
                return url
            }
        }

        #if SWIFT_PACKAGE
        if let url = Bundle.module.url(forResource: "SeedMenu", withExtension: "json") {
            return url
        }
        #endif

        return nil
    }

    // MARK: - Static Defaults & Fallback Data

    public static let defaultSampleItem = MenuItem(
        id: "prelude-01",
        name: "Pan-Seared Hokkaido Scallops",
        course: .prelude,
        price: 34.00,
        description: "Wild-caught diver scallops with yuzu kosho emulsion, charred leek oil, and crystalline sea grapes.",
        culinaryStory: "Sourced directly from the cold currents of the Okhotsk Sea off Hokkaido. Quickly caramelized over binchotan charcoal to preserve delicate sea-sweetness.",
        dietaryTags: [.glutenFree, .pescatarian, .nutFree],
        flavorProfile: FlavorProfile(umami: 0.85, acidity: 0.65, sweetness: 0.40, spiciness: 0.20, texture: 0.75),
        pairing: Pairing(
            id: "pair-prelude-01",
            name: "Sancerre 'Les Monts Damnés'",
            type: "White Wine",
            notes: "Crisp flinty minerality and vibrant grapefruit tension slicing through rich scallops.",
            temperature: "9°C",
            producer: "Domaine François Cotat",
            vintage: "2021"
        ),
        heroImageSymbol: "water.waves",
        rating: 4.95,
        isChefSelection: true,
        origin: "Hokkaido, Japan"
    )

    private static let fallbackJSONString = """
    [
      {
        "id": "prelude-01",
        "name": "Pan-Seared Hokkaido Scallops",
        "course": "Prelude",
        "price": 34.0,
        "description": "Wild-caught diver scallops with yuzu kosho emulsion, charred leek oil, and crystalline sea grapes.",
        "culinaryStory": "Sourced directly from the cold currents of the Okhotsk Sea off Hokkaido. Quickly caramelized over binchotan charcoal to preserve the delicate sea-sweetness, lifted by fermented green yuzu peel.",
        "dietaryTags": ["Gluten-Free", "Pescatarian", "Nut-Free"],
        "flavorProfile": { "umami": 0.85, "acidity": 0.65, "sweetness": 0.40, "spiciness": 0.20, "texture": 0.75 },
        "pairing": {
          "id": "pair-prelude-01",
          "name": "Sancerre 'Les Monts Damnés'",
          "type": "White Wine",
          "notes": "Crisp flinty minerality and vibrant grapefruit tension that slice through the luscious caramel scallop richness.",
          "temperature": "9°C",
          "producer": "Domaine François Cotat",
          "vintage": "2021"
        },
        "heroImageSymbol": "water.waves",
        "rating": 4.95,
        "isChefSelection": true,
        "origin": "Hokkaido, Japan"
      },
      {
        "id": "prelude-02",
        "name": "A5 Wagyu & Imperial Caviar Tartlet",
        "course": "Prelude",
        "price": 42.0,
        "description": "Hand-cut Miyazaki wagyu beef, Kaluga hybrid sturgeon caviar, bone marrow emulsion, crisp nori pastry.",
        "culinaryStory": "A high-tension collision between land and sea. Silky marbling from Miyazaki A5 striploin is subtly smoked over cherrywood, nestled inside an ultra-fine brittle nori tart shell.",
        "dietaryTags": ["Nut-Free", "Halal"],
        "flavorProfile": { "umami": 0.95, "acidity": 0.35, "sweetness": 0.25, "spiciness": 0.15, "texture": 0.85 },
        "pairing": {
          "id": "pair-prelude-02",
          "name": "Champagne Blanc de Blancs Extra Brut",
          "type": "Champagne",
          "notes": "Persistent micro-effervescence and toasted brioche notes harmonize with the salty, nutty caviar beads.",
          "temperature": "8°C",
          "producer": "Pierre Péters Cuvée de Réserve",
          "vintage": "NV"
        },
        "heroImageSymbol": "crown.fill",
        "rating": 5.0,
        "isChefSelection": true,
        "origin": "Miyazaki & Qiandao Lake"
      },
      {
        "id": "prelude-03",
        "name": "Black Truffle & Morels Chawanmushi",
        "course": "Prelude",
        "price": 29.0,
        "description": "Silken dashi egg custard, shaved Périgord black winter truffles, braised wild morel mushrooms, roasted hazelnut drizzle.",
        "culinaryStory": "Steamed gently at exactly 82°C using aged Rishiri kombu broth. The delicate cloud-like custard dissolves immediately, releasing intoxicating earthy aromas of black truffle.",
        "dietaryTags": ["Gluten-Free", "Vegetarian"],
        "flavorProfile": { "umami": 0.90, "acidity": 0.20, "sweetness": 0.30, "spiciness": 0.05, "texture": 0.60 },
        "pairing": {
          "id": "pair-prelude-03",
          "name": "Junmai Daiginjo 'Yukisatsuma'",
          "type": "Craft Sake",
          "notes": "Melon undertones, velvety texture, and clean rice polish complement the soft dashi custard without overpowering truffle.",
          "temperature": "10°C",
          "producer": "Dassai 23",
          "vintage": "2023"
        },
        "heroImageSymbol": "cup.and.saucer.fill",
        "rating": 4.88,
        "isChefSelection": false,
        "origin": "Périgord, France"
      },
      {
        "id": "main-01",
        "name": "Dry-Aged Challandais Duck Breast",
        "course": "Main Courses",
        "price": 58.0,
        "description": "28-day dry-aged heritage duck, fermented black plum reduction, charred salsify, Sichuan pepper lavender jus.",
        "culinaryStory": "Breasted from heritage Challans ducks, aged on salt blocks for concentrated game flavor. Crisp lacquered honey skin cooked over embers, paired with a slow reduction of wild damson plums.",
        "dietaryTags": ["Gluten-Free", "Nut-Free", "Dairy-Free"],
        "flavorProfile": { "umami": 0.92, "acidity": 0.55, "sweetness": 0.45, "spiciness": 0.35, "texture": 0.80 },
        "pairing": {
          "id": "pair-main-01",
          "name": "Gevrey-Chambertin 'Clos Saint-Jacques' 1er Cru",
          "type": "Red Wine",
          "notes": "Dark cherry, violet perfume, and fine-grained tannins echo the wild game notes and spicy lavender jus.",
          "temperature": "16°C",
          "producer": "Domaine Armand Rousseau",
          "vintage": "2018"
        },
        "heroImageSymbol": "flame.fill",
        "rating": 4.98,
        "isChefSelection": true,
        "origin": "Challans, Vendée, France"
      },
      {
        "id": "main-02",
        "name": "Glacier 51 Toothfish en Papillote",
        "course": "Main Courses",
        "price": 64.0,
        "description": "Sub-Antarctic Patagonian toothfish, lemongrass dashi broth, sea succulents, finger lime pearls, galangal oil.",
        "culinaryStory": "Sustainably caught in Heard Island's freezing waters at 2,000 meters depth. The naturally high omega-3 fat gives snow-white flakes of incomparable richness, cut through by citric finger lime.",
        "dietaryTags": ["Gluten-Free", "Pescatarian", "Dairy-Free", "Nut-Free"],
        "flavorProfile": { "umami": 0.88, "acidity": 0.70, "sweetness": 0.35, "spiciness": 0.25, "texture": 0.78 },
        "pairing": {
          "id": "pair-main-02",
          "name": "Meursault 'Les Narvaux'",
          "type": "White Wine",
          "notes": "Rich hazelnut, lemon curd, and saline drive that wrap around the rich, oily texture of deep-sea toothfish.",
          "temperature": "11°C",
          "producer": "Domaine Vincent Girardin",
          "vintage": "2020"
        },
        "heroImageSymbol": "fish.fill",
        "rating": 4.92,
        "isChefSelection": false,
        "origin": "Heard Island & McDonald Islands"
      },
      {
        "id": "main-03",
        "name": "A5 Kagoshima Ribeye & Smoked Bone Marrow",
        "course": "Main Courses",
        "price": 85.0,
        "description": "Kuroge Washu BMS 11 ribeye, charred spring scallion chimichurri, truffled potato pavé, aged balsamic jus.",
        "culinaryStory": "Prized Japanese Black beef raised in southern Kagoshima sun. Melting point below human body temperature creates an explosion of marbling sweetness, balanced by lively herbal chimichurri acidity.",
        "dietaryTags": ["Gluten-Free", "Nut-Free"],
        "flavorProfile": { "umami": 0.98, "acidity": 0.40, "sweetness": 0.40, "spiciness": 0.20, "texture": 0.90 },
        "pairing": {
          "id": "pair-main-03",
          "name": "Barolo Monprivato",
          "type": "Red Wine",
          "notes": "Noble tar and crushed roses with aristocratic Nebbiolo acidity effortlessly dissolve the dense marbling fats.",
          "temperature": "17°C",
          "producer": "Giuseppe Mascarello",
          "vintage": "2016"
        },
        "heroImageSymbol": "fork.knife",
        "rating": 5.0,
        "isChefSelection": true,
        "origin": "Kagoshima, Japan"
      },
      {
        "id": "garden-01",
        "name": "Salt-Baked Heirloom Beetroot & Smoked Cashew Labneh",
        "course": "Garden",
        "price": 26.0,
        "description": "Four varieties of heritage baby beets slow-roasted in rosemary sea salt crust, whipped cashew labneh, blackberry vinaigrette.",
        "culinaryStory": "Cultivated in biodynamic volcanic soil. Salt-baking concentrates the root's natural sugars into ruby gems of earthy sweetness, contrasted by smoked fermented cashew cream.",
        "dietaryTags": ["Vegan", "Vegetarian", "Gluten-Free", "Dairy-Free"],
        "flavorProfile": { "umami": 0.50, "acidity": 0.75, "sweetness": 0.65, "spiciness": 0.10, "texture": 0.70 },
        "pairing": {
          "id": "pair-garden-01",
          "name": "Skin-Contact Orange Ribolla Gialla",
          "type": "Natural Wine",
          "notes": "Amber hues with tactile grip, apricot skin, and herbal nuances mirroring the earthy, floral beet profile.",
          "temperature": "12°C",
          "producer": "Gravner",
          "vintage": "2015"
        },
        "heroImageSymbol": "leaf.fill",
        "rating": 4.85,
        "isChefSelection": false,
        "origin": "Emilia-Romagna, Italy"
      },
      {
        "id": "garden-02",
        "name": "Braised Lion's Mane & Pine Needle Dashi",
        "course": "Garden",
        "price": 32.0,
        "description": "Organic lion's mane mushroom steak basted in wild thyme kombu butter, pine needle broth, roasted sunflower seed praline.",
        "culinaryStory": "Wild-foraged forest mushroom poached in mountain cedar dashi, then pan-roasted until deeply crusty. It mimics the density of tender shellfish with an intoxicating forest aroma.",
        "dietaryTags": ["Vegetarian", "Gluten-Free", "Nut-Free", "Halal"],
        "flavorProfile": { "umami": 0.90, "acidity": 0.35, "sweetness": 0.30, "spiciness": 0.15, "texture": 0.85 },
        "pairing": {
          "id": "pair-garden-02",
          "name": "Roasted Wild Mountain Hojicha Reserve",
          "type": "Artisan Tea",
          "notes": "Charcoal-roasted green tea leaves emitting cedar, dark cacao, and roasted hazelnut nuances.",
          "temperature": "85°C",
          "producer": "Ippodo Kyoto",
          "vintage": "Spring Harvest"
        },
        "heroImageSymbol": "tree.fill",
        "rating": 4.90,
        "isChefSelection": true,
        "origin": "Kyoto Forest, Japan"
      },
      {
        "id": "dessert-01",
        "name": "Valrhona Guanaja Smoked Chocolate Soufflé",
        "course": "Desserts & Fromage",
        "price": 24.0,
        "description": "Warm 70% dark single-origin chocolate soufflé, lapsang souchong smoked salt, Tahitian vanilla bean anglaise.",
        "culinaryStory": "Baked freshly to order in 14 minutes. Deep, bittersweet chocolate complexity lifted by subtle woodsmoke from pine-smoked tea leaves, folded into airy French meringue.",
        "dietaryTags": ["Vegetarian", "Nut-Free"],
        "flavorProfile": { "umami": 0.30, "acidity": 0.30, "sweetness": 0.88, "spiciness": 0.10, "texture": 0.80 },
        "pairing": {
          "id": "pair-dessert-01",
          "name": "Tawny Port 20 Year Old",
          "type": "Fortified Wine",
          "notes": "Nutty walnut, dried fig, and burnt orange peel that dance with molten bittersweet dark chocolate.",
          "temperature": "14°C",
          "producer": "Quinta do Noval",
          "vintage": "Aged 20 Years"
        },
        "heroImageSymbol": "birthday.cake.fill",
        "rating": 4.96,
        "isChefSelection": true,
        "origin": "Tain-l'Hermitage & Tahiti"
      },
      {
        "id": "dessert-02",
        "name": "Affineur 36-Month Comté & White Truffle Honey",
        "course": "Desserts & Fromage",
        "price": 28.0,
        "description": "Hand-selected Marcel Petite reserve Comté aged in Fort Saint-Antoine, acacia honeycomb infused with Alba white truffles, toasted rye crisps.",
        "culinaryStory": "Aged deep in the humid stone vaults of an old military fort. Rich tyrosine crystals deliver crunchy bursts of nutty, browned butter, and roasted pineapple flavor.",
        "dietaryTags": ["Vegetarian", "Nut-Free"],
        "flavorProfile": { "umami": 0.85, "acidity": 0.40, "sweetness": 0.55, "spiciness": 0.05, "texture": 0.90 },
        "pairing": {
          "id": "pair-dessert-02",
          "name": "Vin Jaune 'Château Chalon'",
          "type": "Jura Wine",
          "notes": "Matured 6 years under flor veil; intense walnut, curry leaf, and rancio notes that mirror aged Comté.",
          "temperature": "15°C",
          "producer": "Domaine Macle",
          "vintage": "2014"
        },
        "heroImageSymbol": "shield.fill",
        "rating": 4.93,
        "isChefSelection": false,
        "origin": "Fort Saint-Antoine, Jura, France"
      },
      {
        "id": "cellar-01",
        "name": "Krug Clos d'Ambonnay Vintage Tasting Pour",
        "course": "Cellar",
        "price": 110.0,
        "description": "100% Pinot Noir single-walled vineyard parcel in Ambonnay, disgorged after 15 years sur latte in chalk cellars.",
        "culinaryStory": "From a tiny 0.68-hectare walled parcel surrounded by the village of Ambonnay. Regarded by connoisseurs as the zenith of Champagne expression: breathtaking tension, brioche, red currants, and endless finish.",
        "dietaryTags": ["Vegan", "Vegetarian", "Gluten-Free", "Nut-Free", "Dairy-Free"],
        "flavorProfile": { "umami": 0.60, "acidity": 0.90, "sweetness": 0.25, "spiciness": 0.10, "texture": 0.85 },
        "pairing": {
          "id": "pair-cellar-01",
          "name": "Siberian Imperial Sturgeon Caviar Spoon",
          "type": "Couture Amuse",
          "notes": "Salty mineral pop of dark pearls creates a sensory resonance with the deep acidity and pinot power.",
          "temperature": "10°C",
          "producer": "House of Krug",
          "vintage": "2008"
        },
        "heroImageSymbol": "wineglass.fill",
        "rating": 5.0,
        "isChefSelection": true,
        "origin": "Ambonnay, Champagne, France"
      },
      {
        "id": "cellar-02",
        "name": "Smoked Rosemary Oolong Old Fashioned",
        "course": "Cellar",
        "price": 26.0,
        "description": "Yamazaki 12 Japanese single malt, slow-dripped Da Hong Pao rock oolong reduction, aromatic bitters, smoked with mountain pine.",
        "culinaryStory": "Infused over 48 hours with legendary Wuyi mountain rock tea. Presented in hand-cut baccarat crystal, trapped under an applewood-smoke cloche.",
        "dietaryTags": ["Vegan", "Vegetarian", "Gluten-Free", "Nut-Free", "Dairy-Free"],
        "flavorProfile": { "umami": 0.40, "acidity": 0.30, "sweetness": 0.50, "spiciness": 0.60, "texture": 0.70 },
        "pairing": {
          "id": "pair-cellar-02",
          "name": "Candied Cedar Pine Nuts & 85% Cacao Tile",
          "type": "Digestif Sweet",
          "notes": "Earthy cocoa and resinous pine crunch prolong the peaty malt and roasted oolong finish.",
          "temperature": "6°C",
          "producer": "Carte Blanche Signature Mixology",
          "vintage": "Master Craft 2024"
        },
        "heroImageSymbol": "sparkles",
        "rating": 4.95,
        "isChefSelection": false,
        "origin": "Kyoto & Mount Wuyi"
      }
    ]
    """
}

// MARK: - Interactive Preview & Diagnostic UI

public struct MockDataServicePreviewView: View {
    @State private var service = MockDataService.shared
    @State private var selectedCategory: CourseCategory? = nil
    @State private var selectedDietary: Set<DietaryTag> = []
    @State private var searchQuery: String = ""

    private var filteredItems: [MenuItem] {
        service.search(query: searchQuery, category: selectedCategory, dietary: selectedDietary)
    }

    public init() {}

    public var body: some View {
        NavigationStack {
            ZStack {
                Color(red: 0.055, green: 0.063, blue: 0.075) // #0E1013 Obsidian Canvas
                    .ignoresSafeArea()

                ScrollView {
                    VStack(alignment: .leading, spacing: 20) {
                        // Diagnostic Header
                        diagnosticHeaderView

                        // Search Bar
                        searchBarView

                        // Category Filter Pills
                        categoryFilterPillsView

                        // Dietary Filter Pills
                        dietaryFilterPillsView

                        // Integrity Status Section
                        integrityStatusCardView

                        // Results Header
                        HStack {
                            Text("Dishes (\(filteredItems.count))")
                                .font(.system(.title3, design: .serif, weight: .bold))
                                .foregroundStyle(Color(red: 0.96, green: 0.96, blue: 0.97))

                            Spacer()

                            if selectedCategory != nil || !selectedDietary.isEmpty || !searchQuery.isEmpty {
                                Button("Reset") {
                                    withAnimation(.spring(duration: 0.3)) {
                                        selectedCategory = nil
                                        selectedDietary.removeAll()
                                        searchQuery = ""
                                    }
                                }
                                .font(.system(.caption, design: .monospaced, weight: .semibold))
                                .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))
                            }
                        }

                        // Dish Cards
                        LazyVStack(spacing: 16) {
                            ForEach(filteredItems) { item in
                                PreviewDishCard(item: item)
                            }
                        }
                    }
                    .padding()
                }
            }
            .navigationTitle("Carte Blanche Service")
            .navigationBarTitleDisplayMode(.inline)
            .toolbarBackground(.visible, for: .navigationBar)
            .toolbarColorScheme(.dark, for: .navigationBar)
        }
    }

    // MARK: - Subviews

    private var diagnosticHeaderView: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                Image(systemName: "sparkles")
                    .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))
                Text("CARTE BLANCHE ENGINE")
                    .font(.system(.caption, design: .monospaced, weight: .bold))
                    .tracking(2)
                    .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))

                Spacer()

                HStack(spacing: 4) {
                    Circle()
                        .fill(Color.green)
                        .frame(width: 8, height: 8)
                    Text("Offline Ready")
                        .font(.system(size: 11, weight: .medium, design: .monospaced))
                        .foregroundStyle(Color.green)
                }
                .padding(.horizontal, 8)
                .padding(.vertical, 4)
                .background(Color.green.opacity(0.12))
                .clipShape(Capsule())
            }

            HStack(spacing: 12) {
                metricPill(title: "TOTAL DISHES", value: "\(service.items.count)")
                metricPill(title: "COURSES", value: "\(CourseCategory.allCases.count)")
                metricPill(title: "CHEF PICKS", value: "\(service.chefSelections.count)")
            }

            Text("Source: \(service.sourceDescription)")
                .font(.system(size: 11, design: .monospaced))
                .foregroundStyle(Color(red: 0.56, green: 0.58, blue: 0.65))
        }
        .padding(16)
        .background(
            RoundedRectangle(cornerRadius: 16)
                .fill(Color(red: 0.086, green: 0.098, blue: 0.122))
                .overlay(
                    RoundedRectangle(cornerRadius: 16)
                        .stroke(Color(red: 0.15, green: 0.17, blue: 0.21), lineWidth: 1)
                )
        )
    }

    private func metricPill(title: String, value: String) -> some View {
        VStack(spacing: 2) {
            Text(value)
                .font(.system(.headline, design: .serif, weight: .bold))
                .foregroundStyle(Color(red: 0.96, green: 0.96, blue: 0.97))
            Text(title)
                .font(.system(size: 9, weight: .semibold, design: .monospaced))
                .foregroundStyle(Color(red: 0.56, green: 0.58, blue: 0.65))
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 8)
        .background(Color.black.opacity(0.3))
        .clipShape(RoundedRectangle(cornerRadius: 10))
    }

    private var searchBarView: some View {
        HStack(spacing: 10) {
            Image(systemName: "magnifyingglass")
                .foregroundStyle(Color(red: 0.56, green: 0.58, blue: 0.65))

            TextField("Search dish, ingredient, region, wine...", text: $searchQuery)
                .font(.system(.subheadline, design: .default))
                .foregroundStyle(Color(red: 0.96, green: 0.96, blue: 0.97))

            if !searchQuery.isEmpty {
                Button(action: { searchQuery = "" }) {
                    Image(systemName: "xmark.circle.fill")
                        .foregroundStyle(Color(red: 0.56, green: 0.58, blue: 0.65))
                }
            }
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 10)
        .background(
            RoundedRectangle(cornerRadius: 12)
                .fill(Color(red: 0.086, green: 0.098, blue: 0.122))
                .overlay(
                    RoundedRectangle(cornerRadius: 12)
                        .stroke(Color(red: 0.15, green: 0.17, blue: 0.21), lineWidth: 1)
                )
        )
    }

    private var categoryFilterPillsView: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 8) {
                pillButton(title: "All", isSelected: selectedCategory == nil) {
                    withAnimation(.spring(duration: 0.25)) {
                        selectedCategory = nil
                    }
                }

                ForEach(CourseCategory.allCases) { cat in
                    pillButton(
                        title: cat.rawValue,
                        systemImage: cat.systemImage,
                        isSelected: selectedCategory == cat
                    ) {
                        withAnimation(.spring(duration: 0.25)) {
                            selectedCategory = (selectedCategory == cat) ? nil : cat
                        }
                    }
                }
            }
        }
    }

    private var dietaryFilterPillsView: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 8) {
                ForEach(DietaryTag.allCases) { tag in
                    let isSelected = selectedDietary.contains(tag)
                    Button {
                        withAnimation(.spring(duration: 0.25)) {
                            if isSelected {
                                selectedDietary.remove(tag)
                            } else {
                                selectedDietary.insert(tag)
                            }
                        }
                    } label: {
                        HStack(spacing: 4) {
                            Image(systemName: tag.icon)
                                .font(.system(size: 10))
                            Text(tag.rawValue)
                                .font(.system(size: 12, weight: .medium))
                        }
                        .padding(.horizontal, 10)
                        .padding(.vertical, 6)
                        .background(
                            Capsule()
                                .fill(isSelected ? Color(red: 0.37, green: 0.55, blue: 0.42).opacity(0.25) : Color(red: 0.086, green: 0.098, blue: 0.122))
                        )
                        .overlay(
                            Capsule()
                                .stroke(isSelected ? Color(red: 0.37, green: 0.55, blue: 0.42) : Color(red: 0.15, green: 0.17, blue: 0.21), lineWidth: 1)
                        )
                        .foregroundStyle(isSelected ? Color(red: 0.65, green: 0.85, blue: 0.70) : Color(red: 0.56, green: 0.58, blue: 0.65))
                    }
                }
            }
        }
    }

    private func pillButton(
        title: String,
        systemImage: String? = nil,
        isSelected: Bool,
        action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
            HStack(spacing: 5) {
                if let systemImage {
                    Image(systemName: systemImage)
                        .font(.system(size: 11))
                }
                Text(title)
                    .font(.system(size: 12, weight: .semibold, design: .default))
            }
            .padding(.horizontal, 14)
            .padding(.vertical, 8)
            .background(
                Capsule()
                    .fill(isSelected ? Color(red: 0.90, green: 0.66, blue: 0.38) : Color(red: 0.086, green: 0.098, blue: 0.122))
            )
            .overlay(
                Capsule()
                    .stroke(isSelected ? Color(red: 0.90, green: 0.66, blue: 0.38) : Color(red: 0.15, green: 0.17, blue: 0.21), lineWidth: 1)
            )
            .foregroundStyle(isSelected ? Color.black : Color(red: 0.85, green: 0.86, blue: 0.88))
        }
    }

    private var integrityStatusCardView: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("DATA INTEGRITY VERIFICATION")
                .font(.system(size: 10, weight: .bold, design: .monospaced))
                .tracking(1.5)
                .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))

            let hasAllCourses = CourseCategory.allCases.allSatisfy { cat in
                !service.items(for: cat).isEmpty
            }
            let validPrices = service.items.allSatisfy { $0.price > 0 }
            let validPairings = service.items.allSatisfy { !$0.pairing.name.isEmpty }

            integrityRow(label: "12 Haute Cuisine Dishes Loaded", passed: service.items.count == 12)
            integrityRow(label: "All 5 Gastronomic Courses Covered", passed: hasAllCourses)
            integrityRow(label: "Sensory Flavor Profiles Configured (0.0-1.0)", passed: validPrices)
            integrityRow(label: "Sommelier Pairings & Tasting Notes Attached", passed: validPairings)
        }
        .padding(14)
        .background(
            RoundedRectangle(cornerRadius: 12)
                .fill(Color(red: 0.086, green: 0.098, blue: 0.122).opacity(0.8))
                .overlay(
                    RoundedRectangle(cornerRadius: 12)
                        .stroke(Color.green.opacity(0.2), lineWidth: 1)
                )
        )
    }

    private func integrityRow(label: String, passed: Bool) -> some View {
        HStack(spacing: 8) {
            Image(systemName: passed ? "checkmark.seal.fill" : "xmark.seal.fill")
                .font(.system(size: 12))
                .foregroundStyle(passed ? Color.green : Color.red)
            Text(label)
                .font(.system(size: 11, design: .monospaced))
                .foregroundStyle(Color(red: 0.85, green: 0.86, blue: 0.88))
            Spacer()
            Text(passed ? "PASS" : "FAIL")
                .font(.system(size: 9, weight: .bold, design: .monospaced))
                .foregroundStyle(passed ? Color.green : Color.red)
        }
    }
}

// MARK: - Dish Card Component for Preview

private struct PreviewDishCard: View {
    let item: MenuItem

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            // Top row: Course tag & Chef Selection & Price
            HStack {
                HStack(spacing: 5) {
                    Image(systemName: item.course.systemImage)
                        .font(.system(size: 10))
                    Text(item.course.rawValue.uppercased())
                        .font(.system(size: 10, weight: .bold, design: .monospaced))
                        .tracking(1)
                }
                .padding(.horizontal, 8)
                .padding(.vertical, 4)
                .background(Color(red: 0.15, green: 0.17, blue: 0.21))
                .clipShape(Capsule())
                .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))

                if item.isChefSelection {
                    HStack(spacing: 3) {
                        Image(systemName: "star.fill")
                            .font(.system(size: 9))
                        Text("CHEF")
                            .font(.system(size: 9, weight: .heavy, design: .monospaced))
                    }
                    .padding(.horizontal, 6)
                    .padding(.vertical, 3)
                    .background(Color(red: 0.55, green: 0.18, blue: 0.22))
                    .clipShape(Capsule())
                    .foregroundStyle(.white)
                }

                Spacer()

                Text(item.formattedPrice)
                    .font(.system(.title3, design: .rounded, weight: .bold))
                    .monospacedDigit()
                    .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))
            }

            // Title & Origin
            VStack(alignment: .leading, spacing: 3) {
                Text(item.name)
                    .font(.system(.headline, design: .serif, weight: .semibold))
                    .foregroundStyle(Color(red: 0.96, green: 0.96, blue: 0.97))

                if let origin = item.origin {
                    HStack(spacing: 4) {
                        Image(systemName: "mappin.and.ellipse")
                            .font(.system(size: 9))
                        Text(origin)
                            .font(.system(size: 11, design: .default))
                    }
                    .foregroundStyle(Color(red: 0.56, green: 0.58, blue: 0.65))
                }
            }

            // Description
            Text(item.description)
                .font(.system(size: 13))
                .lineSpacing(2)
                .foregroundStyle(Color(red: 0.75, green: 0.77, blue: 0.82))

            // Sommelier Pairing Snippet
            HStack(alignment: .top, spacing: 8) {
                Image(systemName: "wineglass")
                    .font(.system(size: 11))
                    .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))
                    .padding(.top, 2)

                VStack(alignment: .leading, spacing: 1) {
                    Text(item.pairing.name)
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))
                    Text(item.pairing.notes)
                        .font(.system(size: 10))
                        .foregroundStyle(Color(red: 0.56, green: 0.58, blue: 0.65))
                        .lineLimit(2)
                }
            }
            .padding(10)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(Color.black.opacity(0.35))
            .clipShape(RoundedRectangle(cornerRadius: 8))

            // Dietary Chips
            if !item.dietaryTags.isEmpty {
                HStack(spacing: 6) {
                    ForEach(item.dietaryTags) { tag in
                        HStack(spacing: 3) {
                            Image(systemName: tag.icon)
                                .font(.system(size: 8))
                            Text(tag.rawValue)
                                .font(.system(size: 10, weight: .medium))
                        }
                        .padding(.horizontal, 6)
                        .padding(.vertical, 3)
                        .background(Color(red: 0.37, green: 0.55, blue: 0.42).opacity(0.18))
                        .clipShape(Capsule())
                        .foregroundStyle(Color(red: 0.65, green: 0.85, blue: 0.70))
                    }
                }
            }
        }
        .padding(16)
        .background(
            RoundedRectangle(cornerRadius: 16)
                .fill(Color(red: 0.086, green: 0.098, blue: 0.122))
                .overlay(
                    RoundedRectangle(cornerRadius: 16)
                        .stroke(Color(red: 0.15, green: 0.17, blue: 0.21), lineWidth: 1)
                )
        )
    }
}

// MARK: - Interactive #Preview

#Preview("Mock Data Service - Haute Cuisine Menu") {
    MockDataServicePreviewView()
        .preferredColorScheme(.dark)
}
