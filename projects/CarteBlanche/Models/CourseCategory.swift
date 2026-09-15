//
//  CourseCategory.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

import SwiftUI

/// Gastronomic course progression categories
public enum CourseCategory: String, Codable, CaseIterable, Identifiable, Hashable, Sendable {
    case prelude = "Prelude"
    case main = "Main Courses"
    case garden = "Garden"
    case dessert = "Desserts & Fromage"
    case cellar = "Cellar"

    public var id: String {
        rawValue
    }

    /// Elegant French & Russian haute-cuisine subtitle
    public var subtitle: String {
        switch self {
        case .prelude:
            return "Amuse-bouche & Hors d'œuvres • Закуски и аперитивы"
        case .main:
            return "Plats Principaux & Signature Cuts • Основные подачи"
        case .garden:
            return "Cuisine Végétale & Micro-seasons • Растительные шедевры"
        case .dessert:
            return "Douceurs & Affinage • Десерты и сырная тарелка"
        case .cellar:
            return "Grands Crus & Mixologie • Винная карта и бар"
        }
    }

    /// SF Symbol representing the course category
    public var systemImage: String {
        switch self {
        case .prelude:
            return "sparkles"
        case .main:
            return "flame.fill"
        case .garden:
            return "leaf.fill"
        case .dessert:
            return "birthday.cake.fill"
        case .cellar:
            return "wineglass.fill"
        }
    }
}

// MARK: - Interactive Preview

#Preview("CourseCategory Progression") {
    ZStack {
        Color(red: 0.055, green: 0.063, blue: 0.075).ignoresSafeArea()

        ScrollView {
            VStack(alignment: .leading, spacing: 14) {
                Text("GASTRONOMIC COURSE PROGRESSION")
                    .font(.system(size: 11, weight: .bold, design: .monospaced))
                    .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))

                ForEach(CourseCategory.allCases) { category in
                    HStack(alignment: .top, spacing: 12) {
                        Image(systemName: category.systemImage)
                            .font(.system(size: 18))
                            .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))
                            .frame(width: 36, height: 36)
                            .background(Color(red: 0.15, green: 0.17, blue: 0.21))
                            .clipShape(Circle())

                        VStack(alignment: .leading, spacing: 4) {
                            Text(category.rawValue)
                                .font(.system(.headline, design: .serif, weight: .bold))
                                .foregroundStyle(Color(red: 0.96, green: 0.96, blue: 0.97))
                            Text(category.subtitle)
                                .font(.system(size: 12))
                                .foregroundStyle(Color.gray)
                        }
                        Spacer()
                    }
                    .padding(14)
                    .background(Color(red: 0.086, green: 0.098, blue: 0.122))
                    .clipShape(RoundedRectangle(cornerRadius: 12))
                }
            }
            .padding()
        }
    }
}
