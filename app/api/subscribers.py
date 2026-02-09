from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from app.db.session import SessionLocal
from app.models.subscribers import Subscribers
from app.models.saved_searches import SavedSearch

router = APIRouter()

def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()

@router.post("/subscribers")
def create_subscriber(email: str, db: Session = Depends(get_db)):
    user = Subscribers(email=email)
    db.add(user)
    db.commit()
    db.refresh(user)
    return user

@router.delete("/subscribers/{subscriber_id}")
def delete_subscriber(subscriber_id: int, db: Session = Depends(get_db)):
    placeholder_id = 1
    placeholder = db.get(Subscribers, placeholder_id)
    if not placeholder:
        placeholder = Subscribers(id=placeholder_id, email="placeholder@example.com")
        db.add(placeholder)
        db.commit()

    subscriber = db.get(Subscribers, subscriber_id)
    if not subscriber:
        raise HTTPException(status_code=404, detail="Subscriber not found")

    # Reassign saved searches to placeholder
    db.query(SavedSearch).filter(SavedSearch.subscriber_id == subscriber_id).update({"subscriber_id": placeholder_id})
    db.delete(subscriber)
    db.commit()
    return {"message": f"Subscriber {subscriber_id} deleted and searches reassigned"}
