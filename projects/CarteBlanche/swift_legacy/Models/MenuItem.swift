//
//  MenuItem.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

import Foundation
import SwiftUI

/// Core gastronomic dish or cellar item in the haute cuisine menu
public struct MenuItem: Codable, Hashable, Identifiable, Sendable {
    public var id: String
    public var name: String
    public var course: CourseCategory
    public var price: Double
    public var description: String
    public var culinaryStory: String
    public var dietaryTags: [DietaryTag]
    public var flavorProfile: FlavorProfile
    public var pairing: Pairing
    public var heroImageSymbol: String
    public var rating: Double?
    public var isChefSelection: Bool
    public var origin: String?

    public init(
        id: String = UUID().uuidString,
        name: String,
        course: CourseCategory,
        price: Double,
        description: String,
        culinaryStory: String,
        dietaryTags: [DietaryTag] = [],
        flavorProfile: FlavorProfile = .balanced,
        pairing: Pairing,
        heroImageSymbol: String = "fork.knife",
        rating: Double? = nil,
        isChefSelection: Bool = false,
        origin: String? = nil
    ) {
        self.id = id
        self.name = name
        self.course = course
        self.price = price
        self.description = description
        self.culinaryStory = culinaryStory
        self.dietaryTags = dietaryTags
        self.flavorProfile = flavorProfile
        self.pairing = pairing
        self.heroImageSymbol = heroImageSymbol
        self.rating = rating
        self.isChefSelection = isChefSelection
        self.origin = origin
    }

    /// Formatted price string for elegant luxury display
    public var formattedPrice: String {
        String(format: "€%.2f", price)
    }

    /// Formatted rating display string
    public var formattedRating: String {
        if let rating {
            return String(format: "★ %.1f", rating)
        }
        return "★ 5.0"
    }
}

// MARK: - Interactive Preview

#Preview("MenuItem Model Showcase") {
    ZStack {
        Color(red: 0.055, green: 0.063, blue: 0.075).ignoresSafeArea()

        let sample = MenuItem(
            name: "Pan-Seared Hokkaido Scallops",
            course: .prelude,
            price: 34.00,
            description: "Wild-caught diver scallops with yuzu kosho emulsion and sea grapes.",
            culinaryStory: "Caramelized over binchotan charcoal.",
            dietaryTags: [.glutenFree, .pescatarian],
            pairing: Pairing(name: "Sancerre 2021", type: "White Wine", notes: "Flinty minerality."),
            rating: 4.95,
            isChefSelection: true,
            origin: "Hokkaido, Japan"
        )

        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Text(sample.course.rawValue.uppercased())
                    .font(.system(size: 10, weight: .bold, design: .monospaced))
                    .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))
                Spacer()
                Text(sample.formattedPrice)
                    .font(.system(.title3, design: .rounded, weight: .bold))
                    .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))
            }

            Text(sample.name)
                .font(.system(.title3, design: .serif, weight: .bold))
                .foregroundStyle(Color(red: 0.96, green: 0.96, blue: 0.97))

            Text(sample.description)
                .font(.system(size: 13))
                .foregroundStyle(Color.gray)

            HStack {
                Text(sample.formattedRating)
                    .font(.system(size: 12, weight: .bold, design: .monospaced))
                    .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))
                Spacer()
                Text(sample.origin ?? "")
                    .font(.system(size: 11))
                    .foregroundStyle(Color.gray)
            }
        }
        .padding(18)
        .background(Color(red: 0.086, green: 0.098, blue: 0.122))
        .clipShape(RoundedRectangle(cornerRadius: 16))
        .padding(20)
    }
}
