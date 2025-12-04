from abc import ABC, abstractmethod
from dataclasses import dataclass
from datetime import datetime
from typing import Optional


@dataclass
class PaperRecord:
    doi: Optional[str]
    title: str
    abstract: Optional[str]
    url: str
    external_id: str
    date_discovered: datetime


class BaseConnector(ABC):
    """
    Abstract connector for any research database.
    """

    @abstractmethod
    def fetch_records(self) -> list[PaperRecord]:
        """
        Fetches all available records from the data source.

        Returns:
            list[PaperRecord]: Normalized record objects.
        """
        pass

    @property
    @abstractmethod
    def source_name(self) -> str:
        """
        Human-friendly identifier of the source (e.g. 'PEDro').
        """
        pass
