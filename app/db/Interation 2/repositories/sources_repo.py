from ..dbconnection import get_conn, put_conn

class SourcesRepo:

    @staticmethod
    def get_or_create(name):
        conn = get_conn()
        try:
            with conn.cursor() as cur:
                cur.execute("SELECT id FROM sources WHERE name = %s", (name,))
                row = cur.fetchone()
                if row:
                    return row[0]

                cur.execute("""
                    INSERT INTO sources (name)
                    VALUES (%s)
                    RETURNING id
                """, (name,))
                new_id = cur.fetchone()[0]
                conn.commit()
                return new_id
        finally:
            put_conn(conn)
