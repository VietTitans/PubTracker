import fastapi
from sqlalchemy import text
from fastapi import FastAPI
from app.api import saved_searches, users
from app.db.session import init_db, engine
from app.models.base import Base
import fastapi_swagger_dark as fsd

app = FastAPI(
    title="PubTracker",
    docs_url=None,
    redoc_url=None,
)

router = fastapi.APIRouter()
fsd.install(router, path="/swagger")
app.include_router(router)

app.include_router(saved_searches.router)
app.include_router(users.router)

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