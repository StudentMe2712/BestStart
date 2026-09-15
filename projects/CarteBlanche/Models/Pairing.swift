//
//  Pairing.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

import Foundation
import SwiftUI

/// Sommelier or mixologist recommended beverage pairing
public struct Pairing: Codable, Hashable, Identifiable, Sendable {
    public var id: String
    public var name: String
    public var type: String
    public var notes: String
    public var temperature: String?
    public var producer: String?
    public var vintage: String?

    public init(
        id: String = UUID().uuidString,
        name: String,
        type: String,
        notes: String,
        temperature: String? = nil,
        producer: String? = nil,
        vintage: String? = nil
    ) {
        self.id = id
        self.name = name
        self.type = type
        self.notes = notes
        self.temperature = temperature
        self.producer = producer
        self.vintage = vintage
    }
}

// MARK: - Interactive Preview

#Preview("Pairing Models Showcase") {
    ZStack {
        Color(red: 0.055, green: 0.063, blue: 0.075).ignoresSafeArea()

        ScrollView {
            VStack(alignment: .leading, spacing: 14) {
                Text("SOMMELIER PAIRINGS")
                    .font(.system(size: 11, weight: .bold, design: .monospaced))
                    .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))

                let samplePairings: [Pairing] = [
                    Pairing(name: "Sancerre 'Les Monts Damnés'", type: "White Wine", notes: "Crisp flinty minerality.", temperature: "9°C", producer: "Domaine François Cotat", vintage: "2021"),
                    Pairing(name: "Champagne Blanc de Blancs", type: "Champagne", notes: "Toasted brioche and persistent effervescence.", temperature: "8°C", producer: "Pierre Péters", vintage: "NV")
                ]

                ForEach(samplePairings) { pair in
                    VStack(alignment: .leading, spacing: 6) {
                        HStack {
                            Text(pair.type.uppercased())
                                .font(.system(size: 10, weight: .bold, design: .monospaced))
                                .foregroundStyle(Color(red: 0.55, green: 0.18, blue: 0.22))
                            Spacer()
                            if let temp = pair.temperature {
                                Text(temp)
                                    .font(.system(size: 11, weight: .semibold, design: .monospaced))
                                    .foregroundStyle(Color(red: 0.90, green: 0.66, blue: 0.38))
                            }
                        }
                        Text(pair.name)
                            .font(.system(.headline, design: .serif, weight: .bold))
                            .foregroundStyle(Color(red: 0.96, green: 0.96, blue: 0.97))
                        if let prod = pair.producer {
                            Text(prod)
                                .font(.system(size: 12))
                                .foregroundStyle(Color.gray)
                        }
                        Text(pair.notes)
                            .font(.system(size: 12))
                            .foregroundStyle(Color.gray.opacity(0.8))
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
