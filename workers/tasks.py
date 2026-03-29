from celery import Celery
from app.db.session import SessionLocal
from app.models.saved_searches import SavedSearch
from app.models.subscriber import Subscribers

celery = Celery("worker", broker="redis://localhost:6379/0")

@celery.task
def simulate_new_record(search_id: int, paper_title: str):
    db = SessionLocal()
    search = db.get(SavedSearch, search_id)
    if not search:
        db.close()
        return

    # Simulate record and notifications
    print(f"New paper '{paper_title}' for search '{search.name}'")
    for follower in search.followers:
        print(f"Notification sent to {follower.email}")
    db.close()
