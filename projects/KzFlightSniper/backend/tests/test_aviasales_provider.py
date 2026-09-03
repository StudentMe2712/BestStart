"""Unit tests for AviasalesProvider adapter."""

import asyncio
import unittest
from unittest.mock import AsyncMock, MagicMock, patch
from typing import Any, Dict, List

import httpx

from backend.core.models import FlightOffer
from backend.providers.aviasales_provider import (
    AIRLINE_IATA_MAP,
    METRO_AIRPORT_ALTERNATIVES,
    AviasalesProvider,
    _build_deep_link,
    _normalize_flight_number,
    _parse_time_and_arrival,
)


class TestAviasalesProvider(unittest.TestCase):
    """Test suite for AviasalesProvider implementation."""

    def setUp(self) -> None:
        self.provider = AviasalesProvider(token="test_token_123")

    def test_provider_name(self) -> None:
        """Verify provider_name property returns 'aviasales'."""
        self.assertEqual(self.provider.provider_name, "aviasales")

    def test_airline_iata_mapping(self) -> None:
        """Verify common airline IATA code mappings."""
        self.assertEqual(AIRLINE_IATA_MAP["KC"], "Air Astana")
        self.assertEqual(AIRLINE_IATA_MAP["IQ"], "Qazaq Air")
        self.assertEqual(AIRLINE_IATA_MAP["FS"], "FlyArystan")
        self.assertEqual(AIRLINE_IATA_MAP["DV"], "SCAT Airlines")
        self.assertEqual(AIRLINE_IATA_MAP["FZ"], "Flydubai")
        self.assertEqual(AIRLINE_IATA_MAP["EK"], "Emirates")
        self.assertEqual(AIRLINE_IATA_MAP["TK"], "Turkish Airlines")
        self.assertEqual(AIRLINE_IATA_MAP["PC"], "Pegasus Airlines")
        self.assertEqual(AIRLINE_IATA_MAP["HY"], "Uzbekistan Airways")
        self.assertEqual(AIRLINE_IATA_MAP["QR"], "Qatar Airways")

    def test_normalize_flight_number(self) -> None:
        """Verify flight number standardization logic."""
        self.assertEqual(_normalize_flight_number("FS", "7051"), "FS-7051")
        self.assertEqual(_normalize_flight_number("FS", 7051), "FS-7051")
        self.assertEqual(_normalize_flight_number("FS", "FS7051"), "FS-7051")
        self.assertEqual(_normalize_flight_number("FS", "FS-7051"), "FS-7051")
        self.assertEqual(_normalize_flight_number("KC", "853"), "KC-853")
        self.assertEqual(_normalize_flight_number("KC", "KC853"), "KC-853")
        self.assertEqual(_normalize_flight_number("DV", "713"), "DV-713")
        self.assertEqual(_normalize_flight_number("", ""), "Aviasales Flight")
        self.assertEqual(_normalize_flight_number("KC", ""), "KC")

    def test_parse_time_and_arrival(self) -> None:
        """Verify ISO timestamp parsing and arrival time calculation."""
        # Case 1: ISO timestamp with timezone + duration 105 mins (1h 45m)
        dep, arr = _parse_time_and_arrival(
            departure_raw="2026-10-15T06:45:00+06:00",
            arrival_raw=None,
            duration_minutes=105,
        )
        self.assertEqual(dep, "06:45")
        self.assertEqual(arr, "08:30")

        # Case 2: Explicit arrival time provided
        dep2, arr2 = _parse_time_and_arrival(
            departure_raw="2026-10-15T08:00:00+05:00",
            arrival_raw="2026-10-15T09:40:00+05:00",
            duration_minutes=100,
        )
        self.assertEqual(dep2, "08:00")
        self.assertEqual(arr2, "09:40")

        # Case 3: Simple time strings
        dep3, arr3 = _parse_time_and_arrival(
            departure_raw="14:00",
            arrival_raw="15:30",
            duration_minutes=None,
        )
        self.assertEqual(dep3, "14:00")
        self.assertEqual(arr3, "15:30")

        # Case 4: Missing timestamps
        dep4, arr4 = _parse_time_and_arrival(None, None, None)
        self.assertEqual(dep4, "Scheduled")
        self.assertEqual(arr4, "Scheduled")

    def test_build_deep_link(self) -> None:
        """Verify deep link building with relative and absolute URLs."""
        base = "https://www.aviasales.kz"
        link_rel = "/search/ALA1510NQZ1?t=FS123"
        self.assertEqual(
            _build_deep_link(base, link_rel, "ALA", "NQZ", "2026-10-15"),
            "https://www.aviasales.kz/search/ALA1510NQZ1?t=FS123",
        )

        link_abs = "https://www.aviasales.kz/search/custom"
        self.assertEqual(
            _build_deep_link(base, link_abs, "ALA", "NQZ", "2026-10-15"),
            "https://www.aviasales.kz/search/custom",
        )

        # Fallback link
        fallback = _build_deep_link(base, None, "ALA", "NQZ", "2026-10-15")
        self.assertEqual(fallback, "https://www.aviasales.kz/search/ALA1510NQZ1")

    def test_parse_aviasales_json_standard(self) -> None:
        """Test parsing realistic Travelpayouts v3 prices_for_dates response."""
        raw_payload = {
            "success": True,
            "data": [
                {
                    "origin": "ALA",
                    "destination": "NQZ",
                    "origin_airport": "ALA",
                    "destination_airport": "NQZ",
                    "price": 14500,
                    "airline": "FS",
                    "flight_number": "7051",
                    "departure_at": "2026-10-15T06:45:00+06:00",
                    "return_at": "",
                    "transfers": 0,
                    "duration_to": 105,
                    "duration": 105,
                    "link": "/search/ALA1510NQZ1?t=FS17604891001760495400000105ALANQZ_321d6a221f8926b5ec41ae89a3b2ae7b_14500",
                },
                {
                    "origin": "ALA",
                    "destination": "NQZ",
                    "price": 28900.0,
                    "airline": "KC",
                    "flight_number": "853",
                    "departure_at": "2026-10-15T08:00:00+06:00",
                    "transfers": 0,
                    "duration": 100,
                    "link": "/search/ALA1510NQZ1?t=KC853",
                },
                {
                    "origin": "ALA",
                    "destination": "NQZ",
                    "price": 21000,
                    "airline": "DV",
                    "flight_number": "713",
                    "departure_at": "2026-10-15T16:00:00+06:00",
                    "transfers": 1,
                    "duration": 210,
                },
            ],
            "currency": "kzt",
        }

        offers = AviasalesProvider.parse_aviasales_json(raw_payload, "ALA", "NQZ")
        self.assertEqual(len(offers), 3)

        # Offer 1: FlyArystan
        o1 = offers[0]
        self.assertEqual(o1.provider, "aviasales")
        self.assertEqual(o1.airline, "FlyArystan")
        self.assertEqual(o1.flight_number, "FS-7051")
        self.assertEqual(o1.origin, "ALA")
        self.assertEqual(o1.destination, "NQZ")
        self.assertEqual(o1.departure_time, "06:45")
        self.assertEqual(o1.arrival_time, "08:30")
        self.assertEqual(o1.price_kzt, 14500.0)
        self.assertEqual(o1.transfers_count, 0)
        self.assertEqual(o1.duration_minutes, 105)
        self.assertEqual(
            o1.deep_link,
            "https://www.aviasales.kz/search/ALA1510NQZ1?t=FS17604891001760495400000105ALANQZ_321d6a221f8926b5ec41ae89a3b2ae7b_14500",
        )
        self.assertTrue(o1.is_direct)

        # Offer 2: Air Astana
        o2 = offers[1]
        self.assertEqual(o2.airline, "Air Astana")
        self.assertEqual(o2.flight_number, "KC-853")
        self.assertEqual(o2.price_kzt, 28900.0)
        self.assertEqual(o2.departure_time, "08:00")
        self.assertEqual(o2.arrival_time, "09:40")

        # Offer 3: SCAT with 1 transfer
        o3 = offers[2]
        self.assertEqual(o3.airline, "SCAT Airlines")
        self.assertEqual(o3.flight_number, "DV-713")
        self.assertEqual(o3.transfers_count, 1)
        self.assertFalse(o3.is_direct)

    def test_parse_aviasales_json_invalid_and_empty(self) -> None:
        """Test error resilience with malformed or empty payloads."""
        self.assertEqual(AviasalesProvider.parse_aviasales_json(None, "ALA", "NQZ"), [])
        self.assertEqual(AviasalesProvider.parse_aviasales_json({}, "ALA", "NQZ"), [])
        self.assertEqual(AviasalesProvider.parse_aviasales_json({"success": False, "data": []}, "ALA", "NQZ"), [])
        self.assertEqual(AviasalesProvider.parse_aviasales_json("invalid string", "ALA", "NQZ"), [])

        invalid_items = {
            "data": [
                {"price": 0, "airline": "KC"},
                {"price": -100, "airline": "FS"},
                {"not_a_valid_flight": True},
            ]
        }
        self.assertEqual(AviasalesProvider.parse_aviasales_json(invalid_items, "ALA", "NQZ"), [])

    def test_search_flights_success_mock(self) -> None:
        """Test search_flights with mocked httpx response."""
        mock_response_data = {
            "success": True,
            "data": [
                {
                    "origin": "ALA",
                    "destination": "NQZ",
                    "price": 32000,
                    "airline": "KC",
                    "flight_number": "855",
                    "departure_at": "2026-10-15T14:00:00+06:00",
                    "transfers": 0,
                    "duration": 100,
                },
                {
                    "origin": "ALA",
                    "destination": "NQZ",
                    "price": 18000,
                    "airline": "FS",
                    "flight_number": "7053",
                    "departure_at": "2026-10-15T10:00:00+06:00",
                    "transfers": 0,
                    "duration": 100,
                },
                {
                    "origin": "ALA",
                    "destination": "NQZ",
                    "price": 15000,
                    "airline": "DV",
                    "flight_number": "715",
                    "departure_at": "2026-10-15T12:00:00+06:00",
                    "transfers": 1,
                    "duration": 200,
                },
            ],
        }

        mock_resp = MagicMock()
        mock_resp.status_code = 200
        mock_resp.json.return_value = mock_response_data

        async def _run() -> None:
            with patch("httpx.AsyncClient.get", new=AsyncMock(return_value=mock_resp)):
                # Search direct only (max_transfers=0)
                offers_direct = await self.provider.search_flights("ALA", "NQZ", "2026-10-15", max_transfers=0)
                self.assertEqual(len(offers_direct), 2)
                # Sorted by price ascending: 18000 then 32000
                self.assertEqual(offers_direct[0].price_kzt, 18000.0)
                self.assertEqual(offers_direct[0].flight_number, "FS-7053")
                self.assertEqual(offers_direct[1].price_kzt, 32000.0)

                # Search allowing 1 transfer (max_transfers=1)
                offers_transfers = await self.provider.search_flights("ALA", "NQZ", "2026-10-15", max_transfers=1)
                self.assertEqual(len(offers_transfers), 3)
                self.assertEqual(offers_transfers[0].price_kzt, 15000.0)  # DV with 1 transfer is cheapest

        asyncio.run(_run())

    def test_search_flights_http_errors_handled_gracefully(self) -> None:
        """Test that HTTP errors and timeouts do not raise exceptions but return []."""
        async def _run() -> None:
            # Non-200 response
            mock_500 = MagicMock()
            mock_500.status_code = 500
            mock_500.text = "Internal Server Error"
            with patch("httpx.AsyncClient.get", new=AsyncMock(return_value=mock_500)):
                res = await self.provider.search_flights("ALA", "NQZ", "2026-10-15")
                self.assertEqual(res, [])

            # Timeout exception
            with patch("httpx.AsyncClient.get", new=AsyncMock(side_effect=httpx.TimeoutException("Timeout"))):
                res = await self.provider.search_flights("ALA", "NQZ", "2026-10-15")
                self.assertEqual(res, [])

            # Generic HTTP error
            with patch("httpx.AsyncClient.get", new=AsyncMock(side_effect=httpx.ConnectError("Connection refused"))):
                res = await self.provider.search_flights("ALA", "NQZ", "2026-10-15")
                self.assertEqual(res, [])

        asyncio.run(_run())

    def test_search_convenience_filtering(self) -> None:
        """Test convenience search method with flight_number and direct_only filters."""
        sample_offers = [
            FlightOffer(
                provider="aviasales",
                airline="Air Astana",
                flight_number="KC-853",
                origin="ALA",
                destination="NQZ",
                departure_time="08:00",
                arrival_time="09:40",
                price_kzt=25000.0,
                transfers_count=0,
            ),
            FlightOffer(
                provider="aviasales",
                airline="Air Astana",
                flight_number="KC-855",
                origin="ALA",
                destination="NQZ",
                departure_time="14:00",
                arrival_time="15:40",
                price_kzt=29000.0,
                transfers_count=0,
            ),
            FlightOffer(
                provider="aviasales",
                airline="SCAT Airlines",
                flight_number="DV-713",
                origin="ALA",
                destination="NQZ",
                departure_time="18:00",
                arrival_time="21:00",
                price_kzt=21000.0,
                transfers_count=1,
            ),
        ]

        async def _run() -> None:
            with patch.object(self.provider, "search_flights", new=AsyncMock(return_value=sample_offers)):
                # Filter specific flight number "KC-853"
                res1 = await self.provider.search(
                    origin="ALA",
                    destination="NQZ",
                    date="2026-10-15",
                    flight_number="KC-853",
                    direct_only=True,
                )
                self.assertEqual(len(res1), 1)
                self.assertEqual(res1[0].flight_number, "KC-853")

                # Filter by number substring "855"
                res2 = await self.provider.search(
                    origin="ALA",
                    destination="NQZ",
                    date="2026-10-15",
                    flight_number="855",
                    direct_only=True,
                )
                self.assertEqual(len(res2), 1)
                self.assertEqual(res2[0].flight_number, "KC-855")

                # Filter direct only
                res3 = await self.provider.search(
                    origin="ALA",
                    destination="NQZ",
                    date="2026-10-15",
                    direct_only=True,
                )
                self.assertEqual(len(res3), 2)

                # Filter all including transfers
                res4 = await self.provider.search(
                    origin="ALA",
                    destination="NQZ",
                    date="2026-10-15",
                    direct_only=False,
                )
                self.assertEqual(len(res4), 3)

        asyncio.run(_run())

    def test_metro_airport_alternatives_dict(self) -> None:
        """Verify METRO_AIRPORT_ALTERNATIVES dictionary mappings."""
        self.assertIn("CTU", METRO_AIRPORT_ALTERNATIVES)
        self.assertEqual(METRO_AIRPORT_ALTERNATIVES["CTU"], ["TFU"])
        self.assertIn("TFU", METRO_AIRPORT_ALTERNATIVES)
        self.assertEqual(METRO_AIRPORT_ALTERNATIVES["TFU"], ["CTU"])
        self.assertEqual(METRO_AIRPORT_ALTERNATIVES["IST"], ["SAW"])
        self.assertEqual(METRO_AIRPORT_ALTERNATIVES["SAW"], ["IST"])
        self.assertEqual(METRO_AIRPORT_ALTERNATIVES["DXB"], ["DWC"])
        self.assertEqual(METRO_AIRPORT_ALTERNATIVES["DWC"], ["DXB"])
        self.assertEqual(METRO_AIRPORT_ALTERNATIVES["BKK"], ["DMK"])
        self.assertEqual(METRO_AIRPORT_ALTERNATIVES["DMK"], ["BKK"])
        self.assertEqual(METRO_AIRPORT_ALTERNATIVES["PEK"], ["PKX"])
        self.assertEqual(METRO_AIRPORT_ALTERNATIVES["PKX"], ["PEK"])
        self.assertEqual(METRO_AIRPORT_ALTERNATIVES["MOW"], ["SVO", "DME", "VKO"])
        self.assertEqual(METRO_AIRPORT_ALTERNATIVES["TYO"], ["HND", "NRT"])
        self.assertEqual(METRO_AIRPORT_ALTERNATIVES["LON"], ["LHR", "LGW", "STN"])

    def test_search_flights_direct_only_param(self) -> None:
        """Verify direct query param sent to Travelpayouts API based on direct_only and max_transfers."""
        mock_resp = MagicMock()
        mock_resp.status_code = 200
        mock_resp.json.return_value = {"success": True, "data": []}

        async def _run() -> None:
            # Case 1: direct_only=True -> direct="true"
            with patch("httpx.AsyncClient.get", new=AsyncMock(return_value=mock_resp)) as mock_get:
                await self.provider.search_flights("ALA", "NQZ", "2026-10-15", max_transfers=2, direct_only=True)
                primary_call = mock_get.call_args_list[0]
                self.assertEqual(primary_call.kwargs["params"]["direct"], "true")

            # Case 2: max_transfers=0, direct_only=False -> direct="true"
            with patch("httpx.AsyncClient.get", new=AsyncMock(return_value=mock_resp)) as mock_get:
                await self.provider.search_flights("ALA", "NQZ", "2026-10-15", max_transfers=0, direct_only=False)
                primary_call = mock_get.call_args_list[0]
                self.assertEqual(primary_call.kwargs["params"]["direct"], "true")

            # Case 3: direct_only=False, max_transfers=2 -> direct="false"
            with patch("httpx.AsyncClient.get", new=AsyncMock(return_value=mock_resp)) as mock_get:
                await self.provider.search_flights("ALA", "NQZ", "2026-10-15", max_transfers=2, direct_only=False)
                primary_call = mock_get.call_args_list[0]
                self.assertEqual(primary_call.kwargs["params"]["direct"], "false")

        asyncio.run(_run())

    def test_strict_airport_post_filtering(self) -> None:
        """Verify strict airport filtering discards wrong airport items (e.g. CTU when TFU requested)."""
        # Test 1: Direct parse_aviasales_json filtering
        raw_payload = {
            "success": True,
            "data": [
                {
                    "origin": "ALA",
                    "destination": "CTU",
                    "destination_airport": "CTU",
                    "price": 75000,
                    "airline": "CA",
                    "flight_number": "CA-484",
                    "departure_at": "2026-11-21T08:00:00+06:00",
                    "transfers": 0,
                },
                {
                    "origin": "ALA",
                    "destination": "TFU",
                    "destination_airport": "TFU",
                    "price": 85000,
                    "airline": "CZ",
                    "flight_number": "CZ-6012",
                    "departure_at": "2026-11-21T10:00:00+06:00",
                    "transfers": 0,
                },
                {
                    "origin": "NQZ",
                    "destination": "TFU",
                    "price": 95000,
                    "airline": "CZ",
                    "flight_number": "CZ-6014",
                    "departure_at": "2026-11-21T12:00:00+06:00",
                    "transfers": 0,
                },
            ],
        }

        offers = AviasalesProvider.parse_aviasales_json(raw_payload, "ALA", "TFU")
        # Only the second offer (ALA -> TFU) must be retained
        self.assertEqual(len(offers), 1)
        self.assertEqual(offers[0].origin, "ALA")
        self.assertEqual(offers[0].destination, "TFU")
        self.assertEqual(offers[0].flight_number, "CZ-6012")
        self.assertEqual(offers[0].price_kzt, 85000.0)

        # Test 2: search_flights with mocked response containing mixed destination airports
        mock_resp = MagicMock()
        mock_resp.status_code = 200
        mock_resp.json.return_value = raw_payload

        async def _run() -> None:
            with patch("httpx.AsyncClient.get", new=AsyncMock(return_value=mock_resp)):
                search_offers = await self.provider.search_flights("ALA", "TFU", "2026-11-21", direct_only=True)
                self.assertEqual(len(search_offers), 1)
                self.assertEqual(search_offers[0].destination, "TFU")
                self.assertEqual(search_offers[0].origin, "ALA")
                self.assertEqual(search_offers[0].flight_number, "CZ-6012")

        asyncio.run(_run())

    def test_search_flights_fallback_month_lookup(self) -> None:
        """Verify fallback month cache lookup is executed and logged when exact date returns 0 offers."""
        empty_exact_resp = MagicMock()
        empty_exact_resp.status_code = 200
        empty_exact_resp.json.return_value = {"success": True, "data": []}

        month_resp = MagicMock()
        month_resp.status_code = 200
        month_resp.json.return_value = {
            "success": True,
            "data": [
                {
                    "origin": "ALA",
                    "destination": "NQZ",
                    "price": 24000,
                    "airline": "KC",
                    "flight_number": "KC-853",
                    "departure_at": "2026-11-10T08:00:00+06:00",
                    "transfers": 0,
                },
                {
                    "origin": "ALA",
                    "destination": "NQZ",
                    "price": 19000,
                    "airline": "FS",
                    "flight_number": "FS-7051",
                    "departure_at": "2026-11-15T06:45:00+06:00",
                    "transfers": 0,
                },
            ],
        }

        async def _run() -> None:
            async def side_effect(*args: Any, **kwargs: Any) -> MagicMock:
                params = kwargs.get("params", {})
                if params.get("departure_at") == "2026-11-21":
                    return empty_exact_resp
                elif params.get("departure_at") == "2026-11":
                    return month_resp
                return empty_exact_resp

            # Case A: Month cache contains offers
            with patch("httpx.AsyncClient.get", new=AsyncMock(side_effect=side_effect)):
                with self.assertLogs("kzflight_sniper.providers.aviasales", level="INFO") as cm:
                    offers = await self.provider.search_flights("ALA", "NQZ", "2026-11-21", direct_only=True)
                    self.assertEqual(offers, [])
                    self.assertTrue(
                        any("Found 2 cached flight offer(s) across month 2026-11 (e.g. min price 19000 KZT)" in log for log in cm.output)
                    )

            # Case B: Month cache completely empty
            with patch("httpx.AsyncClient.get", new=AsyncMock(return_value=empty_exact_resp)):
                with self.assertLogs("kzflight_sniper.providers.aviasales", level="INFO") as cm:
                    offers = await self.provider.search_flights("ALA", "NQZ", "2026-11-21", direct_only=False)
                    self.assertEqual(offers, [])
                    self.assertTrue(
                        any("Cache is completely empty for the entire month 2026-11" in log for log in cm.output)
                    )

        asyncio.run(_run())


if __name__ == "__main__":
    unittest.main()
