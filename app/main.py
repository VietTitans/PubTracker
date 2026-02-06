from fastapi import FastAPI
from app.api import subscribers, saved_searches
from app.db.session import init_db

app = FastAPI(title="PubTracker")

app.include_router(subscribers.router)
app.include_router(saved_searches.router)

# Initialize DB
init_db()