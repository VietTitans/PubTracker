from ..dbconnection import get_conn, put_conn

class RecordsRepo:

    @staticmethod
    def insert(paper_id, source_id, external_id):
        conn = get_conn()
        try:
            with conn.cursor() as cur:
                cur.execute("""
                    INSERT INTO records (paper_id, source_id, external_id)
                    VALUES (%s, %s, %s)
                    ON CONFLICT DO NOTHING
                    RETURNING id
                """, (paper_id, source_id, external_id))
                row = cur.fetchone()
                conn.commit()
                return row[0] if row else None
        finally:
            put_conn(conn)
