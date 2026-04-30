from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from app.db.session import SessionLocal
from app.models.saved_searches import SavedSearch
from app.models.user import User

router = APIRouter()

def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()

@router.post("/saved_searches")
def create_saved_search(owner_id: int, name: str, search_query: str, db: Session = Depends(get_db)):
    owner = db.get(User, owner_id)
    if not owner:
        raise HTTPException(status_code=404, detail="Owner not found")
    search = SavedSearch(user_id=owner.id, name=name, search_query=search_query)
    db.add(search)
    db.commit()
    db.refresh(search)
    return search

@router.get("/saved_searches/{search_id}")
def get_saved_search(search_id: int, db: Session = Depends(get_db)):
    search = db.get(SavedSearch, search_id)
    if not search:
        raise HTTPException(status_code=404, detail="Saved search not found")
    return search

@router.get("/saved_searches")
def get_all_saved_searches(db: Session = Depends(get_db)):
    searches = db.query(SavedSearch).all()
    return searches

@router.put("/saved_searches/{search_id}")
def update_saved_search(search_id: int, name: str = None, search_query: str = None, is_active: bool = None, db: Session = Depends(get_db)):
    search = db.get(SavedSearch, search_id)
    if not search:
        raise HTTPException(status_code=404, detail="Saved search not found")
    if name is not None:
        search.name = name
    if search_query is not None:
        search.search_query = search_query
    if is_active is not None:
        search.is_active = is_active
    db.commit()
    db.refresh(search)
    return search

@router.delete("/saved_searches/{search_id}")
def delete_saved_search(search_id: int, db: Session = Depends(get_db)):
    search = db.get(SavedSearch, search_id)
    if not search:
        raise HTTPException(status_code=404, detail="Saved search not found")
    db.delete(search)
    db.commit()
    return {"detail": "Saved search deleted"}