"""Base scraper class."""

from __future__ import annotations

import requests


class BaseScraper:
    STORE_NAME: str = ""
    BASE_URL: str = ""

    def __init__(self, timeout: int = 15) -> None:
        self.session = requests.Session()
        self.session.headers.update(
            {
                "User-Agent": (
                    "Mozilla/5.0 (X11; Linux x86_64) "
                    "AppleWebKit/537.36 (KHTML, like Gecko) "
                    "Chrome/124.0 Safari/537.36"
                ),
                "Accept-Language": "nb-NO,nb;q=0.9,no;q=0.8",
            }
        )
        self.timeout = timeout

    def search(self, query: str) -> list[dict]:
        """Return a list of product dicts for the given query."""
        raise NotImplementedError

    def scrape_all(self, queries: list[str]) -> list[dict]:
        """Run search for each query and deduplicate by EAN/name."""
        seen: set[str] = set()
        results: list[dict] = []
        for q in queries:
            for p in self.search(q):
                key = p.get("ean") or p["name"].lower()
                if key not in seen:
                    seen.add(key)
                    results.append(p)
        return results
