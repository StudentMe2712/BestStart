//
//  DietaryTag.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

import SwiftUI

/// Dietary and allergen tags for culinary curation
public enum DietaryTag: String, Codable, CaseIterable, Identifiable, Hashable, Sendable {
    case glutenFree = "Gluten-Free"
    case vegan = "Vegan"
    case vegetarian = "Vegetarian"
    case halal = "Halal"
    case nutFree = "Nut-Free"
    case dairyFree = "Dairy-Free"
    case pescatarian = "Pescatarian"

    public var id: String {
        rawValue
    }

    /// SF Symbol representing the dietary category
    public var icon: String {
        switch self {
        case .glutenFree:
            return "slash.circle"
        case .vegan:
            return "leaf.fill"
        case .vegetarian:
            return "leaf"
        case .halal:
            return "moon.stars.fill"
        case .nutFree:
            return "allergens"
        case .dairyFree:
            return "drop.slash"
        case .pescatarian:
            return "fish.fill"
        }
    }

    /// Color name corresponding to the design system tokens
    public var colorName: String {
        switch self {
        case .glutenFree:
            return "champagneAccent"
        case .vegan:
            return "sageGreen"
        case .vegetarian:
            return "sageGreen"
        case .halal:
            return "burgundySubAccent"
        case .nutFree:
            return "champagneAccent"
        case .dairyFree:
            return "slateBlue"
        case .pescatarian:
            return "oceanTeal"
        }
    }
}

// MARK: - Interactive Preview

#Preview("DietaryTag Catalog") {
    ZStack {
        Color(red: 0.055, green: 0.063, blue: 0.075).ignoresSafeArea()

        ScrollView {
            VStack(alignment: .leading, spacing: 14) {
                Text("DIETARY & ALLERGEN TAGS")
                    .font(.system(size: 11, weight: .bold, design: .monospaced))
                    .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))

                ForEach(DietaryTag.allCases) { tag in
                    HStack(spacing: 12) {
                        Image(systemName: tag.icon)
                            .font(.system(size: 16))
                            .frame(width: 32, height: 32)
                            .background(Color(red: 0.15, green: 0.17, blue: 0.21))
                            .clipShape(Circle())
                            .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))

                        VStack(alignment: .leading, spacing: 2) {
                            Text(tag.rawValue)
                                .font(.system(size: 14, weight: .semibold))
                                .foregroundStyle(Color.white)
                            Text(tag.colorName)
                                .font(.system(size: 11, design: .monospaced))
                                .foregroundStyle(Color.gray)
                        }
                        Spacer()
                    }
                    .padding(12)
                    .background(Color(red: 0.086, green: 0.098, blue: 0.122))
                    .clipShape(RoundedRectangle(cornerRadius: 12))
                }
            }
            .padding()
        }
    }
}
