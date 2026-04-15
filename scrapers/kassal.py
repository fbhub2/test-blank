"""
Kassal.app scraper — uses the free, public Kassal.app API.

Register for a free token at https://kassal.app/api/documentation
The API aggregates prices from Oda, Meny, Kiwi, Rema 1000, Spar,
Coop, and more.

Set the token via:
  export KASSAL_TOKEN=<your_token>
or pass it directly to KassalScraper(token=...).
"""

from __future__ import annotations

import os

from .base import BaseScraper


class KassalScraper(BaseScraper):
    """
    One scraper instance per store name so prices are stored separately.
    When no specific store is requested, all stores are fetched at once.
    """

    STORE_NAME = "Kassal (all stores)"
    BASE_URL = "https://kassal.app/api/v1/products"

    def __init__(self, token: str | None = None, store_filter: str | None = None,
                 timeout: int = 15) -> None:
        super().__init__(timeout)
        self.token = token or os.getenv("KASSAL_TOKEN", "")
        self.store_filter = store_filter  # e.g. "ODA", "MENY", "KIWI", "REMA_1000"
        if self.token:
            self.session.headers["Authorization"] = f"Bearer {self.token}"

    def search(self, query: str, page_size: int = 50) -> list[dict]:
        if not self.token:
            print(
                "[Kassal] No API token. Get a free token at https://kassal.app/api/documentation "
                "and set KASSAL_TOKEN env var."
            )
            return []

        params: dict = {"search": query, "size": page_size}
        if self.store_filter:
            params["store"] = self.store_filter

        try:
            resp = self.session.get(self.BASE_URL, params=params, timeout=self.timeout)
            resp.raise_for_status()
            data = resp.json()
        except Exception as exc:
            print(f"[Kassal] Request failed for '{query}': {exc}")
            return []

        products: list[dict] = []
        for item in data.get("data", []):
            parsed = self._parse(item)
            if parsed:
                products.append(parsed)
        return products

    def _parse(self, item: dict) -> dict | None:
        # Kassal product structure:
        # { "ean", "name", "brand", "vendor", "current_price", "current_unit_price",
        #   "store": { "name", "code" }, "image", "url" }
        price = item.get("current_price")
        if price is None:
            return None

        store_info = item.get("store", {})
        store_name = store_info.get("name") or item.get("vendor", "Unknown")

        return {
            "name": item.get("name", "Unknown"),
            "brand": item.get("brand"),
            "price": float(price),
            "unit_price": float(item["current_unit_price"]) if item.get("current_unit_price") else None,
            "unit": None,
            "ean": item.get("ean"),
            # Store the per-product store name so callers can split by store
            "_store": store_name,
        }

    def scrape_all_by_store(self, queries: list[str]) -> dict[str, list[dict]]:
        """
        Returns a mapping of store_name -> [product, ...] so the caller
        can persist each store separately.
        """
        by_store: dict[str, list[dict]] = {}
        seen: set[str] = set()

        for q in queries:
            for p in self.search(q):
                store = p.pop("_store")
                key = (store, p.get("ean") or p["name"].lower())
                if key not in seen:
                    seen.add(key)
                    by_store.setdefault(store, []).append(p)
        return by_store
