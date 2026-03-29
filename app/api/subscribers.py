from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from app.db.session import SessionLocal
from app.models.subscriber import Subscriber
from app.db.schema.subscriber_create import SubscriberCreate
from app.models.saved_searches import SavedSearch

router = APIRouter()

def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()

@router.post("/subscribers")
def create_subscriber(subscriber: SubscriberCreate, db: Session = Depends(get_db)):
    subscriber = Subscriber(
        email=subscriber.email, 
        is_active=subscriber.is_active
    )

    db.add(subscriber)
    db.commit()
    db.refresh(subscriber)
    return subscriber

# TODO: Authencation and authorization should be added to these endpoints in the future 
@router.get("/subscribers")
def get_subscribers(db: Session = Depends(get_db)):
    return db.query(Subscriber).all()

@router.delete("/subscribers/{subscriber_id}")
def delete_subscriber(subscriber_id: int, db: Session = Depends(get_db)):
    placeholder_id = 1
    placeholder = db.get(Subscriber, placeholder_id)
    if not placeholder:
        placeholder = Subscriber(id=placeholder_id, email="placeholder@example.com")
        db.add(placeholder)
        db.commit()

    subscriber = db.get(Subscriber, subscriber_id)
    if not subscriber:
        raise HTTPException(status_code=404, detail="Subscriber not found")

    # Reassign saved searches to placeholder
    db.query(SavedSearch).filter(SavedSearch.subscriber_id == subscriber_id).update({"subscriber_id": placeholder_id})
    db.delete(subscriber)
    db.commit()
    return {"message": f"Subscriber {subscriber_id} deleted and searches reassigned"}
