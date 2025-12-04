from dataclasses import dataclass
from typing import Optional
from ..dbconnection import get_conn, put_conn


@dataclass
class Paper:
    id: int
    doi: str
    title: str
    abstract: Optional[str]
    url: str
    date_first_discovered: str


class PapersRepo:

    @staticmethod
    def get_by_doi(doi: str) -> Optional[Paper]:
        conn = get_conn()
        try:
            with conn.cursor() as cur:
                cur.execute("""
                    SELECT id, doi, title, abstract, url, date_first_discovered
                    FROM papers WHERE doi = %s
                """, (doi,))
                row = cur.fetchone()
                if not row:
                    return None
                return Paper(*row)
        finally:
            put_conn(conn)

    @staticmethod
    def insert(doi, title, abstract, url):
        conn = get_conn()
        try:
            with conn.cursor() as cur:
                cur.execute("""
                    INSERT INTO papers (doi, title, abstract, url)
                    VALUES (%s, %s, %s, %s)
                    RETURNING id
                """, (doi, title, abstract, url))
                paper_id = cur.fetchone()[0]
                conn.commit()
                return paper_id
        finally:
            put_conn(conn)
