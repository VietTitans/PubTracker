import re
import time
from datetime import datetime
from selenium import webdriver
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.common.by import By
from typing import Optional

from connectors.base import BaseConnector, PaperRecord


class PEDroConnector(BaseConnector):
    def __init__(self, search_urls: list[str]):
        self._urls = search_urls

    @property
    def source_name(self) -> str:
        return "PEDro"

    def _init_driver(self):
        options = Options()
        options.add_argument("--headless")
        return webdriver.Chrome(options=options)

    def fetch_records(self) -> list[PaperRecord]:
        """
        Fetches all records across the provided PEDro search URLs.
        Returns a deduplicated list of PaperRecord objects.
        """
        driver = self._init_driver()
        all_records = {}

        for url in self._urls:
            driver.get(url)
            time.sleep(2)

            results = driver.find_elements(By.CSS_SELECTOR, "div.result-title")

            for result in results:
                link = result.find_element(By.TAG_NAME, "a")
                title = link.text.strip()
                record_url = link.get_attribute("href")
                external_id = self._extract_pedro_id(record_url)

                abstract = self._fetch_abstract(driver, record_url)
                doi = self._extract_doi(abstract)
                
                # Type narrowing
                if record_url is None or external_id is None:
                    continue

                all_records[external_id] = PaperRecord(
                    doi=doi,
                    title=title,
                    abstract=abstract,
                    url=record_url,
                    external_id=external_id,
                    date_discovered=datetime.now()
                )

        driver.quit()
        return list(all_records.values())

    def _extract_pedro_id(self, url: Optional[str]) -> Optional[str]:
        if not url:
            return None
        match = re.search(r"id=(\d+)", url)
        return match.group(1) if match else url

    def _fetch_abstract(self, driver, url: Optional[str]) -> Optional[str]:
        driver.get(url)
        time.sleep(1)
        try:
            abstract_div = driver.find_element(By.CSS_SELECTOR, ".abstract")
            return abstract_div.text.strip()
        except:
            return None

    def _extract_doi(self, text: Optional[str]) -> Optional[str]:
        if not text:
            return None
        match = re.search(r"(10\.\d{4,9}/[-._;()/:A-Za-z0-9]+)", text)
        return match.group(1) if match else None
