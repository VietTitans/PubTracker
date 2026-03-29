from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from app.db.session import SessionLocal
from app.models.saved_searches import SavedSearch
from app.models.subscriber import Subscriber

router = APIRouter()

def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()

@router.post("/saved_searches")
def create_saved_search(owner_id: int, name: str, query_params: dict, db: Session = Depends(get_db)):
    owner = db.get(Subscriber, owner_id)
    if not owner:
        raise HTTPException(status_code=404, detail="Owner not found")
    search = SavedSearch(subscriber_id=owner.id, name=name, query_params=query_params)
    db.add(search)
    db.commit()
    db.refresh(search)
    return search

@router.post("/saved_searches/{search_id}/subscribe")
def subscribe_to_search(search_id: int, subscriber_id: int, db: Session = Depends(get_db)):
    search = db.get(SavedSearch, search_id)
    subscriber = db.get(Subscriber, subscriber_id)
    if not search or not subscriber:
        raise HTTPException(status_code=404, detail="Search or subscriber not found")
    if subscriber not in search.followers:
        search.followers.append(subscriber)
        db.commit()
    return {"message": f"Subscriber {subscriber_id} is now following search {search_id}"}
