"""
Meny (meny.no) scraper.
Uses the NorgesGruppen SAP Commerce (Hybris) REST API powering meny.no.
"""

from __future__ import annotations

from .base import BaseScraper


class MenyScraper(BaseScraper):
    STORE_NAME = "Meny"
    HOME_URL = "https://meny.no/"
    # Hybris/SAP Commerce search endpoint used by meny.no
    SEARCH_URL = "https://meny.no/vmpws/v2/meny/search"

    def __init__(self, timeout: int = 15) -> None:
        super().__init__(timeout)
        self.session.headers.update(
            {
                "Accept": "application/json",
                "Referer": "https://meny.no/",
                "Origin": "https://meny.no",
            }
        )
        self._session_ready = False

    def _init_session(self) -> None:
        if self._session_ready:
            return
        try:
            self.session.get(self.HOME_URL, timeout=self.timeout)
            self._session_ready = True
        except Exception as exc:
            print(f"[Meny] Could not initialise session: {exc}")

    def search(self, query: str, page_size: int = 24) -> list[dict]:
        self._init_session()
        params = {
            "query": query,
            "pageSize": page_size,
            "currentPage": 0,
            "fields": "FULL",
        }
        try:
            resp = self.session.get(self.SEARCH_URL, params=params, timeout=self.timeout)
            resp.raise_for_status()
            data = resp.json()
        except Exception as exc:
            print(f"[Meny] Request failed for '{query}': {exc}")
            return []

        products: list[dict] = []
        for item in data.get("results", []):
            try:
                p = self._parse(item)
                if p:
                    products.append(p)
            except (KeyError, TypeError):
                continue
        return products

    def _parse(self, item: dict) -> dict | None:
        # Meny/Hybris product structure:
        # { "name", "code", "price": {"value", "formattedValue"},
        #   "pricePerUnit": {"value"}, "unit": {"name"}, "manufacturer" }
        price_obj = item.get("price") or {}
        price = price_obj.get("value")
        if price is None:
            return None

        unit_obj = item.get("pricePerUnit") or {}
        unit_name_obj = item.get("unit") or {}

        return {
            "name": item.get("name", item.get("code", "Unknown")),
            "brand": item.get("manufacturer"),
            "price": float(price),
            "unit_price": float(unit_obj["value"]) if unit_obj.get("value") else None,
            "unit": unit_name_obj.get("name"),
            "ean": item.get("ean"),
        }
