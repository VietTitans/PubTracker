import psycopg2
from psycopg2 import pool

# Create a global connection pool
db_pool = None

def init_db_pool(dsn, minconn=1, maxconn=10):
    global db_pool
    db_pool = pool.SimpleConnectionPool(minconn, maxconn, dsn)
    if not db_pool:
        raise Exception("Failed to create database pool.")

def get_conn():
    if db_pool is None:
        raise Exception("Database pool not initialized.")
    return db_pool.getconn()

def put_conn(conn):
    if db_pool is None:
        raise Exception("Database pool not initialized.")
    db_pool.putconn(conn)
