#!/usr/bin/env python3
"""
Price Comparison Scraper
------------------------
Scrape Norwegian grocery prices from Oda and Meny (via direct store APIs
or the Kassal.app aggregator API), store them with date and store name in
a local SQLite database, and find the cheapest store for any product.

Quick-start with Kassal.app (recommended):
  1. Get a free token: https://kassal.app/api/documentation
  2. export KASSAL_TOKEN=<your_token>
  3. python main.py scrape "melk"
  4. python main.py find "melk"

Usage:
  python main.py scrape "melk"                  # scrape via Kassal (all stores)
  python main.py scrape "havregryn" "smør" "egg"
  python main.py scrape "melk" --source direct  # scrape Oda + Meny directly
  python main.py find "melk"                    # cheapest store for 'melk'
  python main.py find "havregryn" --limit 5
  python main.py stats                          # show DB stats
"""

from __future__ import annotations

import argparse
import sys

import db
from scrapers import DIRECT_SCRAPERS, KassalScraper


# ── helpers ──────────────────────────────────────────────────────────────────

def _fmt_price(price: float | None) -> str:
    return f"kr {price:.2f}" if price is not None else "–"


def _fmt_unit(unit_price: float | None, unit: str | None) -> str:
    if unit_price is None:
        return ""
    unit_str = f"/{unit}" if unit else ""
    return f"({_fmt_price(unit_price)}{unit_str})"


# ── commands ──────────────────────────────────────────────────────────────────

def cmd_scrape(queries: list[str], source: str) -> None:
    """Fetch products and save to DB."""
    db.init_db()
    total = 0

    if source == "kassal":
        scraper = KassalScraper()
        print(f"Source: Kassal.app (aggregates all stores)\n")
        by_store = scraper.scrape_all_by_store(queries)
        if not by_store:
            print("No products found. Check your KASSAL_TOKEN env var.")
            return
        for store_name, products in sorted(by_store.items()):
            saved = db.save_products(store_name, products)
            print(f"  {store_name:<20} {saved:>4} products saved")
            total += saved

    else:  # direct
        print("Source: direct store APIs (Oda, Meny)\n")
        for ScraperClass in DIRECT_SCRAPERS:
            scraper = ScraperClass()
            print(f"── {scraper.STORE_NAME} ──")
            products = scraper.scrape_all(queries)
            if not products:
                print("  No products found.")
                continue
            saved = db.save_products(scraper.STORE_NAME, products)
            total += saved
            print(f"  Saved {saved} products.")

    print(f"\nTotal saved: {total} products.")


def cmd_find(query: str, limit: int = 10) -> None:
    """Search DB for cheapest options matching the query."""
    db.init_db()
    results = db.find_cheapest(query, limit=limit)

    if not results:
        print(
            f"No results for '{query}'.\n"
            f"Run first:  python main.py scrape \"{query}\""
        )
        return

    print(f"\nCheapest results for: '{query}'\n")
    print(f"{'#':<3}  {'Store':<18}  {'Product':<42}  {'Price':>10}  {'Unit price':<20}  Date")
    print("─" * 105)
    for i, r in enumerate(results, 1):
        brand = f" [{r['brand']}]" if r.get("brand") else ""
        name = (r["product"] + brand)[:41]
        unit_info = _fmt_unit(r["unit_price"], r["unit"])
        print(
            f"{i:<3}  {r['store']:<18}  {name:<42}  "
            f"{_fmt_price(r['price']):>10}  {unit_info:<20}  {r['scraped_at']}"
        )

    best = results[0]
    print(f"\n  Best deal  →  {best['store']}  {_fmt_price(best['price'])}")


def cmd_stats() -> None:
    """Show DB stats."""
    db.init_db()
    counts = db.product_count()
    stores = db.list_stores()

    if not stores:
        print("Database is empty. Run: python main.py scrape <product>")
        return

    print("\nDatabase stats\n")
    print(f"  {'Store':<20}  {'Products':>10}")
    print("  " + "─" * 32)
    for store in stores:
        print(f"  {store:<20}  {counts.get(store, 0):>10}")
    print(f"\n  {'Total':<20}  {sum(counts.values()):>10}")


# ── CLI ───────────────────────────────────────────────────────────────────────

def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Norwegian grocery price comparison tool",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    sub = parser.add_subparsers(dest="command", required=True)

    # scrape
    p_scrape = sub.add_parser("scrape", help="Scrape prices for one or more products")
    p_scrape.add_argument("queries", nargs="+", metavar="PRODUCT",
                          help="Product names / categories to search for")
    p_scrape.add_argument(
        "--source", choices=["kassal", "direct"], default="kassal",
        help="Data source: 'kassal' (default, all stores via kassal.app API) "
             "or 'direct' (Oda + Meny store APIs)",
    )

    # find
    p_find = sub.add_parser("find", help="Find cheapest store for a product")
    p_find.add_argument("query", help="Product name to look up")
    p_find.add_argument("--limit", type=int, default=10,
                        help="Max results to show (default: 10)")

    # stats
    sub.add_parser("stats", help="Show database statistics")

    return parser


def main() -> None:
    parser = build_parser()
    args = parser.parse_args()

    if args.command == "scrape":
        cmd_scrape(args.queries, source=args.source)
    elif args.command == "find":
        cmd_find(args.query, limit=args.limit)
    elif args.command == "stats":
        cmd_stats()
    else:
        parser.print_help()
        sys.exit(1)


if __name__ == "__main__":
    main()
