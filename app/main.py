from sqlalchemy import text
from fastapi import FastAPI
from app.api import subscribers, saved_searches
from app.db.session import init_db, engine
from app.models.base import Base

app = FastAPI(title="PubTracker")

app.include_router(subscribers.router)
app.include_router(saved_searches.router)

# Test database connection and create tables
Base.metadata.create_all(bind=engine)

init_db()

@app.get("/health")
def health():
    return {"status": "ok"}

@app.on_event("startup")
def startup():
    with engine.connect() as conn:
        conn.execute(text("SELECT 1"))