"""
Database module for price comparison scraper.
Stores products, prices, store names, and scrape dates in SQLite.
"""

import sqlite3
from datetime import date
from pathlib import Path

DB_PATH = Path(__file__).parent / "prices.db"


def get_connection() -> sqlite3.Connection:
    conn = sqlite3.connect(DB_PATH)
    conn.row_factory = sqlite3.Row
    return conn


def init_db() -> None:
    """Create tables if they don't exist."""
    with get_connection() as conn:
        conn.executescript("""
            CREATE TABLE IF NOT EXISTS stores (
                id   INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT    NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS products (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                store_id   INTEGER NOT NULL REFERENCES stores(id),
                ean        TEXT,
                name       TEXT    NOT NULL,
                brand      TEXT,
                unit       TEXT,
                scraped_at TEXT    NOT NULL,
                price      REAL    NOT NULL,
                unit_price REAL
            );

            CREATE INDEX IF NOT EXISTS idx_products_name
                ON products(name COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS idx_products_ean
                ON products(ean);
            CREATE INDEX IF NOT EXISTS idx_products_scraped_at
                ON products(scraped_at);
        """)


def upsert_store(name: str) -> int:
    """Insert store if missing, return its id."""
    with get_connection() as conn:
        conn.execute("INSERT OR IGNORE INTO stores(name) VALUES(?)", (name,))
        row = conn.execute("SELECT id FROM stores WHERE name=?", (name,)).fetchone()
        return row["id"]


def save_products(store_name: str, products: list[dict]) -> int:
    """
    Persist a list of product dicts for today.
    Each dict must have: name, price
    Optional keys: ean, brand, unit, unit_price

    Returns the number of rows inserted.
    """
    store_id = upsert_store(store_name)
    today = date.today().isoformat()

    rows = [
        (
            store_id,
            p.get("ean"),
            p["name"],
            p.get("brand"),
            p.get("unit"),
            today,
            p["price"],
            p.get("unit_price"),
        )
        for p in products
        if p.get("price") is not None
    ]

    with get_connection() as conn:
        conn.executemany(
            """
            INSERT INTO products(store_id, ean, name, brand, unit, scraped_at, price, unit_price)
            VALUES (?,?,?,?,?,?,?,?)
            """,
            rows,
        )
    return len(rows)


def find_cheapest(query: str, limit: int = 10) -> list[dict]:
    """
    Return the cheapest results for a search term, one row per store.
    Searches name LIKE %query% and picks the lowest price per store
    from the most recent scrape date for that store.
    """
    sql = """
        WITH latest AS (
            SELECT p.store_id, MAX(p.scraped_at) AS max_date
            FROM products p
            JOIN stores s ON s.id = p.store_id
            WHERE p.name LIKE :q COLLATE NOCASE
            GROUP BY p.store_id
        ),
        best AS (
            SELECT
                s.name  AS store,
                p.name  AS product,
                p.brand,
                p.price,
                p.unit_price,
                p.unit,
                p.scraped_at
            FROM products p
            JOIN stores  s ON s.id = p.store_id
            JOIN latest  l ON l.store_id = p.store_id
                           AND l.max_date = p.scraped_at
            WHERE p.name LIKE :q COLLATE NOCASE
        )
        SELECT * FROM best
        ORDER BY price ASC
        LIMIT :limit
    """
    with get_connection() as conn:
        rows = conn.execute(sql, {"q": f"%{query}%", "limit": limit}).fetchall()
    return [dict(r) for r in rows]


def list_stores() -> list[str]:
    with get_connection() as conn:
        rows = conn.execute("SELECT name FROM stores ORDER BY name").fetchall()
    return [r["name"] for r in rows]


def product_count() -> dict[str, int]:
    sql = """
        SELECT s.name AS store, COUNT(*) AS cnt
        FROM products p JOIN stores s ON s.id = p.store_id
        GROUP BY s.name
    """
    with get_connection() as conn:
        rows = conn.execute(sql).fetchall()
    return {r["store"]: r["cnt"] for r in rows}
