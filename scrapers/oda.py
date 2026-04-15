"""
Oda (oda.com) scraper.
Fetches the homepage first to obtain session cookies, then queries
the JSON search API.
"""

from __future__ import annotations

from .base import BaseScraper


class OdaScraper(BaseScraper):
    STORE_NAME = "Oda"
    HOME_URL = "https://oda.com/no/"
    SEARCH_URL = "https://oda.com/api/v1/search/"

    def __init__(self, timeout: int = 15) -> None:
        super().__init__(timeout)
        self._session_ready = False

    def _init_session(self) -> None:
        """Visit the homepage once to obtain session cookies."""
        if self._session_ready:
            return
        try:
            self.session.get(self.HOME_URL, timeout=self.timeout)
            self._session_ready = True
        except Exception as exc:
            print(f"[Oda] Could not initialise session: {exc}")

    def search(self, query: str, page_size: int = 24) -> list[dict]:
        self._init_session()
        params = {"q": query, "page_size": page_size}
        try:
            resp = self.session.get(self.SEARCH_URL, params=params, timeout=self.timeout)
            resp.raise_for_status()
            data = resp.json()
        except Exception as exc:
            print(f"[Oda] Request failed for '{query}': {exc}")
            return []

        products: list[dict] = []
        for item in data.get("items", []):
            try:
                products.append(self._parse(item))
            except (KeyError, TypeError):
                continue
        return products

    def _parse(self, item: dict) -> dict:
        brand = item.get("brand")
        if isinstance(brand, dict):
            brand = brand.get("name")
        return {
            "name": item["full_name"],
            "brand": brand,
            "price": float(item["gross_price"]),
            "unit_price": float(item["gross_unit_price"]) if item.get("gross_unit_price") else None,
            "unit": item.get("unit_price_quantity_abbreviation"),
            "ean": item.get("ean"),
        }
