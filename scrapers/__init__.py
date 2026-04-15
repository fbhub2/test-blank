from .oda import OdaScraper
from .meny import MenyScraper
from .kassal import KassalScraper

# Direct store scrapers (require live sessions; may be blocked by WAF)
DIRECT_SCRAPERS = [OdaScraper, MenyScraper]
